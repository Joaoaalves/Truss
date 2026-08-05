namespace Truss.Cli.Templates
{
    internal static class GeneratorTemplates
    {
        public const string AggregateId = """
            using Truss.Domain;

            namespace __NS_DOMAIN__
            {
                public sealed record __TYPE__Id(Guid Value) : TypedId<Guid>(Value);
            }
            """;

        public const string AggregateCreated = """
            using Truss.Domain;

            namespace __NS_DOMAIN__
            {
                public sealed record __TYPE__Created(__TYPE__Id __TYPE__Id) : DomainEvent;
            }
            """;

        public const string Aggregate = """
            using Truss.Domain;

            namespace __NS_DOMAIN__
            {
                public class __TYPE__ : AggregateRoot<__TYPE__Id>
                {
                    private __TYPE__()
                    {
                    }

                    private __TYPE__(__TYPE__Id id) : base(id)
                    {
                    }

                    public static __TYPE__ Create()
                    {
                        var instance = new __TYPE__(new __TYPE__Id(Guid.NewGuid()));
                        instance.AddDomainEvent(new __TYPE__Created(instance.Id));
                        return instance;
                    }
                }
            }
            """;

        public const string Command = """
            using Truss.Application;

            namespace __NS_APPLICATION__
            {
                public sealed record __TYPE__ : ICommand;
            }
            """;

        public const string CommandHandler = """
            using Truss.Application;

            namespace __NS_APPLICATION__
            {
                public class __TYPE__Handler : ICommandHandler<__TYPE__>
                {
                    public Task<Unit> Handle(__TYPE__ command, CancellationToken cancellationToken)
                    {
                        return Task.FromResult(Unit.Value);
                    }
                }
            }
            """;

        public const string CommandValidator = """
            using FluentValidation;

            namespace __NS_APPLICATION__
            {
                public class __TYPE__Validator : AbstractValidator<__TYPE__>
                {
                    public __TYPE__Validator()
                    {
                    }
                }
            }
            """;

        public const string Query = """
            using Truss.Application;

            namespace __NS_APPLICATION__
            {
                public sealed record __TYPE__ : IQuery<__RESULT__>;
            }
            """;

        public const string QueryHandler = """
            using Truss.Application;

            namespace __NS_APPLICATION__
            {
                public class __TYPE__Handler : IQueryHandler<__TYPE__, __RESULT__>
                {
                    public Task<__RESULT__> Handle(__TYPE__ query, CancellationToken cancellationToken)
                    {
                        throw new NotImplementedException();
                    }
                }
            }
            """;
    }
}
