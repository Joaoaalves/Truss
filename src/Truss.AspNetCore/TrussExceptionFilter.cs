using Microsoft.AspNetCore.Http;
using Truss.Application;
using Truss.Domain;

namespace Truss.AspNetCore
{
    internal sealed class TrussExceptionFilter : IEndpointFilter
    {
        public static readonly TrussExceptionFilter Instance = new();

        private TrussExceptionFilter()
        {
        }

        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            try
            {
                return await next(context);
            }
            catch (RequestValidationException exception)
            {
                return Results.ValidationProblem(ToErrorDictionary(exception));
            }
            catch (BusinessRuleValidationException exception)
            {
                return Results.Problem(
                    title: "A business rule was violated.",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    extensions: new Dictionary<string, object?>
                    {
                        ["rule"] = exception.BrokenRule.GetType().Name,
                        ["code"] = exception.BrokenRule.Code
                    });
            }
        }

        private static Dictionary<string, string[]> ToErrorDictionary(RequestValidationException exception)
        {
            return exception.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray());
        }
    }
}
