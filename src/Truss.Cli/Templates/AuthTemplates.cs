namespace Truss.Cli.Templates
{
    /// <summary>
    /// The Accounts context scaffolded by truss add auth. It follows the same
    /// layout as the generators: the User aggregate owns a folder with its value
    /// objects, events and rules, each command owns a folder with its handler and
    /// validator, and DTOs and rules of the application layer own theirs.
    /// The namespaces of the account aggregate are placeholders so a bound
    /// aggregate can take its place.
    /// </summary>
    internal static class AuthTemplates
    {
        public const string UserId = """
            using Truss.Domain;

            namespace __NAME__.Domain.Accounts.User.ValueObjects
            {
                public sealed record UserId(Guid Value) : TypedId<Guid>(Value);
            }
            """;

        public const string UserRegistered = """
            using __NAME__.Domain.Accounts.User.ValueObjects;
            using Truss.Domain;

            namespace __NAME__.Domain.Accounts.User.Events
            {
                public sealed record UserRegistered(UserId UserId) : DomainEvent;
            }
            """;

        public const string User = """
            using __NAME__.Domain.Accounts.User.Events;
            using __NAME__.Domain.Accounts.User.ValueObjects;
            using Truss.Domain;

            namespace __NAME__.Domain.Accounts.User
            {
                public class User : AggregateRoot<UserId>
                {
                    private User()
                    {
                    }

                    private User(UserId id, string email, string name) : base(id)
                    {
                        Email = email;
                        Name = name;
                    }

                    public string Email { get; private set; } = string.Empty;

                    public string Name { get; private set; } = string.Empty;

                    public static User Register(string email, string name)
                    {
                        var user = new User(new UserId(Guid.NewGuid()), email.ToLowerInvariant(), name);
                        user.AddDomainEvent(new UserRegistered(user.Id));
                        return user;
                    }
                }
            }
            """;

        public const string EmailMustBeUnique = """
            using Truss.Domain;

            namespace __NS_USER_RULES__
            {
                public class EmailMustBeUnique(bool alreadyInUse) : IBusinessRule
                {
                    public bool IsBroken() => alreadyInUse;

                    public string Message => "This email is already registered.";
                }
            }
            """;

        public const string InvalidCredentials = """
            namespace __NAME__.Application.Accounts.Rules
            {
                using Truss.Domain;

                public class InvalidCredentials : IBusinessRule
                {
                    public bool IsBroken() => true;

                    public string Message => "Invalid email or password.";
                }
            }
            """;

        public const string UserRepository = """
            namespace __NAME__.Application.Accounts
            {
                using __NS_USER__;
                using __NS_USER_ID__;

                public interface IUserRepository
                {
                    void Add(User user);

                    Task<User?> GetByEmail(string email, CancellationToken cancellationToken = default);

                    Task<User?> GetById(UserId id, CancellationToken cancellationToken = default);
                }
            }
            """;

        public const string UserCredentialsStore = """
            namespace __NAME__.Application.Accounts
            {
                using __NS_USER_ID__;

                public interface IUserCredentialsStore
                {
                    Task SetPassword(UserId userId, string password, CancellationToken cancellationToken = default);

                    Task<bool> VerifyPassword(UserId userId, string password, CancellationToken cancellationToken = default);
                }
            }
            """;

        public const string RefreshTokenStore = """
            namespace __NAME__.Application.Accounts
            {
                using __NS_USER_ID__;

                public sealed record ActiveRefreshToken(Guid Id, UserId UserId);

                public interface IRefreshTokenStore
                {
                    void Add(UserId userId, string tokenHash, DateTimeOffset expiresOn);

                    Task<ActiveRefreshToken?> FindActive(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken = default);

                    Task Revoke(Guid refreshTokenId, DateTimeOffset now, CancellationToken cancellationToken = default);
                }
            }
            """;

        public const string AuthTokensDto = """
            namespace __NAME__.Application.Accounts.DTOs
            {
                public sealed record AuthTokensDto(string AccessToken, string RefreshToken);
            }
            """;

        public const string RegisterUser = """
            namespace __NAME__.Application.Accounts.RegisterUser
            {
                using Truss.Application;

                public sealed record RegisterUser(string Email, string Name, string Password) : ICommand<Guid>;
            }
            """;

        public const string RegisterUserHandler = """
            namespace __NAME__.Application.Accounts.RegisterUser
            {
                using __NAME__.Application.Accounts;
                using __NS_USER__;
                using __NS_USER_RULES__;__BIND_USING__
                using Truss.Application;
                using Truss.Domain;

                public class RegisterUserHandler(
                    IUserRepository users,
                    IUserCredentialsStore credentials) : ICommandHandler<RegisterUser, Guid>
                {
                    public async Task<Guid> Handle(RegisterUser command, CancellationToken cancellationToken)
                    {
                        var existing = await users.GetByEmail(command.Email.ToLowerInvariant(), cancellationToken);

                        var rule = new EmailMustBeUnique(existing is not null);

                        if (rule.IsBroken())
                            throw new BusinessRuleValidationException(rule);

                        var user = User.Register(command.Email, command.Name);
                        users.Add(user);

                        await credentials.SetPassword(user.Id, command.Password, cancellationToken);

                        return user.Id.Value;
                    }
                }
            }
            """;

        public const string RegisterUserValidator = """
            namespace __NAME__.Application.Accounts.RegisterUser
            {
                using FluentValidation;

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
            namespace __NAME__.Application.Accounts.Login
            {
                using __NAME__.Application.Accounts.DTOs;
                using Truss.Application;

                public sealed record Login(string Email, string Password) : ICommand<AuthTokensDto>;
            }
            """;

        public const string LoginHandler = """
            namespace __NAME__.Application.Accounts.Login
            {
                using System.Security.Claims;
                using __NAME__.Application.Accounts;
                using __NAME__.Application.Accounts.DTOs;
                using __NAME__.Application.Accounts.Rules;
                using Truss.Application;
                using Truss.Auth;
                using Truss.Domain;

                public class LoginHandler(
                    IUserRepository users,
                    IUserCredentialsStore credentials,
                    IRefreshTokenStore refreshTokens,
                    IJwtTokenService tokens) : ICommandHandler<Login, AuthTokensDto>
                {
                    public async Task<AuthTokensDto> Handle(Login command, CancellationToken cancellationToken)
                    {
                        var user = await users.GetByEmail(command.Email.ToLowerInvariant(), cancellationToken);

                        var validPassword = user is not null
                            && await credentials.VerifyPassword(user.Id, command.Password, cancellationToken);

                        if (user is null || !validPassword)
                            throw new BusinessRuleValidationException(new InvalidCredentials());

                        var refresh = tokens.CreateRefreshToken();
                        refreshTokens.Add(user.Id, refresh.TokenHash, refresh.ExpiresOn);

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
            namespace __NAME__.Application.Accounts.Login
            {
                using FluentValidation;

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
            namespace __NAME__.Application.Accounts.Refresh
            {
                using __NAME__.Application.Accounts.DTOs;
                using Truss.Application;

                public sealed record Refresh(string RefreshToken) : ICommand<AuthTokensDto>;
            }
            """;

        public const string RefreshHandler = """
            namespace __NAME__.Application.Accounts.Refresh
            {
                using System.Security.Claims;
                using __NAME__.Application.Accounts;
                using __NAME__.Application.Accounts.DTOs;
                using __NAME__.Application.Accounts.Rules;
                using Truss.Application;
                using Truss.Auth;
                using Truss.Domain;

                public class RefreshHandler(
                    IUserRepository users,
                    IRefreshTokenStore refreshTokens,
                    IJwtTokenService tokens,
                    TimeProvider timeProvider) : ICommandHandler<Refresh, AuthTokensDto>
                {
                    public async Task<AuthTokensDto> Handle(Refresh command, CancellationToken cancellationToken)
                    {
                        var now = timeProvider.GetUtcNow();
                        var active = await refreshTokens.FindActive(tokens.HashRefreshToken(command.RefreshToken), now, cancellationToken);

                        if (active is null)
                            throw new BusinessRuleValidationException(new InvalidCredentials());

                        var user = await users.GetById(active.UserId, cancellationToken)
                            ?? throw new BusinessRuleValidationException(new InvalidCredentials());

                        await refreshTokens.Revoke(active.Id, now, cancellationToken);

                        var refresh = tokens.CreateRefreshToken();
                        refreshTokens.Add(user.Id, refresh.TokenHash, refresh.ExpiresOn);

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
            namespace __NAME__.Application.Accounts.Refresh
            {
                using FluentValidation;

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
            using __NS_USER__;
            using __NS_USER_ID__;__BIND_USING_TOP__
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

                        builder.HasIndex(user => user.Email).IsUnique();
                    }
                }
            }
            """;

        public const string UserCredential = """
            namespace __NAME__.Infrastructure.Accounts
            {
                public class UserCredential
                {
                    private UserCredential()
                    {
                    }

                    public UserCredential(Guid userId, string passwordHash)
                    {
                        UserId = userId;
                        PasswordHash = passwordHash;
                    }

                    public Guid UserId { get; private set; }

                    public string PasswordHash { get; private set; } = string.Empty;

                    public bool EmailConfirmed { get; private set; }

                    public bool TwoFactorEnabled { get; private set; }

                    public void ChangePassword(string passwordHash)
                    {
                        PasswordHash = passwordHash;
                    }

                    public void ConfirmEmail()
                    {
                        EmailConfirmed = true;
                    }

                    public void SetTwoFactorEnabled(bool enabled)
                    {
                        TwoFactorEnabled = enabled;
                    }
                }
            }
            """;

        public const string UserCredentialConfiguration = """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;

            namespace __NAME__.Infrastructure.Accounts
            {
                public class UserCredentialConfiguration : IEntityTypeConfiguration<UserCredential>
                {
                    public void Configure(EntityTypeBuilder<UserCredential> builder)
                    {
                        builder.ToTable("UserCredentials");
                        builder.HasKey(credential => credential.UserId);

                        builder.Property(credential => credential.PasswordHash).HasMaxLength(512).IsRequired();
                    }
                }
            }
            """;

        public const string RefreshTokenRecord = """
            namespace __NAME__.Infrastructure.Accounts
            {
                public class RefreshTokenRecord
                {
                    private RefreshTokenRecord()
                    {
                    }

                    public RefreshTokenRecord(Guid id, Guid userId, string tokenHash, DateTimeOffset expiresOn)
                    {
                        Id = id;
                        UserId = userId;
                        TokenHash = tokenHash;
                        ExpiresOn = expiresOn;
                    }

                    public Guid Id { get; private set; }

                    public Guid UserId { get; private set; }

                    public string TokenHash { get; private set; } = string.Empty;

                    public DateTimeOffset ExpiresOn { get; private set; }

                    public DateTimeOffset? RevokedOn { get; private set; }

                    public bool IsActive(DateTimeOffset now) => RevokedOn is null && now < ExpiresOn;

                    public void Revoke(DateTimeOffset now) => RevokedOn = now;
                }
            }
            """;

        public const string RefreshTokenConfiguration = """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;

            namespace __NAME__.Infrastructure.Accounts
            {
                public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshTokenRecord>
                {
                    public void Configure(EntityTypeBuilder<RefreshTokenRecord> builder)
                    {
                        builder.ToTable("RefreshTokens");
                        builder.HasKey(token => token.Id);

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
            using __NS_USER__;
            using __NS_USER_ID__;
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

        public const string EfUserCredentialsStore = """
            using __NAME__.Application.Accounts;
            using __NS_USER_ID__;
            using Truss.Auth;

            namespace __NAME__.Infrastructure.Accounts
            {
                public class EfUserCredentialsStore(AppDbContext context, IPasswordHasher passwordHasher) : IUserCredentialsStore
                {
                    public async Task SetPassword(UserId userId, string password, CancellationToken cancellationToken = default)
                    {
                        var passwordHash = passwordHasher.Hash(password);
                        var existing = await context.Set<UserCredential>().FindAsync([userId.Value], cancellationToken);

                        if (existing is null)
                            context.Set<UserCredential>().Add(new UserCredential(userId.Value, passwordHash));
                        else
                            existing.ChangePassword(passwordHash);
                    }

                    public async Task<bool> VerifyPassword(UserId userId, string password, CancellationToken cancellationToken = default)
                    {
                        var credential = await context.Set<UserCredential>().FindAsync([userId.Value], cancellationToken);
                        return credential is not null && passwordHasher.Verify(password, credential.PasswordHash);
                    }
                }
            }
            """;

        public const string EfRefreshTokenStore = """
            using __NAME__.Application.Accounts;
            using __NS_USER_ID__;
            using Microsoft.EntityFrameworkCore;

            namespace __NAME__.Infrastructure.Accounts
            {
                public class EfRefreshTokenStore(AppDbContext context) : IRefreshTokenStore
                {
                    public void Add(UserId userId, string tokenHash, DateTimeOffset expiresOn)
                    {
                        context.Set<RefreshTokenRecord>().Add(
                            new RefreshTokenRecord(Guid.NewGuid(), userId.Value, tokenHash, expiresOn));
                    }

                    public async Task<ActiveRefreshToken?> FindActive(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken = default)
                    {
                        var record = await context.Set<RefreshTokenRecord>()
                            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

                        return record is not null && record.IsActive(now)
                            ? new ActiveRefreshToken(record.Id, new UserId(record.UserId))
                            : null;
                    }

                    public async Task Revoke(Guid refreshTokenId, DateTimeOffset now, CancellationToken cancellationToken = default)
                    {
                        var record = await context.Set<RefreshTokenRecord>().FindAsync([refreshTokenId], cancellationToken);
                        record?.Revoke(now);
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
                        services.AddScoped<IUserCredentialsStore, EfUserCredentialsStore>();
                        services.AddScoped<IRefreshTokenStore, EfRefreshTokenStore>();__FLOW_REGISTRATIONS__
                        return services;
                    }
                }
            }
            """;

        public const string ApplicationUser = """
            using Microsoft.AspNetCore.Identity;

            namespace __NAME__.Infrastructure.Accounts
            {
                public class ApplicationUser : IdentityUser<Guid>
                {
                }
            }
            """;

        public const string IdentityModelConfiguration = """
            using Microsoft.AspNetCore.Identity;
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;

            namespace __NAME__.Infrastructure.Accounts
            {
                public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
                {
                    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
                    {
                        builder.ToTable("AspNetUsers");
                        builder.HasKey(user => user.Id);

                        builder.HasIndex(user => user.NormalizedUserName).IsUnique();
                        builder.HasIndex(user => user.NormalizedEmail);

                        builder.Property(user => user.UserName).HasMaxLength(256);
                        builder.Property(user => user.NormalizedUserName).HasMaxLength(256);
                        builder.Property(user => user.Email).HasMaxLength(256);
                        builder.Property(user => user.NormalizedEmail).HasMaxLength(256);
                        builder.Property(user => user.ConcurrencyStamp).IsConcurrencyToken();
                    }
                }

                public class IdentityUserClaimConfiguration : IEntityTypeConfiguration<IdentityUserClaim<Guid>>
                {
                    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder)
                    {
                        builder.ToTable("AspNetUserClaims");
                        builder.HasKey(claim => claim.Id);
                    }
                }

                public class IdentityUserLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
                {
                    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder)
                    {
                        builder.ToTable("AspNetUserLogins");
                        builder.HasKey(login => new { login.LoginProvider, login.ProviderKey });
                    }
                }

                public class IdentityUserTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<Guid>>
                {
                    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder)
                    {
                        builder.ToTable("AspNetUserTokens");
                        builder.HasKey(token => new { token.UserId, token.LoginProvider, token.Name });
                    }
                }
            }
            """;

        public const string IdentityUserCredentialsStore = """
            using __NAME__.Application.Accounts;
            using __NS_USER_ID__;
            using Microsoft.AspNetCore.Identity;

            namespace __NAME__.Infrastructure.Accounts
            {
                public class IdentityUserCredentialsStore(UserManager<ApplicationUser> userManager) : IUserCredentialsStore
                {
                    public async Task SetPassword(UserId userId, string password, CancellationToken cancellationToken = default)
                    {
                        var user = await userManager.FindByIdAsync(userId.Value.ToString());

                        if (user is null)
                        {
                            user = new ApplicationUser { Id = userId.Value, UserName = userId.Value.ToString() };
                            EnsureSucceeded(await userManager.CreateAsync(user, password));
                            return;
                        }

                        EnsureSucceeded(await userManager.RemovePasswordAsync(user));
                        EnsureSucceeded(await userManager.AddPasswordAsync(user, password));
                    }

                    public async Task<bool> VerifyPassword(UserId userId, string password, CancellationToken cancellationToken = default)
                    {
                        var user = await userManager.FindByIdAsync(userId.Value.ToString());
                        return user is not null && await userManager.CheckPasswordAsync(user, password);
                    }

                    private static void EnsureSucceeded(IdentityResult result)
                    {
                        if (!result.Succeeded)
                            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
                    }
                }
            }
            """;

        public const string AccountsModuleIdentity = """
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
                        services.AddScoped<IUserCredentialsStore, IdentityUserCredentialsStore>();
                        services.AddScoped<IRefreshTokenStore, EfRefreshTokenStore>();__FLOW_REGISTRATIONS__

                        services.AddIdentityCore<ApplicationUser>(options =>
                        {
                            options.Password.RequiredLength = 8;
                            options.Password.RequireDigit = false;
                            options.Password.RequireLowercase = false;
                            options.Password.RequireUppercase = false;
                            options.Password.RequireNonAlphanumeric = false;
                        })
                        .AddEntityFrameworkStores<AppDbContext>();

                        return services;
                    }
                }
            }
            """;

        public const string ProgramUsings = """
            using __NAME__.Application.Accounts.DTOs;
            using __NAME__.Application.Accounts.Login;
            using __NAME__.Application.Accounts.Refresh;
            using __NAME__.Application.Accounts.RegisterUser;
            """;

        public const string ProgramUsingsWithFlows = """
            using __NAME__.Application.Accounts.ConfirmEmail;
            using __NAME__.Application.Accounts.DTOs;
            using __NAME__.Application.Accounts.Login;
            using __NAME__.Application.Accounts.Refresh;
            using __NAME__.Application.Accounts.RegisterUser;
            using __NAME__.Application.Accounts.RequestPasswordReset;
            using __NAME__.Application.Accounts.ResetPassword;
            using __NAME__.Application.Accounts.VerifyTwoFactor;
            """;

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
