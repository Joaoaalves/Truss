using System.Net;
using System.Text;
using System.Text.Json;
using Truss.Application;
using Truss.Domain;
using Truss.Support;
using Xunit;

namespace Truss.Support.Tests
{
    /// <summary>
    /// The client against a scripted deck: the wire shape it sends, the
    /// headers it carries and the local exceptions it rebuilds from the
    /// deck's answers.
    /// </summary>
    public class SupportDeckClientTests
    {
        private sealed class ScriptedDeck(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
        {
            public HttpRequestMessage? LastRequest { get; private set; }

            public string? LastBody { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequest = request;
                LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
                return answer(request);
            }
        }

        private static readonly SupportRequester Maria = new("user-42", "maria@example.com", "Maria");

        private static (ISupportDeckClient Client, ScriptedDeck Deck) Client(Func<HttpRequestMessage, HttpResponseMessage> answer)
        {
            var deck = new ScriptedDeck(answer);
            var http = new HttpClient(deck) { BaseAddress = new Uri("http://deck.local/") };
            http.DefaultRequestHeaders.Add("X-Deck-Key", "deck_test");

            return (new SupportDeckClient(http), deck);
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string body)
        {
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }

        [Fact]
        public async Task OpenTicket_CarriesTheKeyAndAnIdempotencyKey()
        {
            var ticketId = Guid.NewGuid();
            var (client, deck) = Client(_ => Json(HttpStatusCode.Created, $$"""{"ticketId":"{{ticketId}}"}"""));

            var opened = await client.OpenTicket(Maria, "The export is broken", "It fails with a 500.");

            Assert.Equal(ticketId, opened);
            Assert.Equal("http://deck.local/v1/tickets", deck.LastRequest!.RequestUri!.ToString());
            Assert.True(deck.LastRequest.Headers.Contains("Idempotency-Key"));

            using var body = JsonDocument.Parse(deck.LastBody!);
            Assert.Equal("user-42", body.RootElement.GetProperty("requester").GetProperty("externalUserId").GetString());
        }

        [Fact]
        public async Task AValidationProblem_BecomesTheLocalValidationException()
        {
            var (client, _) = Client(_ => Json(HttpStatusCode.BadRequest,
                """{"errors":{"Subject":["The subject is required."]}}"""));

            var exception = await Assert.ThrowsAsync<RequestValidationException>(
                () => client.OpenTicket(Maria, "", "Body."));

            var failure = Assert.Single(exception.Errors);
            Assert.Equal("Subject", failure.PropertyName);
        }

        [Fact]
        public async Task ABrokenRule_ArrivesWithItsStableCode()
        {
            var (client, _) = Client(_ => Json(HttpStatusCode.UnprocessableEntity,
                """{"code":"support.closed-to-replies","detail":"The ticket no longer accepts replies; open a new one."}"""));

            var exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
                () => client.Reply(Guid.NewGuid(), Maria, "Hello?"));

            Assert.Equal("support.closed-to-replies", exception.BrokenRule.Code);
        }

        [Fact]
        public async Task ARejectedCredential_SaysSoByName()
        {
            var (client, _) = Client(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

            var exception = await Assert.ThrowsAsync<SupportDeckException>(
                () => client.ListTickets("user-42"));

            Assert.Contains("credential", exception.Message);
        }

        [Fact]
        public async Task ADeadDeck_NamesTheOperationAndTheAddress()
        {
            var (client, _) = Client(_ => throw new HttpRequestException("connection refused"));

            var exception = await Assert.ThrowsAsync<SupportDeckException>(
                () => client.GetTicket(Guid.NewGuid(), "user-42"));

            Assert.Contains("http://deck.local/", exception.Message);
        }

        [Fact]
        public async Task AMissingTicket_IsNull_NotAnError()
        {
            var (client, _) = Client(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

            Assert.Null(await client.GetTicket(Guid.NewGuid(), "user-42"));
        }


        [Fact]
        public async Task UploadAttachment_TravelsAsMultipart_WithTheFileNamed()
        {
            var attachmentId = Guid.NewGuid();
            var (client, deck) = Client(_ => Json(HttpStatusCode.Created,
                $$"""{"attachmentId":"{{attachmentId}}","status":"Scanning"}"""));

            using var content = new MemoryStream("%PDF-1.7 evidence"u8.ToArray());
            var receipt = await client.UploadAttachment(Guid.NewGuid(), "user-42", "invoice.pdf", "application/pdf", content);

            Assert.Equal(attachmentId, receipt.AttachmentId);
            Assert.Equal(SupportAttachmentStatus.Scanning, receipt.Status);
            Assert.Contains("externalUserId=user-42", deck.LastRequest!.RequestUri!.Query);
            Assert.True(deck.LastRequest.Headers.Contains("Idempotency-Key"));
            Assert.StartsWith("multipart/form-data", deck.LastRequest.Content!.Headers.ContentType!.MediaType);
            Assert.Contains("filename=invoice.pdf", deck.LastBody!);
        }

        [Fact]
        public async Task DownloadAttachment_CarriesTheBytes_AndTheName()
        {
            var (client, _) = Client(_ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent("evidence"u8.ToArray())
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
                response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
                {
                    FileName = "\"invoice.pdf\""
                };
                return response;
            });

            await using var download = await client.DownloadAttachment(Guid.NewGuid(), Guid.NewGuid(), "user-42");

            Assert.Equal("invoice.pdf", download!.FileName);
            Assert.Equal("application/pdf", download.ContentType);
            using var reader = new StreamReader(download.Content);
            Assert.Equal("evidence", await reader.ReadToEndAsync());
        }

        [Fact]
        public async Task DownloadAttachment_WhileHeldOrMissing_IsNull()
        {
            var (client, _) = Client(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

            Assert.Null(await client.DownloadAttachment(Guid.NewGuid(), Guid.NewGuid(), "user-42"));
        }

        [Fact]
        public async Task ThePage_ComesBackWithItsCounters()
        {
            var (client, _) = Client(_ => Json(HttpStatusCode.OK,
                """{"items":[{"id":"5e0e5c3a-0b32-4f0e-9d3a-111111111111","subject":"Hi","status":"WaitingOnCustomer","priority":"Normal","openedOn":"2026-01-10T12:00:00+00:00","lastMessageOn":"2026-01-10T12:05:00+00:00"}],"page":1,"size":20,"totalCount":1}"""));

            var page = await client.ListTickets("user-42");

            Assert.Equal(1, page.TotalCount);
            Assert.Equal(SupportTicketStatus.WaitingOnCustomer, Assert.Single(page.Items).Status);
        }
    }
}
