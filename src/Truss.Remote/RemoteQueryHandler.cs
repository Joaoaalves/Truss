using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Truss.Application;
using Truss.Domain;

namespace Truss.Remote
{
    /// <summary>
    /// Forwards a query to the context's remote host and gives the caller the
    /// same outcomes a local dispatch would: the result, a
    /// RequestValidationException for a 400 or a BusinessRuleValidationException
    /// carrying the rule's stable code for a 422. Anything else becomes a
    /// RemoteContextException, because the network is allowed to fail and the
    /// caller must know it can.
    /// </summary>
    internal sealed class RemoteQueryHandler<TQuery, TResult>(
        IHttpClientFactory factory,
        string contextName,
        string clientName,
        string prefix) : IRequestHandler<TQuery, TResult>
        where TQuery : IRequest<TResult>
    {
        private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;

        public async Task<TResult> Handle(TQuery request, CancellationToken cancellationToken)
        {
            var client = factory.CreateClient(clientName);
            var route = $"{prefix.TrimEnd('/')}/{typeof(TQuery).FullName}";

            HttpResponseMessage response;

            try
            {
                response = await client.PostAsJsonAsync(route, request, Json, cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                throw new RemoteContextException(
                    $"The {contextName} context did not answer {typeof(TQuery).Name} at {client.BaseAddress}{route.TrimStart('/')}.", exception);
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    // A null result arrives as an empty body or the JSON null
                    // literal; both answer "not found", exactly like the local
                    // handler would.
                    var payload = await response.Content.ReadAsStringAsync(cancellationToken);

                    return string.IsNullOrWhiteSpace(payload)
                        ? default!
                        : JsonSerializer.Deserialize<TResult>(payload, Json)!;
                }

                await ThrowAsTheLocalOutcome(response, cancellationToken);

                throw new RemoteContextException(
                    $"The {contextName} context answered {typeof(TQuery).Name} with {(int)response.StatusCode}.");
            }
        }

        /// <summary>
        /// Translates the ProblemDetails the remote pipeline produced back into
        /// the exceptions the local pipeline would have thrown.
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

                    throw new BusinessRuleValidationException(new RemoteBusinessRule(code.GetString() ?? string.Empty, detail));
                }
            }
        }
    }

    /// <summary>
    /// A business rule broken on the other side of the wire. It carries the
    /// remote rule's stable code and message, so callers branching on codes do
    /// not care where the rule ran.
    /// </summary>
    internal sealed class RemoteBusinessRule(string code, string message) : IBusinessRule
    {
        public bool IsBroken() => true;

        public string Message { get; } = message;

        public string Code { get; } = code;
    }
}
