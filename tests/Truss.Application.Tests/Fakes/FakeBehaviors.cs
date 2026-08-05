using Truss.Application.Abstractions.Requests;

namespace Truss.Application.Tests.Fakes
{
    public class CallLog
    {
        public List<string> Entries { get; } = [];
    }

    public class OuterBehavior(CallLog log) : IPipelineBehavior<PingCommand, string>
    {
        public async Task<string> Handle(
            PingCommand request,
            RequestHandlerDelegate<string> next,
            CancellationToken cancellationToken)
        {
            log.Entries.Add("outer:before");
            var response = await next();
            log.Entries.Add("outer:after");
            return response;
        }
    }

    public class InnerBehavior(CallLog log) : IPipelineBehavior<PingCommand, string>
    {
        public async Task<string> Handle(
            PingCommand request,
            RequestHandlerDelegate<string> next,
            CancellationToken cancellationToken)
        {
            log.Entries.Add("inner:before");
            var response = await next();
            log.Entries.Add("inner:after");
            return response;
        }
    }
}
