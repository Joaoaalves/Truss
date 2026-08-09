namespace Truss.Cli.Templates
{
    /// <summary>
    /// Templates of the account flows scaffolded when the email module is present:
    /// password reset, email confirmation and two factor login by email. They
    /// follow the same layout as the rest of the Accounts context: a folder and a
    /// namespace per command, with DTOs and rules in theirs.
    /// </summary>
    internal static class AuthFlowTemplates
    {
        public const string AccountSecurityStore = """
            namespace __NAME__.Application.Accounts
            {
                using __NS_USER_ID__;

                public sealed record AccountSecurity(bool EmailConfirmed, bool TwoFactorEnabled);

                public interface IAccountSecurityStore
                {
                    Task<AccountSecurity> Get(UserId userId, CancellationToken cancellationToken = default);

                    Task ConfirmEmail(UserId userId, CancellationToken cancellationToken = default);

                    Task SetTwoFactorEnabled(UserId userId, bool enabled, CancellationToken cancellationToken = default);
                }
            }
            """;

        public const string AccountTokens = """
            namespace __NAME__.Application.Accounts
            {
                using __NS_USER_ID__;

                public enum AccountTokenPurpose
                {
                    PasswordReset = 1,
                    EmailConfirmation = 2,
                    TwoFactor = 3
                }

                public interface IAccountTokenStore
                {
                    void Add(UserId userId, string tokenHash, AccountTokenPurpose purpose, DateTimeOffset expiresOn);

                    Task<UserId?> Consume(string tokenHash, AccountTokenPurpose purpose, DateTimeOffset now, CancellationToken cancellationToken = default);
                }
            }
            """;

        public const string InvalidToken = """
            namespace __NAME__.Application.Accounts.Rules
            {
                using Truss.Domain;

                public class InvalidToken : IBusinessRule
                {
                    public bool IsBroken() => true;

                    public string Message => "The token is invalid or has expired.";

                    public string Code => "accounts.invalid-token";
                }
            }
            """;

        public const string LoginResult = """
            namespace __NAME__.Application.Accounts.DTOs
            {
                public sealed record LoginResult(bool RequiresTwoFactor, AuthTokensDto? Tokens);
            }
            """;

        public const string RequestPasswordReset = """
            namespace __NAME__.Application.Accounts.RequestPasswordReset
            {
                using Truss.Application;

                public sealed record RequestPasswordReset(string Email) : ICommand;
            }
            """;

        public const string RequestPasswordResetHandler = """
            namespace __NAME__.Application.Accounts.RequestPasswordReset
            {
                using __NAME__.Application.Accounts;
                using Truss.Application;
                using Truss.Auth;
                using Truss.Email;

                public class RequestPasswordResetHandler(
                    IUserRepository users,
                    IAccountTokenStore tokens,
                    IOneTimeTokens oneTimeTokens,
                    IEmailSender email) : ICommandHandler<RequestPasswordReset>
                {
                    public async Task<Unit> Handle(RequestPasswordReset command, CancellationToken cancellationToken)
                    {
                        // An unknown address gets the same silence as a known one,
                        // so the endpoint never confirms whether an account exists.
                        var user = await users.GetByEmail(command.Email.ToLowerInvariant(), cancellationToken);

                        if (user is null)
                            return Unit.Value;

                        var token = oneTimeTokens.Create(TimeSpan.FromHours(1));
                        tokens.Add(user.Id, token.TokenHash, AccountTokenPurpose.PasswordReset, token.ExpiresOn);

                        await email.Send(new EmailMessage(
                            user.Email,
                            "Reset your password",
                            $"<p>Use this token to set a new password. It expires in one hour.</p><p><code>{token.Token}</code></p>",
                            $"Use this token to set a new password (expires in one hour): {token.Token}"), cancellationToken);

                        return Unit.Value;
                    }
                }
            }
            """;

        public const string RequestPasswordResetValidator = """
            namespace __NAME__.Application.Accounts.RequestPasswordReset
            {
                using FluentValidation;

                public class RequestPasswordResetValidator : AbstractValidator<RequestPasswordReset>
                {
                    public RequestPasswordResetValidator()
                    {
                        RuleFor(command => command.Email).NotEmpty().EmailAddress();
                    }
                }
            }
            """;

        public const string ResetPassword = """
            namespace __NAME__.Application.Accounts.ResetPassword
            {
                using Truss.Application;

                public sealed record ResetPassword(string Token, string NewPassword) : ICommand;
            }
            """;

        public const string ResetPasswordHandler = """
            namespace __NAME__.Application.Accounts.ResetPassword
            {
                using __NAME__.Application.Accounts;
                using __NAME__.Application.Accounts.Rules;
                using Truss.Application;
                using Truss.Auth;
                using Truss.Domain;

                public class ResetPasswordHandler(
                    IAccountTokenStore tokens,
                    IOneTimeTokens oneTimeTokens,
                    IUserCredentialsStore credentials,
                    TimeProvider timeProvider) : ICommandHandler<ResetPassword>
                {
                    public async Task<Unit> Handle(ResetPassword command, CancellationToken cancellationToken)
                    {
                        var userId = await tokens.Consume(
                            oneTimeTokens.Hash(command.Token),
                            AccountTokenPurpose.PasswordReset,
                            timeProvider.GetUtcNow(),
                            cancellationToken);

                        if (userId is null)
                            throw new BusinessRuleValidationException(new InvalidToken());

                        await credentials.SetPassword(userId, command.NewPassword, cancellationToken);

                        return Unit.Value;
                    }
                }
            }
            """;

        public const string ResetPasswordValidator = """
            namespace __NAME__.Application.Accounts.ResetPassword
            {
                using FluentValidation;

                public class ResetPasswordValidator : AbstractValidator<ResetPassword>
                {
                    public ResetPasswordValidator()
                    {
                        RuleFor(command => command.Token).NotEmpty();
                        RuleFor(command => command.NewPassword).NotEmpty().MinimumLength(8);
                    }
                }
            }
            """;

        public const string ConfirmEmail = """
            namespace __NAME__.Application.Accounts.ConfirmEmail
            {
                using Truss.Application;

                public sealed record ConfirmEmail(string Token) : ICommand;
            }
            """;

        public const string ConfirmEmailHandler = """
            namespace __NAME__.Application.Accounts.ConfirmEmail
            {
                using __NAME__.Application.Accounts;
                using __NAME__.Application.Accounts.Rules;
                using Truss.Application;
                using Truss.Auth;
                using Truss.Domain;

                public class ConfirmEmailHandler(
                    IAccountTokenStore tokens,
                    IOneTimeTokens oneTimeTokens,
                    IAccountSecurityStore security,
                    TimeProvider timeProvider) : ICommandHandler<ConfirmEmail>
                {
                    public async Task<Unit> Handle(ConfirmEmail command, CancellationToken cancellationToken)
                    {
                        var userId = await tokens.Consume(
                            oneTimeTokens.Hash(command.Token),
                            AccountTokenPurpose.EmailConfirmation,
                            timeProvider.GetUtcNow(),
                            cancellationToken);

                        if (userId is null)
                            throw new BusinessRuleValidationException(new InvalidToken());

                        await security.ConfirmEmail(userId, cancellationToken);

                        return Unit.Value;
                    }
                }
            }
            """;

        public const string ConfirmEmailValidator = """
            namespace __NAME__.Application.Accounts.ConfirmEmail
            {
                using FluentValidation;

                public class ConfirmEmailValidator : AbstractValidator<ConfirmEmail>
                {
                    public ConfirmEmailValidator()
                    {
                        RuleFor(command => command.Token).NotEmpty();
                    }
                }
            }
            """;

        public const string VerifyTwoFactor = """
            namespace __NAME__.Application.Accounts.VerifyTwoFactor
            {
                using __NAME__.Application.Accounts.DTOs;
                using Truss.Application;

                public sealed record VerifyTwoFactor(string Email, string Code) : ICommand<AuthTokensDto>;
            }
            """;

        public const string VerifyTwoFactorHandler = """
            namespace __NAME__.Application.Accounts.VerifyTwoFactor
            {
                using System.Security.Claims;
                using __NAME__.Application.Accounts;
                using __NAME__.Application.Accounts.DTOs;
                using __NAME__.Application.Accounts.Rules;
                using Truss.Application;
                using Truss.Auth;
                using Truss.Domain;

                public class VerifyTwoFactorHandler(
                    IUserRepository users,
                    IAccountTokenStore accountTokens,
                    IOneTimeTokens oneTimeTokens,
                    IRefreshTokenStore refreshTokens,
                    IJwtTokenService tokens,
                    TimeProvider timeProvider) : ICommandHandler<VerifyTwoFactor, AuthTokensDto>
                {
                    public async Task<AuthTokensDto> Handle(VerifyTwoFactor command, CancellationToken cancellationToken)
                    {
                        var user = await users.GetByEmail(command.Email.ToLowerInvariant(), cancellationToken);

                        var userId = await accountTokens.Consume(
                            oneTimeTokens.Hash(command.Code),
                            AccountTokenPurpose.TwoFactor,
                            timeProvider.GetUtcNow(),
                            cancellationToken);

                        if (user is null || userId is null || userId != user.Id)
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

        public const string VerifyTwoFactorValidator = """
            namespace __NAME__.Application.Accounts.VerifyTwoFactor
            {
                using FluentValidation;

                public class VerifyTwoFactorValidator : AbstractValidator<VerifyTwoFactor>
                {
                    public VerifyTwoFactorValidator()
                    {
                        RuleFor(command => command.Email).NotEmpty().EmailAddress();
                        RuleFor(command => command.Code).NotEmpty().Length(6);
                    }
                }
            }
            """;

        public const string LoginWithFlows = """
            namespace __NAME__.Application.Accounts.Login
            {
                using __NAME__.Application.Accounts.DTOs;
                using Truss.Application;

                public sealed record Login(string Email, string Password) : ICommand<LoginResult>;
            }
            """;

        public const string LoginHandlerWithFlows = """
            namespace __NAME__.Application.Accounts.Login
            {
                using System.Security.Claims;
                using __NAME__.Application.Accounts;
                using __NAME__.Application.Accounts.DTOs;
                using __NAME__.Application.Accounts.Rules;
                using Truss.Application;
                using Truss.Auth;
                using Truss.Domain;
                using Truss.Email;

                public class LoginHandler(
                    IUserRepository users,
                    IUserCredentialsStore credentials,
                    IAccountSecurityStore security,
                    IAccountTokenStore accountTokens,
                    IOneTimeTokens oneTimeTokens,
                    IRefreshTokenStore refreshTokens,
                    IJwtTokenService tokens,
                    IEmailSender email) : ICommandHandler<Login, LoginResult>
                {
                    public async Task<LoginResult> Handle(Login command, CancellationToken cancellationToken)
                    {
                        var user = await users.GetByEmail(command.Email.ToLowerInvariant(), cancellationToken);

                        var validPassword = user is not null
                            && await credentials.VerifyPassword(user.Id, command.Password, cancellationToken);

                        if (user is null || !validPassword)
                            throw new BusinessRuleValidationException(new InvalidCredentials());

                        var accountSecurity = await security.Get(user.Id, cancellationToken);

                        if (accountSecurity.TwoFactorEnabled)
                        {
                            var code = oneTimeTokens.CreateCode(TimeSpan.FromMinutes(5));
                            accountTokens.Add(user.Id, code.TokenHash, AccountTokenPurpose.TwoFactor, code.ExpiresOn);

                            await email.Send(new EmailMessage(
                                user.Email,
                                "Your login code",
                                $"<p>Your login code is <strong>{code.Token}</strong>. It expires in five minutes.</p>",
                                $"Your login code is {code.Token} (expires in five minutes)."), cancellationToken);

                            return new LoginResult(true, null);
                        }

                        var refresh = tokens.CreateRefreshToken();
                        refreshTokens.Add(user.Id, refresh.TokenHash, refresh.ExpiresOn);

                        var access = tokens.CreateAccessToken(
                        [
                            new Claim("sub", user.Id.Value.ToString()),
                            new Claim("email", user.Email),
                            new Claim("name", user.Name)
                        ]);

                        return new LoginResult(false, new AuthTokensDto(access, refresh.Token));
                    }
                }
            }
            """;

        public const string RegisterUserHandlerWithFlows = """
            namespace __NAME__.Application.Accounts.RegisterUser
            {
                using __NAME__.Application.Accounts;
                using __NS_USER__;
                using __NS_USER_RULES__;__BIND_USING__
                using Truss.Application;
                using Truss.Auth;
                using Truss.Domain;
                using Truss.Email;

                public class RegisterUserHandler(
                    IUserRepository users,
                    IUserCredentialsStore credentials,
                    IAccountTokenStore tokens,
                    IOneTimeTokens oneTimeTokens,
                    IEmailSender email) : ICommandHandler<RegisterUser, Guid>
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

                        var confirmation = oneTimeTokens.Create(TimeSpan.FromHours(24));
                        tokens.Add(user.Id, confirmation.TokenHash, AccountTokenPurpose.EmailConfirmation, confirmation.ExpiresOn);

                        await email.Send(new EmailMessage(
                            user.Email,
                            "Confirm your email",
                            $"<p>Use this token to confirm your address. It expires in 24 hours.</p><p><code>{confirmation.Token}</code></p>",
                            $"Use this token to confirm your address (expires in 24 hours): {confirmation.Token}"), cancellationToken);

                        return user.Id.Value;
                    }
                }
            }
            """;

        public const string RegisterUserValidatorWithFlows = """
            namespace __NAME__.Application.Accounts.RegisterUser
            {
                using FluentValidation;
                using Truss.Email;

                public class RegisterUserValidator : AbstractValidator<RegisterUser>
                {
                    public RegisterUserValidator(IEmailAddressValidator emails)
                    {
                        RuleFor(command => command.Email)
                            .NotEmpty()
                            .MustAsync(async (email, cancellationToken) => (await emails.Validate(email, cancellationToken)).IsValid)
                            .WithMessage("The email address cannot receive mail.");

                        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
                        RuleFor(command => command.Password).NotEmpty().MinimumLength(8);
                    }
                }
            }
            """;

        public const string AccountTokenRecord = """
            namespace __NAME__.Infrastructure.Accounts
            {
                public class AccountTokenRecord
                {
                    private AccountTokenRecord()
                    {
                        TokenHash = string.Empty;
                    }

                    public AccountTokenRecord(Guid id, Guid userId, string tokenHash, int purpose, DateTimeOffset expiresOn)
                    {
                        Id = id;
                        UserId = userId;
                        TokenHash = tokenHash;
                        Purpose = purpose;
                        ExpiresOn = expiresOn;
                    }

                    public Guid Id { get; private set; }

                    public Guid UserId { get; private set; }

                    public string TokenHash { get; private set; }

                    public int Purpose { get; private set; }

                    public DateTimeOffset ExpiresOn { get; private set; }

                    public DateTimeOffset? UsedOn { get; private set; }

                    public bool IsUsable(DateTimeOffset now) => UsedOn is null && now < ExpiresOn;

                    public void MarkUsed(DateTimeOffset now) => UsedOn = now;
                }
            }
            """;

        public const string AccountTokenConfiguration = """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;

            namespace __NAME__.Infrastructure.Accounts
            {
                public class AccountTokenConfiguration : IEntityTypeConfiguration<AccountTokenRecord>
                {
                    public void Configure(EntityTypeBuilder<AccountTokenRecord> builder)
                    {
                        builder.ToTable("AccountTokens");
                        builder.HasKey(token => token.Id);

                        builder.Property(token => token.TokenHash).HasMaxLength(128).IsRequired();
                        builder.HasIndex(token => token.TokenHash);

                        builder.Property(token => token.ExpiresOn)
                            .HasConversion(value => value.UtcTicks, value => new DateTimeOffset(value, TimeSpan.Zero));

                        builder.Property(token => token.UsedOn)
                            .HasConversion(
                                value => value.HasValue ? value.Value.UtcTicks : (long?)null,
                                value => value.HasValue ? new DateTimeOffset(value.Value, TimeSpan.Zero) : null);
                    }
                }
            }
            """;

        public const string EfAccountTokenStore = """
            using __NAME__.Application.Accounts;
            using __NS_USER_ID__;
            using Microsoft.EntityFrameworkCore;

            namespace __NAME__.Infrastructure.Accounts
            {
                public class EfAccountTokenStore(AppDbContext context) : IAccountTokenStore
                {
                    public void Add(UserId userId, string tokenHash, AccountTokenPurpose purpose, DateTimeOffset expiresOn)
                    {
                        context.Set<AccountTokenRecord>().Add(
                            new AccountTokenRecord(Guid.NewGuid(), userId.Value, tokenHash, (int)purpose, expiresOn));
                    }

                    public async Task<UserId?> Consume(string tokenHash, AccountTokenPurpose purpose, DateTimeOffset now, CancellationToken cancellationToken = default)
                    {
                        var record = await context.Set<AccountTokenRecord>()
                            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash && token.Purpose == (int)purpose, cancellationToken);

                        if (record is null || !record.IsUsable(now))
                            return null;

                        record.MarkUsed(now);
                        return new UserId(record.UserId);
                    }
                }
            }
            """;

        public const string EfAccountSecurityStore = """
            using __NAME__.Application.Accounts;
            using __NS_USER_ID__;

            namespace __NAME__.Infrastructure.Accounts
            {
                public class EfAccountSecurityStore(AppDbContext context) : IAccountSecurityStore
                {
                    public async Task<AccountSecurity> Get(UserId userId, CancellationToken cancellationToken = default)
                    {
                        var credential = await context.Set<UserCredential>().FindAsync([userId.Value], cancellationToken);

                        return credential is null
                            ? new AccountSecurity(false, false)
                            : new AccountSecurity(credential.EmailConfirmed, credential.TwoFactorEnabled);
                    }

                    public async Task ConfirmEmail(UserId userId, CancellationToken cancellationToken = default)
                    {
                        var credential = await context.Set<UserCredential>().FindAsync([userId.Value], cancellationToken);
                        credential?.ConfirmEmail();
                    }

                    public async Task SetTwoFactorEnabled(UserId userId, bool enabled, CancellationToken cancellationToken = default)
                    {
                        var credential = await context.Set<UserCredential>().FindAsync([userId.Value], cancellationToken);
                        credential?.SetTwoFactorEnabled(enabled);
                    }
                }
            }
            """;

        public const string IdentityAccountSecurityStore = """
            using __NAME__.Application.Accounts;
            using __NS_USER_ID__;
            using Microsoft.AspNetCore.Identity;

            namespace __NAME__.Infrastructure.Accounts
            {
                public class IdentityAccountSecurityStore(UserManager<ApplicationUser> userManager) : IAccountSecurityStore
                {
                    public async Task<AccountSecurity> Get(UserId userId, CancellationToken cancellationToken = default)
                    {
                        var user = await userManager.FindByIdAsync(userId.Value.ToString());

                        return user is null
                            ? new AccountSecurity(false, false)
                            : new AccountSecurity(user.EmailConfirmed, user.TwoFactorEnabled);
                    }

                    public async Task ConfirmEmail(UserId userId, CancellationToken cancellationToken = default)
                    {
                        var user = await userManager.FindByIdAsync(userId.Value.ToString());

                        if (user is null)
                            return;

                        user.EmailConfirmed = true;
                        await userManager.UpdateAsync(user);
                    }

                    public async Task SetTwoFactorEnabled(UserId userId, bool enabled, CancellationToken cancellationToken = default)
                    {
                        var user = await userManager.FindByIdAsync(userId.Value.ToString());

                        if (user is not null)
                            await userManager.SetTwoFactorEnabledAsync(user, enabled);
                    }
                }
            }
            """;

        public const string ProgramEndpoints = """
            app.MapCommand<RegisterUser, Guid>("/auth/register");
            app.MapCommand<Login, LoginResult>("/auth/login");
            app.MapCommand<VerifyTwoFactor, AuthTokensDto>("/auth/login/2fa");
            app.MapCommand<Refresh, AuthTokensDto>("/auth/refresh");
            app.MapCommand<RequestPasswordReset>("/auth/password/request-reset");
            app.MapCommand<ResetPassword>("/auth/password/reset");
            app.MapCommand<ConfirmEmail>("/auth/confirm-email");
            """;
    }
}
