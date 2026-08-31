using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Truss.Application;
using Truss.Domain;

namespace Truss.Support
{
    internal sealed class SupportDeckClient(HttpClient http) : ISupportDeckClient
    {
        private sealed record OpenTicketRequest(SupportRequester Requester, string Subject, string Body, IReadOnlyDictionary<string, string>? Metadata);

        private sealed record ReplyRequest(SupportRequester Requester, string Body);

        private sealed record TicketAcceptedResponse(Guid TicketId);

        public async Task<Guid> OpenTicket(SupportRequester requester, string subject, string body, IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default)
        {
            var response = await Post("v1/tickets", new OpenTicketRequest(requester, subject, body, metadata), cancellationToken);
            return (await Read<TicketAcceptedResponse>(response, "OpenTicket", cancellationToken)).TicketId;
        }

        public async Task<Guid> Reply(Guid ticketId, SupportRequester requester, string body, CancellationToken cancellationToken = default)
        {
            var response = await Post($"v1/tickets/{ticketId}/messages", new ReplyRequest(requester, body), cancellationToken);
            return (await Read<TicketAcceptedResponse>(response, "Reply", cancellationToken)).TicketId;
        }

        public async Task<PageResult<SupportTicketSummary>> ListTickets(string externalUserId, int page = 1, int size = 20, CancellationToken cancellationToken = default)
        {
            var response = await Send(
                new HttpRequestMessage(HttpMethod.Get, $"v1/tickets?externalUserId={Uri.EscapeDataString(externalUserId)}&page={page}&size={size}"),
                cancellationToken);

            return await Read<PageResult<SupportTicketSummary>>(response, "ListTickets", cancellationToken);
        }

        public async Task<SupportTicket?> GetTicket(Guid ticketId, string externalUserId, CancellationToken cancellationToken = default)
        {
            var response = await Send(
                new HttpRequestMessage(HttpMethod.Get, $"v1/tickets/{ticketId}?externalUserId={Uri.EscapeDataString(externalUserId)}"),
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                response.Dispose();
                return null;
            }

            return await Read<SupportTicket>(response, "GetTicket", cancellationToken);
        }

        private async Task<HttpResponseMessage> Post<TBody>(string path, TBody body, CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };

            // A fresh key per logical call: infrastructure retries of the same
            // call reuse the message? No: HttpClient does not retry on its own.
            // The key exists so a handler retried by its caller, or a resilience
            // pipeline the application adds, can never duplicate the effect.
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("n"));

            return await Send(request, cancellationToken);
        }

        private async Task<HttpResponseMessage> Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try
            {
                return await http.SendAsync(request, cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                throw new SupportDeckException(
                    $"The deck did not answer {request.Method} {new Uri(http.BaseAddress!, request.RequestUri!)}.", exception);
            }
        }

        private static async Task<T> Read<T>(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
        {
            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
                        ?? throw new SupportDeckException($"The deck answered {operation} with an empty body.");
                }

                await ThrowAsTheLocalOutcome(response, cancellationToken);

                throw new SupportDeckException(response.StatusCode == HttpStatusCode.Unauthorized
                    ? $"The deck rejected this application's credential on {operation}. Was the key rotated or the app deactivated?"
                    : $"The deck answered {operation} with {(int)response.StatusCode}.");
            }
        }

        /// <summary>
        /// Translates the ProblemDetails the deck's pipeline produced back
        /// into the exceptions the local pipeline would have thrown.
        /// </summary>
        private static async Task ThrowAsTheLocalOutcome(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            if (response.StatusCode is not (HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity))
                return;

            JsonDocument problem;

            try
            {
                problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            }
            catch (JsonException)
            {
                return;
            }

            using (problem)
            {
                if (response.StatusCode == HttpStatusCode.BadRequest && problem.RootElement.TryGetProperty("errors", out var errors))
                {
                    var failures = new List<ValidationError>();

                    foreach (var property in errors.EnumerateObject())
                    {
                        foreach (var message in property.Value.EnumerateArray())
                            failures.Add(new ValidationError(property.Name, message.GetString() ?? string.Empty));
                    }

                    throw new RequestValidationException(failures);
                }

                if (response.StatusCode == HttpStatusCode.UnprocessableEntity && problem.RootElement.TryGetProperty("code", out var code))
                {
                    var detail = problem.RootElement.TryGetProperty("detail", out var element)
                        ? element.GetString() ?? string.Empty
                        : string.Empty;

                    throw new BusinessRuleValidationException(new DeckBusinessRule(code.GetString() ?? string.Empty, detail));
                }
            }
        }
    }

    /// <summary>
    /// A business rule broken on the deck. It carries the rule's stable code
    /// and message, so callers branching on codes do not care where it ran.
    /// </summary>
    internal sealed class DeckBusinessRule(string code, string message) : IBusinessRule
    {
        public bool IsBroken() => true;

        public string Message => message;

        public string Code => code;
    }
}
