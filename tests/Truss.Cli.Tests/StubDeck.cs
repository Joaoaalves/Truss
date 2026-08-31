using System.Net;
using System.Text;
using System.Text.Json;

namespace Truss.Cli.Tests
{
    /// <summary>
    /// A contract-faithful deck for the end-to-end proof: it enforces the
    /// service credential, demands the idempotency key on writes, stores
    /// tickets in memory and answers with the /v1 wire shapes. When a ticket
    /// opens, an agent reply is staged, so the application under test can
    /// show a whole conversation it never stored.
    /// </summary>
    internal sealed class StubDeck : IDisposable
    {
        private sealed record StoredMessage(Guid Id, string Author, string Body, DateTimeOffset SentOn);

        private sealed record StoredTicket(Guid Id, string ExternalUserId, string Subject, List<StoredMessage> Messages)
        {
            public string Status { get; set; } = "Open";
        }

        private readonly HttpListener _listener = new();
        private readonly List<StoredTicket> _tickets = [];
        private readonly string _apiKey;

        public List<string> PresentedKeys { get; } = [];

        public List<string?> IdempotencyKeys { get; } = [];

        public string? LastRequesterExternalUserId { get; private set; }

        public StubDeck(int port, string apiKey)
        {
            _apiKey = apiKey;
            _listener.Prefixes.Add($"http://localhost:{port}/");
            _listener.Start();
            _ = Task.Run(Loop);
        }

        private async Task Loop()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;

                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (Exception)
                {
                    return;
                }

                try
                {
                    await Answer(context);
                }
                catch (Exception)
                {
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
            }
        }

        private async Task Answer(HttpListenerContext context)
        {
            var request = context.Request;

            lock (PresentedKeys)
            {
                PresentedKeys.Add(request.Headers["X-Deck-Key"] ?? string.Empty);
            }

            if (request.Headers["X-Deck-Key"] != _apiKey)
            {
                context.Response.StatusCode = 401;
                context.Response.Close();
                return;
            }

            var path = request.Url!.AbsolutePath.TrimEnd('/');
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (request.HttpMethod == "POST" && path == "/v1/tickets")
            {
                lock (IdempotencyKeys)
                {
                    IdempotencyKeys.Add(request.Headers["Idempotency-Key"]);
                }

                using var document = await JsonDocument.ParseAsync(request.InputStream);
                var requester = document.RootElement.GetProperty("requester");
                var externalUserId = requester.GetProperty("externalUserId").GetString()!;
                LastRequesterExternalUserId = externalUserId;

                var ticket = new StoredTicket(Guid.NewGuid(), externalUserId, document.RootElement.GetProperty("subject").GetString()!, []);
                ticket.Messages.Add(new StoredMessage(Guid.NewGuid(), "Customer", document.RootElement.GetProperty("body").GetString()!, DateTimeOffset.UtcNow));

                // The staged attendance: an agent answers the moment it lands.
                ticket.Messages.Add(new StoredMessage(Guid.NewGuid(), "Agent", "We are on it.", DateTimeOffset.UtcNow.AddSeconds(1)));
                ticket.Status = "WaitingOnCustomer";

                lock (_tickets)
                {
                    _tickets.Add(ticket);
                }

                await Json(context, 201, new { ticketId = ticket.Id });
                return;
            }

            if (request.HttpMethod == "POST" && segments.Length == 4 && segments[3] == "messages")
            {
                var ticketId = Guid.Parse(segments[2]);

                lock (_tickets)
                {
                    var ticket = _tickets.Single(stored => stored.Id == ticketId);
                    ticket.Messages.Add(new StoredMessage(Guid.NewGuid(), "Customer", "reply", DateTimeOffset.UtcNow));
                    ticket.Status = "Open";
                }

                await Json(context, 200, new { ticketId });
                return;
            }

            if (request.HttpMethod == "GET" && path == "/v1/tickets")
            {
                var externalUserId = request.QueryString["externalUserId"];

                List<object> items;

                lock (_tickets)
                {
                    items = _tickets
                        .Where(ticket => ticket.ExternalUserId == externalUserId)
                        .Select(ticket => (object)new
                        {
                            id = ticket.Id,
                            subject = ticket.Subject,
                            status = ticket.Status,
                            priority = "Normal",
                            openedOn = DateTimeOffset.UtcNow,
                            lastMessageOn = DateTimeOffset.UtcNow
                        })
                        .ToList();
                }

                await Json(context, 200, new { items, page = 1, size = 20, totalCount = items.Count });
                return;
            }

            if (request.HttpMethod == "GET" && segments.Length == 3)
            {
                var ticketId = Guid.Parse(segments[2]);
                object? payload = null;

                lock (_tickets)
                {
                    var ticket = _tickets.FirstOrDefault(stored =>
                        stored.Id == ticketId && stored.ExternalUserId == request.QueryString["externalUserId"]);

                    if (ticket is not null)
                    {
                        payload = new
                        {
                            id = ticket.Id,
                            subject = ticket.Subject,
                            status = ticket.Status,
                            priority = "Normal",
                            linkedFromTicketId = (Guid?)null,
                            openedOn = DateTimeOffset.UtcNow,
                            messages = ticket.Messages.Select(message => new
                            {
                                id = message.Id,
                                author = message.Author,
                                body = message.Body,
                                sentOn = message.SentOn
                            })
                        };
                    }
                }

                if (payload is null)
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    return;
                }

                await Json(context, 200, payload);
                return;
            }

            context.Response.StatusCode = 404;
            context.Response.Close();
        }

        private static async Task Json(HttpListenerContext context, int status, object payload)
        {
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            await context.Response.OutputStream.WriteAsync(body);
            context.Response.Close();
        }

        public void Dispose()
        {
            try
            {
                _listener.Stop();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}
