namespace Truss.Cli.Templates
{
    internal static class AuthTemplates
    {
        public const string UserId = """
            using Truss.Domain;

            namespace __NAME__.Domain.Accounts
            {
                public sealed record UserId(Guid Value) : TypedId<Guid>(Value);
            }
            """;

        public const string UserRegistered = """
            using Truss.Domain;

            namespace __NAME__.Domain.Accounts
            {
                public sealed record UserRegistered(UserId UserId) : DomainEvent;
            }
            """;

        public const string User = """
            using Truss.Domain;

            namespace __NAME__.Domain.Accounts
            {
                public class User : AggregateRoot<UserId>
                {
                    private User()
                    {
                    }

                    private User(UserId id, string email, string name, string passwordHash) : base(id)
                    {
                        Email = email;
                        Name = name;
                        PasswordHash = passwordHash;
                    }

                    public string Email { get; private set; } = string.Empty;

                    public string Name { get; private set; } = string.Empty;

                    public string PasswordHash { get; private set; } = string.Empty;

                    public static User Register(string email, string name, string passwordHash)
                    {
                        var user = new User(new UserId(Guid.NewGuid()), email.ToLowerInvariant(), name, passwordHash);
                        user.AddDomainEvent(new UserRegistered(user.Id));
                        return user;
                    }
                }
            }
            """;

        public const string RefreshTokenEntity = """
            using Truss.Domain;

            namespace __NAME__.Domain.Accounts
            {
                public class RefreshToken : Entity<Guid>
                {
                    private RefreshToken()
                    {
                    }

                    private RefreshToken(Guid id, UserId userId, string tokenHash, DateTimeOffset expiresOn) : base(id)
                    {
                        UserId = userId;
                        TokenHash = tokenHash;
                        ExpiresOn = expiresOn;
                    }

                    public UserId UserId { get; private set; } = null!;

                    public string TokenHash { get; private set; } = string.Empty;

                    public DateTimeOffset ExpiresOn { get; private set; }

                    public DateTimeOffset? RevokedOn { get; private set; }

                    public static RefreshToken Issue(UserId userId, string tokenHash, DateTimeOffset expiresOn)
                    {
                        return new RefreshToken(Guid.NewGuid(), userId, tokenHash, expiresOn);
                    }

                    public bool IsActive(DateTimeOffset now) => RevokedOn is null && now < ExpiresOn;

                    public void Revoke(DateTimeOffset now) => RevokedOn = now;
                }
            }
            """;

        public const string InvalidCredentials = """
            using Truss.Domain;

            namespace __NAME__.Domain.Accounts
            {
                public class InvalidCredentials : IBusinessRule
                {
                    public bool IsBroken() => true;

                    public string Message => "Invalid email or password.";
                }
            }
            """;

        public const string EmailMustBeUnique = """
            using Truss.Domain;

            namespace __NAME__.Domain.Accounts
            {
                public class EmailMustBeUnique(bool alreadyInUse) : IBusinessRule
                {
                    public bool IsBroken() => alreadyInUse;

                    public string Message => "This email is already registered.";
                }
            }
            """;

        public const string UserRepository = """
            using __NAME__.Domain.Accounts;

            namespace __NAME__.Application.Accounts
            {
                public interface IUserRepository
                {
                    void Add(User user);

                    Task<User?> GetByEmail(string email, CancellationToken cancellationToken = default);

                    Task<User?> GetById(UserId id, CancellationToken cancellationToken = default);
                }
            }
            """;

        public const string RefreshTokenRepository = """
            using __NAME__.Domain.Accounts;

            namespace __NAME__.Application.Accounts
            {
                public interface IRefreshTokenRepository
                {
                    void Add(RefreshToken refreshToken);

                    Task<RefreshToken?> GetByHash(string tokenHash, CancellationToken cancellationToken = default);
                }
            }
            """;

        public const string AuthTokensDto = """
            namespace __NAME__.Application.Accounts
            {
                public sealed record AuthTokensDto(string AccessToken, string RefreshToken);
            }
            """;

        public const string RegisterUser = """
            using Truss.Application;

            namespace __NAME__.Application.Accounts
            {
                public sealed record RegisterUser(string Email, string Name, string Password) : ICommand<Guid>;
            }
            """;

        public const string RegisterUserHandler = """
            using __NAME__.Domain.Accounts;
            using Truss.Application;
            using Truss.Auth;
            using Truss.Domain;

            namespace __NAME__.Application.Accounts
            {
                public class RegisterUserHandler(IUserRepository users, IPasswordHasher passwordHasher)
                    : ICommandHandler<RegisterUser, Guid>
                {
                    public async Task<Guid> Handle(RegisterUser command, CancellationToken cancellationToken)
                    {
                        var existing = await users.GetByEmail(command.Email.ToLowerInvariant(), cancellationToken);

                        var rule = new EmailMustBeUnique(existing is not null);

                        if (rule.IsBroken())
                            throw new BusinessRuleValidationException(rule);

                        var user = User.Register(command.Email, command.Name, passwordHasher.Hash(command.Password));
                        users.Add(user);

                        return user.Id.Value;
                    }
                }
            }
            """;

        public const string RegisterUserValidator = """
            using FluentValidation;

            namespace __NAME__.Application.Accounts
            {
                public class RegisterUserValidator : AbstractValidator<RegisterUser>
                {
                    public RegisterUserValidator()
                    {
                        RuleFor(command => command.Email).NotEmpty().EmailAddress();
                        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
                        RuleFor(command => command.Password).NotEmpty().MinimumLength(8);
                    }
                }
            }
            """;

        public const string Login = """
            using Truss.Application;

            namespace __NAME__.Application.Accounts
            {
                public sealed record Login(string Email, string Password) : ICommand<AuthTokensDto>;
            }
            """;

        public const string LoginHandler = """
            using System.Security.Claims;
            using __NAME__.Domain.Accounts;
            using Truss.Application;
            using Truss.Auth;
            using Truss.Domain;

            namespace __NAME__.Application.Accounts
            {
                public class LoginHandler(
                    IUserRepository users,
                    IRefreshTokenRepository refreshTokens,
                    IPasswordHasher passwordHasher,
                    IJwtTokenService tokens) : ICommandHandler<Login, AuthTokensDto>
                {
                    public async Task<AuthTokensDto> Handle(Login command, CancellationToken cancellationToken)
                    {
                        var user = await users.GetByEmail(command.Email.ToLowerInvariant(), cancellationToken);

                        if (user is null || !passwordHasher.Verify(command.Password, user.PasswordHash))
                            throw new BusinessRuleValidationException(new InvalidCredentials());

                        var refresh = tokens.CreateRefreshToken();
                        refreshTokens.Add(RefreshToken.Issue(user.Id, refresh.TokenHash, refresh.ExpiresOn));

                        var access = tokens.CreateAccessToken(
                        [
                            new Claim("sub", user.Id.Value.ToString()),
                            new Claim("email", user.Email),
                            new Claim("name", user.Name)
                        ]);

                        return new AuthTokensDto(access, refresh.Token);
                    }
                }
            }
            """;

        public const string LoginValidator = """
            using FluentValidation;

            namespace __NAME__.Application.Accounts
            {
                public class LoginValidator : AbstractValidator<Login>
                {
                    public LoginValidator()
                    {
                        RuleFor(command => command.Email).NotEmpty();
                        RuleFor(command => command.Password).NotEmpty();
                    }
                }
            }
            """;

        public const string Refresh = """
            using Truss.Application;

            namespace __NAME__.Application.Accounts
            {
                public sealed record Refresh(string RefreshToken) : ICommand<AuthTokensDto>;
            }
            """;

        public const string RefreshHandler = """
            using System.Security.Claims;
            using __NAME__.Domain.Accounts;
            using Truss.Application;
            using Truss.Auth;
            using Truss.Domain;

            namespace __NAME__.Application.Accounts
            {
                public class RefreshHandler(
                    IUserRepository users,
                    IRefreshTokenRepository refreshTokens,
                    IJwtTokenService tokens,
                    TimeProvider timeProvider) : ICommandHandler<Refresh, AuthTokensDto>
                {
                    public async Task<AuthTokensDto> Handle(Refresh command, CancellationToken cancellationToken)
                    {
                        var now = timeProvider.GetUtcNow();
                        var stored = await refreshTokens.GetByHash(tokens.HashRefreshToken(command.RefreshToken), cancellationToken);

                        if (stored is null || !stored.IsActive(now))
                            throw new BusinessRuleValidationException(new InvalidCredentials());

                        var user = await users.GetById(stored.UserId, cancellationToken)
                            ?? throw new BusinessRuleValidationException(new InvalidCredentials());

                        stored.Revoke(now);

                        var refresh = tokens.CreateRefreshToken();
                        refreshTokens.Add(RefreshToken.Issue(user.Id, refresh.TokenHash, refresh.ExpiresOn));

                        var access = tokens.CreateAccessToken(
                        [
                            new Claim("sub", user.Id.Value.ToString()),
                            new Claim("email", user.Email),
                            new Claim("name", user.Name)
                        ]);

                        return new AuthTokensDto(access, refresh.Token);
                    }
                }
            }
            """;

        public const string RefreshValidator = """
            using FluentValidation;

            namespace __NAME__.Application.Accounts
            {
                public class RefreshValidator : AbstractValidator<Refresh>
                {
                    public RefreshValidator()
                    {
                        RuleFor(command => command.RefreshToken).NotEmpty();
                    }
                }
            }
            """;

        public const string UserConfiguration = """
            using __NAME__.Domain.Accounts;
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;

            namespace __NAME__.Infrastructure.Accounts
            {
                public class UserConfiguration : IEntityTypeConfiguration<User>
                {
                    public void Configure(EntityTypeBuilder<User> builder)
                    {
                        builder.ToTable("Users");
                        builder.HasKey(user => user.Id);

                        builder.Property(user => user.Id)
                            .HasConversion(id => id.Value, value => new UserId(value));

                        builder.Property(user => user.Email).HasMaxLength(320).IsRequired();
                        builder.Property(user => user.Name).HasMaxLength(200).IsRequired();
                        builder.Property(user => user.PasswordHash).HasMaxLength(512).IsRequired();

                        builder.HasIndex(user => user.Email).IsUnique();
                    }
                }
            }
            """;

        public const string RefreshTokenConfiguration = """
            using __NAME__.Domain.Accounts;
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;

            namespace __NAME__.Infrastructure.Accounts
            {
                public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
                {
                    public void Configure(EntityTypeBuilder<RefreshToken> builder)
                    {
                        builder.ToTable("RefreshTokens");
                        builder.HasKey(token => token.Id);

                        builder.Property(token => token.UserId)
                            .HasConversion(id => id.Value, value => new UserId(value));

                        builder.Property(token => token.TokenHash).HasMaxLength(128).IsRequired();

                        builder.Property(token => token.ExpiresOn)
                            .HasConversion(value => value.UtcTicks, value => new DateTimeOffset(value, TimeSpan.Zero));

                        builder.Property(token => token.RevokedOn)
                            .HasConversion(
                                value => value.HasValue ? value.Value.UtcTicks : (long?)null,
                                value => value.HasValue ? new DateTimeOffset(value.Value, TimeSpan.Zero) : null);

                        builder.HasIndex(token => token.TokenHash).IsUnique();
                    }
                }
            }
            """;

        public const string EfUserRepository = """
            using __NAME__.Application.Accounts;
            using __NAME__.Domain.Accounts;
            using Microsoft.EntityFrameworkCore;

            namespace __NAME__.Infrastructure.Accounts
            {
                public class EfUserRepository(AppDbContext context) : IUserRepository
                {
                    public void Add(User user)
                    {
                        context.Set<User>().Add(user);
                    }

                    public Task<User?> GetByEmail(string email, CancellationToken cancellationToken = default)
                    {
                        return context.Set<User>().FirstOrDefaultAsync(user => user.Email == email, cancellationToken);
                    }

                    public Task<User?> GetById(UserId id, CancellationToken cancellationToken = default)
                    {
                        return context.Set<User>().FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
                    }
                }
            }
            """;

        public const string EfRefreshTokenRepository = """
            using __NAME__.Application.Accounts;
            using __NAME__.Domain.Accounts;
            using Microsoft.EntityFrameworkCore;

            namespace __NAME__.Infrastructure.Accounts
            {
                public class EfRefreshTokenRepository(AppDbContext context) : IRefreshTokenRepository
                {
                    public void Add(RefreshToken refreshToken)
                    {
                        context.Set<RefreshToken>().Add(refreshToken);
                    }

                    public Task<RefreshToken?> GetByHash(string tokenHash, CancellationToken cancellationToken = default)
                    {
                        return context.Set<RefreshToken>().FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
                    }
                }
            }
            """;

        public const string AccountsModule = """
            using __NAME__.Application.Accounts;
            using __NAME__.Infrastructure.Accounts;
            using Microsoft.Extensions.DependencyInjection;

            namespace __NAME__.Infrastructure
            {
                public static class AccountsModule
                {
                    public static IServiceCollection AddAccountsInfrastructure(this IServiceCollection services)
                    {
                        services.AddScoped<IUserRepository, EfUserRepository>();
                        services.AddScoped<IRefreshTokenRepository, EfRefreshTokenRepository>();
                        return services;
                    }
                }
            }
            """;

        public const string ProgramUsing = "using __NAME__.Application.Accounts;";

        public const string ProgramServices = """
            builder.Services.AddAccountsInfrastructure();

            builder.Services.AddTrussJwtAuth(options =>
            {
                options.Issuer = builder.Configuration["Truss:Auth:Jwt:Issuer"]!;
                options.Audience = builder.Configuration["Truss:Auth:Jwt:Audience"]!;
                options.SigningKey = builder.Configuration["Truss:Auth:Jwt:SigningKey"]!;
            });
            """;

        public const string ProgramMiddleware = """
            app.UseAuthentication();
            app.UseAuthorization();
            """;

        public const string ProgramEndpoints = """
            app.MapCommand<RegisterUser, Guid>("/auth/register");
            app.MapCommand<Login, AuthTokensDto>("/auth/login");
            app.MapCommand<Refresh, AuthTokensDto>("/auth/refresh");
            """;
    }
}
