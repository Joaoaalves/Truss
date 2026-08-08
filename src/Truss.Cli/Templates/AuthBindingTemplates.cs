namespace Truss.Cli.Templates
{
    /// <summary>
    /// Templates for binding the account User to an existing aggregate and for
    /// external login providers. In reference mode the User holds the aggregate's
    /// id; in merge mode the aggregate is the account and global using aliases
    /// point the scaffolded code at it.
    /// </summary>
    internal static class AuthBindingTemplates
    {
        public const string UserWithBinding = """
            using __AGGNS__;
            using Truss.Domain;

            namespace __NAME__.Domain.Accounts
            {
                public class User : AggregateRoot<UserId>
                {
                    private User()
                    {
                    }

                    private User(UserId id, string email, string name, __AGGID__ __AGGCAMEL__Id) : base(id)
                    {
                        Email = email;
                        Name = name;
                        __AGG__Id = __AGGCAMEL__Id;
                    }

                    public string Email { get; private set; } = string.Empty;

                    public string Name { get; private set; } = string.Empty;

                    public __AGGID__ __AGG__Id { get; private set; } = default!;

                    public static User Register(string email, string name, __AGGID__ __AGGCAMEL__Id)
                    {
                        var user = new User(new UserId(Guid.NewGuid()), email.ToLowerInvariant(), name, __AGGCAMEL__Id);
                        user.AddDomainEvent(new UserRegistered(user.Id));
                        return user;
                    }
                }
            }
            """;

        public const string AccountAliases = """
            // In this project the __AGG__ aggregate is the account. The scaffolded
            // commands and stores speak of users; these aliases point them at it.
            global using User = __AGGNS__.__AGG__;
            global using UserId = __AGGNS__.__AGGID__;
            """;

        public const string MergedAggregateConfiguration = """
            using __AGGNS__;
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;

            namespace __NAME__.Infrastructure.Accounts
            {
                public class __AGG__Configuration : IEntityTypeConfiguration<__AGG__>
                {
                    public void Configure(EntityTypeBuilder<__AGG__> builder)
                    {
                        builder.ToTable("__AGG__s");
                        builder.HasKey(entity => entity.Id);

                        builder.Property(entity => entity.Id)
                            .HasConversion(id => id.Value, value => new __AGGID__(value));

                        builder.Property(entity => entity.Email).HasMaxLength(320).IsRequired();
                        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();

                        builder.HasIndex(entity => entity.Email).IsUnique();
                    }
                }
            }
            """;

        public const string ExternalLoginStore = """
            using __NAME__.Domain.Accounts;

            namespace __NAME__.Application.Accounts
            {
                public interface IExternalLoginStore
                {
                    Task<UserId?> Find(string provider, string providerKey, CancellationToken cancellationToken = default);

                    void Add(UserId userId, string provider, string providerKey);
                }
            }
            """;

        public const string ExternalLogin = """
            using Truss.Application;

            namespace __NAME__.Application.Accounts
            {
                public sealed record ExternalLogin(string Provider, string ProviderKey, string Email, string Name) : ICommand<AuthTokensDto>;
            }
            """;

        public const string ExternalLoginValidator = """
            using FluentValidation;

            namespace __NAME__.Application.Accounts
            {
                public class ExternalLoginValidator : AbstractValidator<ExternalLogin>
                {
                    public ExternalLoginValidator()
                    {
                        RuleFor(command => command.Provider).NotEmpty();
                        RuleFor(command => command.ProviderKey).NotEmpty();
                        RuleFor(command => command.Email).NotEmpty().EmailAddress();
                        RuleFor(command => command.Name).NotEmpty();
                    }
                }
            }
            """;

        public const string ExternalLoginHandler = """
            using System.Security.Claims;
            using __NAME__.Domain.Accounts;
            using Truss.Application;
            using Truss.Auth;

            namespace __NAME__.Application.Accounts
            {
                public class ExternalLoginHandler(
                    IUserRepository users,
                    IExternalLoginStore externalLogins,
                    IRefreshTokenStore refreshTokens,
                    IJwtTokenService tokens) : ICommandHandler<ExternalLogin, AuthTokensDto>
                {
                    public async Task<AuthTokensDto> Handle(ExternalLogin command, CancellationToken cancellationToken)
                    {
                        var user = await Resolve(command, cancellationToken);

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

                    private async Task<User> Resolve(ExternalLogin command, CancellationToken cancellationToken)
                    {
                        var linked = await externalLogins.Find(command.Provider, command.ProviderKey, cancellationToken);

                        if (linked is not null)
                        {
                            var existing = await users.GetById(linked, cancellationToken);

                            if (existing is not null)
                                return existing;
                        }

                        var byEmail = await users.GetByEmail(command.Email.ToLowerInvariant(), cancellationToken);

                        if (byEmail is not null)
                        {
                            externalLogins.Add(byEmail.Id, command.Provider, command.ProviderKey);
                            return byEmail;
                        }

                        var user = User.Register(command.Email, command.Name);
                        users.Add(user);
                        externalLogins.Add(user.Id, command.Provider, command.ProviderKey);

                        return user;
                    }
                }
            }
            """;

        public const string ExternalLoginHandlerBound = """
            using System.Security.Claims;
            using __NAME__.Domain.Accounts;
            using Truss.Application;
            using Truss.Auth;
            using Truss.Domain;

            namespace __NAME__.Application.Accounts
            {
                public class ExternalLoginHandler(
                    IUserRepository users,
                    IExternalLoginStore externalLogins,
                    IRefreshTokenStore refreshTokens,
                    IJwtTokenService tokens) : ICommandHandler<ExternalLogin, AuthTokensDto>
                {
                    public async Task<AuthTokensDto> Handle(ExternalLogin command, CancellationToken cancellationToken)
                    {
                        var user = await Resolve(command, cancellationToken);

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

                    private async Task<User> Resolve(ExternalLogin command, CancellationToken cancellationToken)
                    {
                        var linked = await externalLogins.Find(command.Provider, command.ProviderKey, cancellationToken);

                        if (linked is not null)
                        {
                            var existing = await users.GetById(linked, cancellationToken);

                            if (existing is not null)
                                return existing;
                        }

                        // The account references a __AGG__, so it cannot be provisioned
                        // from an external profile alone: link to the account registered
                        // for this email, or ask the person to register first.
                        var byEmail = await users.GetByEmail(command.Email.ToLowerInvariant(), cancellationToken);

                        if (byEmail is null)
                            throw new BusinessRuleValidationException(new NoAccountForExternalLogin());

                        externalLogins.Add(byEmail.Id, command.Provider, command.ProviderKey);
                        return byEmail;
                    }
                }
            }
            """;

        public const string NoAccountForExternalLogin = """
            using Truss.Domain;

            namespace __NAME__.Application.Accounts
            {
                public class NoAccountForExternalLogin : IBusinessRule
                {
                    public bool IsBroken() => true;

                    public string Message => "No account exists for this email. Register first, then sign in with the external provider.";

                    public string Code => "accounts.external-unlinked";
                }
            }
            """;

        public const string ExternalLoginRecord = """
            namespace __NAME__.Infrastructure.Accounts
            {
                public class ExternalLoginRecord
                {
                    private ExternalLoginRecord()
                    {
                        Provider = string.Empty;
                        ProviderKey = string.Empty;
                    }

                    public ExternalLoginRecord(string provider, string providerKey, Guid userId)
                    {
                        Provider = provider;
                        ProviderKey = providerKey;
                        UserId = userId;
                    }

                    public string Provider { get; private set; }

                    public string ProviderKey { get; private set; }

                    public Guid UserId { get; private set; }
                }
            }
            """;

        public const string ExternalLoginConfiguration = """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;

            namespace __NAME__.Infrastructure.Accounts
            {
                public class ExternalLoginConfiguration : IEntityTypeConfiguration<ExternalLoginRecord>
                {
                    public void Configure(EntityTypeBuilder<ExternalLoginRecord> builder)
                    {
                        builder.ToTable("ExternalLogins");
                        builder.HasKey(login => new { login.Provider, login.ProviderKey });

                        builder.Property(login => login.Provider).HasMaxLength(64);
                        builder.Property(login => login.ProviderKey).HasMaxLength(256);

                        builder.HasIndex(login => login.UserId);
                    }
                }
            }
            """;

        public const string EfExternalLoginStore = """
            using __NAME__.Application.Accounts;
            using __NAME__.Domain.Accounts;

            namespace __NAME__.Infrastructure.Accounts
            {
                public class EfExternalLoginStore(AppDbContext context) : IExternalLoginStore
                {
                    public async Task<UserId?> Find(string provider, string providerKey, CancellationToken cancellationToken = default)
                    {
                        var record = await context.Set<ExternalLoginRecord>().FindAsync([provider, providerKey], cancellationToken);
                        return record is null ? null : new UserId(record.UserId);
                    }

                    public void Add(UserId userId, string provider, string providerKey)
                    {
                        context.Set<ExternalLoginRecord>().Add(new ExternalLoginRecord(provider, providerKey, userId.Value));
                    }
                }
            }
            """;

        public const string ExternalAuthEndpoints = """
            using System.Security.Claims;
            using __NAME__.Application.Accounts;
            using Microsoft.AspNetCore.Authentication;
            using Truss.Application;

            namespace __NAME__.Api
            {
                public static class ExternalAuthEndpoints
                {
                    private const string SignInScheme = "truss.external";

                    public static void MapExternalAuth(this WebApplication app)
                    {
                        app.MapGet("/auth/external/{provider}", async (string provider, IAuthenticationSchemeProvider schemes) =>
                        {
                            if (!await IsExternalProvider(schemes, provider))
                                return Results.NotFound();

                            return Results.Challenge(
                                new AuthenticationProperties { RedirectUri = $"/auth/external/{provider}/callback" },
                                [provider]);
                        });

                        app.MapGet("/auth/external/{provider}/callback", async (
                            string provider,
                            HttpContext http,
                            IDispatcher dispatcher,
                            CancellationToken cancellationToken) =>
                        {
                            var result = await http.AuthenticateAsync(SignInScheme);

                            if (!result.Succeeded)
                                return Results.Unauthorized();

                            var key = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
                            var email = result.Principal.FindFirstValue(ClaimTypes.Email);
                            var name = result.Principal.FindFirstValue(ClaimTypes.Name) ?? email;

                            if (key is null || email is null)
                                return Results.Unauthorized();

                            await http.SignOutAsync(SignInScheme);

                            var tokens = await dispatcher.Send(new ExternalLogin(provider, key, email, name!), cancellationToken);
                            return Results.Ok(tokens);
                        });
                    }

                    private static async Task<bool> IsExternalProvider(IAuthenticationSchemeProvider schemes, string provider)
                    {
                        var scheme = await schemes.GetSchemeAsync(provider);
                        return scheme?.HandlerType is { } handler && typeof(IAuthenticationRequestHandler).IsAssignableFrom(handler);
                    }
                }
            }
            """;

        public const string ProgramAuthenticationBlock = """
            builder.Services.AddAuthentication()
                .AddCookie("truss.external")__PROVIDERS__;
            """;

        public const string ProgramEndpoint = "app.MapExternalAuth();";

        public static string ProviderRegistration(string provider) => provider switch
        {
            "google" => """

                    .AddGoogle("google", options =>
                    {
                        options.SignInScheme = "truss.external";
                        options.ClientId = builder.Configuration["Truss:Auth:External:Google:ClientId"] ?? string.Empty;
                        options.ClientSecret = builder.Configuration["Truss:Auth:External:Google:ClientSecret"] ?? string.Empty;
                    })
                """,
            "microsoft" => """

                    .AddMicrosoftAccount("microsoft", options =>
                    {
                        options.SignInScheme = "truss.external";
                        options.ClientId = builder.Configuration["Truss:Auth:External:Microsoft:ClientId"] ?? string.Empty;
                        options.ClientSecret = builder.Configuration["Truss:Auth:External:Microsoft:ClientSecret"] ?? string.Empty;
                    })
                """,
            _ => """

                    .AddGitHub("github", options =>
                    {
                        options.SignInScheme = "truss.external";
                        options.ClientId = builder.Configuration["Truss:Auth:External:GitHub:ClientId"] ?? string.Empty;
                        options.ClientSecret = builder.Configuration["Truss:Auth:External:GitHub:ClientSecret"] ?? string.Empty;
                    })
                """
        };
    }
}
