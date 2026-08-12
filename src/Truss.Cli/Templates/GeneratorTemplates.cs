namespace Truss.Cli.Templates
{
    internal static class GeneratorTemplates
    {
        public const string AggregateId = """
            using Truss.Domain;

            namespace __NS_AGG__.ValueObjects
            {
                public sealed record __TYPE__Id(Guid Value) : TypedId<Guid>(Value);
            }
            """;

        public const string AggregateCreated = """
            using __NS_AGG__.ValueObjects;
            using Truss.Domain;

            namespace __NS_AGG__.Events
            {
                public sealed record __TYPE__Created(__TYPE__Id __TYPE__Id) : DomainEvent;
            }
            """;

        public const string Aggregate = """
            using __NS_AGG__.Events;
            using __NS_AGG__.Rules;
            using __NS_AGG__.ValueObjects;
            using Truss.Domain;

            namespace __NS_AGG__
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
                        CheckRule(new __TYPE__MustBeValid());

                        var instance = new __TYPE__(new __TYPE__Id(Guid.NewGuid()));
                        instance.AddDomainEvent(new __TYPE__Created(instance.Id));
                        return instance;
                    }
                }
            }
            """;

        public const string Command = """
            namespace __NS_APPLICATION__
            {
                using Truss.Application;

                public sealed record __TYPE__ : ICommand;
            }
            """;

        public const string CommandHandler = """
            namespace __NS_APPLICATION__
            {
                using Truss.Application;

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
            namespace __NS_APPLICATION__
            {
                using FluentValidation;

                public class __TYPE__Validator : AbstractValidator<__TYPE__>
                {
                    public __TYPE__Validator()
                    {
                    }
                }
            }
            """;

        public const string Query = """
            namespace __NS_APPLICATION__
            {__DTO_USING__
                using Truss.Application;

                public sealed record __TYPE__ : IQuery<__RESULT__>;
            }
            """;

        public const string QueryHandler = """
            namespace __NS_APPLICATION__
            {__DTO_USING__
                using Truss.Application;

                public class __TYPE__Handler : IQueryHandler<__TYPE__, __RESULT__>
                {
                    public Task<__RESULT__> Handle(__TYPE__ query, CancellationToken cancellationToken)
                    {
                        throw new NotImplementedException();
                    }
                }
            }
            """;

        public const string QueryPaged = """
            namespace __NS_APPLICATION__
            {__DTO_USING__
                using Truss.Application;

                public sealed record __TYPE__(int Page = 1, int Size = 20) : IQuery<PageResult<__RESULT__>>;
            }
            """;

        public const string QueryPagedHandler = """
            namespace __NS_APPLICATION__
            {__DTO_USING__
                using Truss.Application;

                public class __TYPE__Handler : IQueryHandler<__TYPE__, PageResult<__RESULT__>>
                {
                    public Task<PageResult<__RESULT__>> Handle(__TYPE__ query, CancellationToken cancellationToken)
                    {
                        // Order the source, then: .ToPageAsync(new PageRequest(query.Page, query.Size), cancellationToken)
                        throw new NotImplementedException();
                    }
                }
            }
            """;

        public const string QueryPagedValidator = """
            namespace __NS_APPLICATION__
            {
                using FluentValidation;

                public class __TYPE__Validator : AbstractValidator<__TYPE__>
                {
                    public __TYPE__Validator()
                    {
                        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
                        RuleFor(query => query.Size).InclusiveBetween(1, 100);
                    }
                }
            }
            """;

        public const string AggregateRule = """
            using Truss.Domain;

            namespace __NS_AGG__.Rules
            {
                public class __TYPE__MustBeValid : IBusinessRule
                {
                    // Replace with the first real invariant of __TYPE__; the wiring
                    // in Create shows where rules belong.
                    public bool IsBroken() => false;

                    public string Message => "The __TYPE__ is not valid.";

                    // Clients switch on this code, so it survives renaming the class.
                    public string Code => "__CAMEL__.invalid";
                }
            }
            """;

        public const string AggregateCrud = """
            using __NS_AGG__.Events;
            using __NS_AGG__.Rules;
            using __NS_AGG__.ValueObjects;
            using Truss.Domain;

            namespace __NS_AGG__
            {
                public class __TYPE__ : AggregateRoot<__TYPE__Id>
                {
                    private __TYPE__()
                    {
                    }

                    private __TYPE__(__TYPE__Id id, string name) : base(id)
                    {
                        Name = name;
                    }

                    public string Name { get; private set; } = string.Empty;

                    public static __TYPE__ Create(string name)
                    {
                        CheckRule(new __TYPE__MustBeValid());

                        var instance = new __TYPE__(new __TYPE__Id(Guid.NewGuid()), name);
                        instance.AddDomainEvent(new __TYPE__Created(instance.Id));
                        return instance;
                    }

                    // Name is a starter field; model real changes as intention-revealing
                    // methods like this one instead of property setters.
                    public void Rename(string name)
                    {
                        Name = name;
                    }
                }
            }
            """;

        public const string Entity = """
            using __NS_AGG__.ValueObjects;
            using Truss.Domain;

            namespace __NS_AGG__
            {
                public class __TYPE__ : Entity<__TYPE__Id>
                {
                    private __TYPE__()
                    {
                    }

                    public __TYPE__(__TYPE__Id id) : base(id)
                    {
                    }
                }
            }
            """;

        public const string QueryDto = """
            namespace __NS_DTOS__
            {
                // The shape this query answers with. Add the members the client needs;
                // a DTO is a contract, not a mirror of the aggregate.
                public sealed record __RESULT__(Guid Id);
            }
            """;

        public const string CrudDto = """
            namespace __NS_FEATURE__.DTOs
            {
                public sealed record __TYPE__Dto(Guid Id, string Name);
            }
            """;

        public const string CrudRepository = """
            namespace __NS_FEATURE__
            {
                using __NS_AGG__;
                using __NS_AGG__.ValueObjects;
                using __NS_FEATURE__.DTOs;
                using Truss.Application;

                public interface I__TYPE__Repository
                {
                    void Add(__TYPE__ __CAMEL__);

                    Task<__TYPE__?> GetById(__TYPE__Id id, CancellationToken cancellationToken = default);

                    void Remove(__TYPE__ __CAMEL__);

                    Task<PageResult<__TYPE__Dto>> List(PageRequest page, CancellationToken cancellationToken = default);
                }
            }
            """;

        public const string CrudCreate = """
            namespace __NS_FEATURE__.Create__TYPE__
            {
                using Truss.Application;

                public sealed record Create__TYPE__(string Name) : ICommand<Guid>;
            }
            """;

        public const string CrudCreateHandler = """
            namespace __NS_FEATURE__.Create__TYPE__
            {
                using __NS_AGG__;
                using __NS_FEATURE__;
                using Truss.Application;

                public class Create__TYPE__Handler(I__TYPE__Repository repository) : ICommandHandler<Create__TYPE__, Guid>
                {
                    public Task<Guid> Handle(Create__TYPE__ command, CancellationToken cancellationToken)
                    {
                        var __CAMEL__ = __TYPE__.Create(command.Name);
                        repository.Add(__CAMEL__);
                        return Task.FromResult(__CAMEL__.Id.Value);
                    }
                }
            }
            """;

        public const string CrudCreateValidator = """
            namespace __NS_FEATURE__.Create__TYPE__
            {
                using FluentValidation;

                public class Create__TYPE__Validator : AbstractValidator<Create__TYPE__>
                {
                    public Create__TYPE__Validator()
                    {
                        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
                    }
                }
            }
            """;

        public const string CrudUpdate = """
            namespace __NS_FEATURE__.Update__TYPE__
            {
                using Truss.Application;

                public sealed record Update__TYPE__(Guid Id, string Name) : ICommand;
            }
            """;

        public const string CrudUpdateHandler = """
            namespace __NS_FEATURE__.Update__TYPE__
            {
                using __NS_AGG__.ValueObjects;
                using __NS_FEATURE__;
                using __NS_FEATURE__.Rules;
                using Truss.Application;
                using Truss.Domain;

                public class Update__TYPE__Handler(I__TYPE__Repository repository) : ICommandHandler<Update__TYPE__>
                {
                    public async Task<Unit> Handle(Update__TYPE__ command, CancellationToken cancellationToken)
                    {
                        var __CAMEL__ = await repository.GetById(new __TYPE__Id(command.Id), cancellationToken);

                        if (__CAMEL__ is null)
                            throw new BusinessRuleValidationException(new __TYPE__MustExist());

                        __CAMEL__.Rename(command.Name);

                        return Unit.Value;
                    }
                }
            }
            """;

        public const string CrudUpdateValidator = """
            namespace __NS_FEATURE__.Update__TYPE__
            {
                using FluentValidation;

                public class Update__TYPE__Validator : AbstractValidator<Update__TYPE__>
                {
                    public Update__TYPE__Validator()
                    {
                        RuleFor(command => command.Id).NotEmpty();
                        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
                    }
                }
            }
            """;

        public const string CrudDelete = """
            namespace __NS_FEATURE__.Delete__TYPE__
            {
                using Truss.Application;

                public sealed record Delete__TYPE__(Guid Id) : ICommand;
            }
            """;

        public const string CrudDeleteHandler = """
            namespace __NS_FEATURE__.Delete__TYPE__
            {
                using __NS_AGG__.ValueObjects;
                using __NS_FEATURE__;
                using __NS_FEATURE__.Rules;
                using Truss.Application;
                using Truss.Domain;

                public class Delete__TYPE__Handler(I__TYPE__Repository repository) : ICommandHandler<Delete__TYPE__>
                {
                    public async Task<Unit> Handle(Delete__TYPE__ command, CancellationToken cancellationToken)
                    {
                        var __CAMEL__ = await repository.GetById(new __TYPE__Id(command.Id), cancellationToken);

                        if (__CAMEL__ is null)
                            throw new BusinessRuleValidationException(new __TYPE__MustExist());

                        repository.Remove(__CAMEL__);

                        return Unit.Value;
                    }
                }
            }
            """;

        public const string CrudMustExist = """
            namespace __NS_FEATURE__.Rules
            {
                using Truss.Domain;

                public class __TYPE__MustExist : IBusinessRule
                {
                    public bool IsBroken() => true;

                    public string Message => "The __TYPE__ does not exist.";

                    public string Code => "__CAMEL__.not-found";
                }
            }
            """;

        public const string CrudGetById = """
            namespace __NS_FEATURE__.Get__TYPE__ById
            {
                using __NS_FEATURE__.DTOs;
                using Truss.Application;

                public sealed record Get__TYPE__ById(Guid Id) : IQuery<__TYPE__Dto?>;
            }
            """;

        public const string CrudGetByIdHandler = """
            namespace __NS_FEATURE__.Get__TYPE__ById
            {
                using __NS_AGG__.ValueObjects;
                using __NS_FEATURE__;
                using __NS_FEATURE__.DTOs;
                using Truss.Application;

                public class Get__TYPE__ByIdHandler(I__TYPE__Repository repository) : IQueryHandler<Get__TYPE__ById, __TYPE__Dto?>
                {
                    public async Task<__TYPE__Dto?> Handle(Get__TYPE__ById query, CancellationToken cancellationToken)
                    {
                        var __CAMEL__ = await repository.GetById(new __TYPE__Id(query.Id), cancellationToken);

                        return __CAMEL__ is null ? null : new __TYPE__Dto(__CAMEL__.Id.Value, __CAMEL__.Name);
                    }
                }
            }
            """;

        public const string CrudList = """
            namespace __NS_FEATURE__.List__TYPE__
            {
                using __NS_FEATURE__.DTOs;
                using Truss.Application;

                public sealed record List__TYPE__(int Page = 1, int Size = 20) : IQuery<PageResult<__TYPE__Dto>>;
            }
            """;

        public const string CrudListHandler = """
            namespace __NS_FEATURE__.List__TYPE__
            {
                using __NS_FEATURE__;
                using __NS_FEATURE__.DTOs;
                using Truss.Application;

                public class List__TYPE__Handler(I__TYPE__Repository repository) : IQueryHandler<List__TYPE__, PageResult<__TYPE__Dto>>
                {
                    public Task<PageResult<__TYPE__Dto>> Handle(List__TYPE__ query, CancellationToken cancellationToken)
                    {
                        return repository.List(new PageRequest(query.Page, query.Size), cancellationToken);
                    }
                }
            }
            """;

        public const string CrudListValidator = """
            namespace __NS_FEATURE__.List__TYPE__
            {
                using FluentValidation;

                public class List__TYPE__Validator : AbstractValidator<List__TYPE__>
                {
                    public List__TYPE__Validator()
                    {
                        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
                        RuleFor(query => query.Size).InclusiveBetween(1, 100);
                    }
                }
            }
            """;

        public const string CrudConfiguration = """
            using __NS_AGG__;
            using __NS_AGG__.ValueObjects;
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;

            namespace __NS_INFRASTRUCTURE__
            {
                public class __TYPE__Configuration : IEntityTypeConfiguration<__TYPE__>
                {
                    public void Configure(EntityTypeBuilder<__TYPE__> builder)
                    {
                        builder.ToTable("__TYPE__s");
                        builder.HasKey(__CAMEL__ => __CAMEL__.Id);

                        builder.Property(__CAMEL__ => __CAMEL__.Id)
                            .HasConversion(id => id.Value, value => new __TYPE__Id(value));

                        builder.Property(__CAMEL__ => __CAMEL__.Name).HasMaxLength(200).IsRequired();
                    }
                }
            }
            """;

        public const string CrudEfRepository = """
            using __NS_AGG__;
            using __NS_AGG__.ValueObjects;
            using __NS_FEATURE__;
            using __NS_FEATURE__.DTOs;
            using Microsoft.EntityFrameworkCore;
            using Truss.Application;

            namespace __NS_INFRASTRUCTURE__
            {
                public class Ef__TYPE__Repository(DbContext context) : I__TYPE__Repository
                {
                    public void Add(__TYPE__ __CAMEL__)
                    {
                        context.Set<__TYPE__>().Add(__CAMEL__);
                    }

                    public Task<__TYPE__?> GetById(__TYPE__Id id, CancellationToken cancellationToken = default)
                    {
                        return context.Set<__TYPE__>().FirstOrDefaultAsync(__CAMEL__ => __CAMEL__.Id == id, cancellationToken);
                    }

                    public void Remove(__TYPE__ __CAMEL__)
                    {
                        context.Set<__TYPE__>().Remove(__CAMEL__);
                    }

                    public Task<PageResult<__TYPE__Dto>> List(PageRequest page, CancellationToken cancellationToken = default)
                    {
                        return context.Set<__TYPE__>()
                            .OrderBy(__CAMEL__ => __CAMEL__.Name)
                            .Select(__CAMEL__ => new __TYPE__Dto(__CAMEL__.Id.Value, __CAMEL__.Name))
                            .ToPageAsync(page, cancellationToken);
                    }
                }
            }
            """;
    }
}
