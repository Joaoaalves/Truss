using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Truss.Cli.Templates;

namespace Truss.Cli
{
    /// <summary>
    /// Options gathered by truss add auth: the credential provider, the optional
    /// binding of the account User to an existing aggregate, and the external
    /// login providers to wire.
    /// </summary>
    internal sealed record AuthAddOptions(string? BindUser, string? BindMode, string[] External)
    {
        public static readonly AuthAddOptions None = new(null, null, []);
    }

    internal static class AuthScaffolder
    {
        private static readonly string[] ExternalProviders = ["google", "microsoft", "github"];

        /// <summary>
        /// How the account relates to an existing aggregate: in reference mode the
        /// User carries the aggregate's id; in merge mode the aggregate is the
        /// account and aliases point the scaffolded code at it.
        /// </summary>
        private sealed record BindContext(string Aggregate, string IdType, string Namespace, bool Merge)
        {
            public string Camel => char.ToLowerInvariant(Aggregate[0]) + Aggregate[1..];
        }

        public static int Install(string provider, AuthAddOptions options, TrussManifest manifest, string root, Action<string> log)
        {
            if (!manifest.UsesEntityFramework)
            {
                log("The auth module stores users in the database and requires one. Scaffold the project with --database, or add persistence first.");
                return 1;
            }

            if (File.Exists(Path.Combine(root, manifest.DomainProject, "Accounts", "User.cs")))
            {
                log("An Accounts context already exists in the domain; refusing to overwrite it.");
                return 1;
            }

            if (options.BindUser is null && options.BindMode is not null)
            {
                log("--bind-mode only makes sense together with --bind-user.");
                return 1;
            }

            BindContext? bind = null;

            if (options.BindUser is not null)
            {
                bind = ResolveBinding(options.BindUser, options.BindMode, manifest, root, log);

                if (bind is null)
                    return 1;
            }

            var external = NormalizeExternal(options.External, log);

            if (external is null)
                return 1;

            CsprojEditor.AddPackageReference(
                CsprojPath(root, manifest.ApplicationProject), "Truss.Auth.Abstractions", manifest.TrussVersion);
            CsprojEditor.AddPackageReference(
                CsprojPath(root, manifest.ApiProject), "Truss.Auth.Jwt", manifest.TrussVersion);

            if (provider == "identity")
            {
                CsprojEditor.AddPackageReference(
                    CsprojPath(root, manifest.InfrastructureProject), "Microsoft.AspNetCore.Identity.EntityFrameworkCore", "10.*");
            }

            var flows = manifest.Modules.Contains("email");

            WriteScaffold(provider, flows, bind, external.Length > 0, manifest, root, log);
            WireProgram(flows, manifest, root, log);
            WriteJwtSettings(manifest, root);

            manifest.Settings["auth.provider"] = provider;

            if (bind is not null)
            {
                manifest.Settings["auth.bind"] = bind.Aggregate;
                manifest.Settings["auth.bind.mode"] = bind.Merge ? "merge" : "reference";
            }

            if (external.Length > 0)
                WireExternal(external, bind, manifest, root, log);

            if (flows)
                log("Account flows were scaffolded too: password reset, email confirmation and two factor login by email.");
            else
                log("Install the email module before auth to also scaffold password reset, email confirmation and two factor login.");

            if (bind is { Merge: true })
                log($"The {bind.Aggregate} aggregate is the account: User and UserId in the scaffolded code are aliases to it (AccountAliases.cs).");
            else if (bind is not null)
                log($"The User references the {bind.Aggregate} aggregate: registration takes its id and the {bind.Camel}Id claim carries it.");

            return 0;
        }

        /// <summary>
        /// Wires additional external login providers into a project where auth is
        /// already installed.
        /// </summary>
        public static int AddExternal(string[] providers, TrussManifest manifest, string root, Action<string> log)
        {
            var external = NormalizeExternal(providers, log);

            if (external is null)
                return 1;

            if (external.Length == 0)
            {
                log("The auth module is already installed. Pass --external google,microsoft,github to wire external login providers.");
                return 1;
            }

            var bind = ResolveInstalledBinding(manifest, root);

            WriteExternalScaffold(bind, manifest, root);

            var modulePath = Path.Combine(root, manifest.InfrastructureProject, "AccountsModule.cs");
            var registration = "services.AddScoped<IExternalLoginStore, EfExternalLoginStore>();";

            if (!(File.Exists(modulePath) && File.ReadAllText(modulePath).Contains(registration))
                && !InsertRawAfter(modulePath, "services.AddScoped<IRefreshTokenStore, EfRefreshTokenStore>();", $"{Environment.NewLine}            {registration}"))
            {
                log($"Could not update AccountsModule.cs automatically. Register the store yourself: {registration}");
            }

            WireExternal(external, bind, manifest, root, log);
            return 0;
        }

        private static BindContext? ResolveBinding(string aggregate, string? mode, TrussManifest manifest, string root, Action<string> log)
        {
            mode ??= "reference";

            if (mode is not ("reference" or "merge"))
            {
                log($"Unknown bind mode '{mode}'. Available modes: reference, merge.");
                return null;
            }

            if (string.Equals(aggregate, "User", StringComparison.OrdinalIgnoreCase))
            {
                log("Binding to an aggregate named User would collide with the scaffolded account type; rename the aggregate or skip the binding.");
                return null;
            }

            var domainRoot = Path.Combine(root, manifest.DomainProject);

            var file = Directory.Exists(domainRoot)
                ? Directory.EnumerateFiles(domainRoot, $"{aggregate}.cs", SearchOption.AllDirectories)
                    .FirstOrDefault(candidate => File.ReadAllText(candidate).Contains("AggregateRoot<"))
                : null;

            if (file is null)
            {
                log($"No {aggregate} aggregate was found in {manifest.DomainProject}. Generate it first: truss generate aggregate {aggregate} --context <Context>");
                return null;
            }

            var content = File.ReadAllText(file);
            var ns = Regex.Match(content, @"namespace\s+([\w.]+)").Groups[1].Value;
            var idType = Regex.Match(content, @"AggregateRoot<(\w+)>").Groups[1].Value;

            if (mode == "reference")
                return new BindContext(aggregate, idType, ns, Merge: false);

            if (idType == "Guid")
            {
                log($"Merge mode needs a typed id on the aggregate; {aggregate} is keyed by a plain Guid.");
                return null;
            }

            var missing = new List<string>();

            if (!content.Contains("public string Email"))
                missing.Add("    public string Email { get; private set; } = string.Empty;");

            if (!content.Contains("public string Name"))
                missing.Add("    public string Name { get; private set; } = string.Empty;");

            if (!content.Contains("Register("))
                missing.Add($"    public static {aggregate} Register(string email, string name) // create the aggregate with your factory and set Email and Name");

            if (missing.Count > 0)
            {
                log($"Merge mode makes {aggregate} the account, so it must own the identity fields. Add to the aggregate:");

                foreach (var member in missing)
                    log(member);

                log("Then run truss add auth again.");
                return null;
            }

            return new BindContext(aggregate, idType, ns, Merge: true);
        }

        private static BindContext? ResolveInstalledBinding(TrussManifest manifest, string root)
        {
            if (!manifest.Settings.TryGetValue("auth.bind", out var aggregate))
                return null;

            manifest.Settings.TryGetValue("auth.bind.mode", out var mode);
            return ResolveBinding(aggregate, mode, manifest, root, _ => { });
        }

        private static string[]? NormalizeExternal(string[] providers, Action<string> log)
        {
            var normalized = providers
                .Select(provider => provider.ToLowerInvariant())
                .Distinct()
                .OrderBy(provider => provider, StringComparer.Ordinal)
                .ToArray();

            var unknown = normalized.Except(ExternalProviders).ToArray();

            if (unknown.Length > 0)
            {
                log($"Unknown external provider '{unknown[0]}'. Available providers: {string.Join(", ", ExternalProviders)}.");
                return null;
            }

            return normalized;
        }

        private static void WriteScaffold(string provider, bool flows, BindContext? bind, bool external, TrussManifest manifest, string root, Action<string> log)
        {
            var domain = Path.Combine(manifest.DomainProject, "Accounts");
            var application = Path.Combine(manifest.ApplicationProject, "Accounts");
            var infrastructure = Path.Combine(manifest.InfrastructureProject, "Accounts");

            if (bind is { Merge: true })
            {
                Write(root, Path.Combine(application, "AccountAliases.cs"), AuthBindingTemplates.AccountAliases, manifest, bind);
                Write(root, Path.Combine(infrastructure, "AccountAliases.cs"), AuthBindingTemplates.AccountAliases, manifest, bind);
                EnsureMergedConfiguration(bind, manifest, root, log);
            }
            else
            {
                Write(root, Path.Combine(domain, "UserId.cs"), AuthTemplates.UserId, manifest, bind);
                Write(root, Path.Combine(domain, "User.cs"), bind is null ? AuthTemplates.User : AuthBindingTemplates.UserWithBinding, manifest, bind);
                Write(root, Path.Combine(domain, "UserRegistered.cs"), AuthTemplates.UserRegistered, manifest, bind);
                Write(root, Path.Combine(infrastructure, "UserConfiguration.cs"), AuthTemplates.UserConfiguration, manifest, bind);
            }

            Write(root, Path.Combine(domain, "EmailMustBeUnique.cs"), AuthTemplates.EmailMustBeUnique, manifest, bind);

            Write(root, Path.Combine(application, "InvalidCredentials.cs"), AuthTemplates.InvalidCredentials, manifest, bind);
            Write(root, Path.Combine(application, "IUserRepository.cs"), AuthTemplates.UserRepository, manifest, bind);
            Write(root, Path.Combine(application, "IUserCredentialsStore.cs"), AuthTemplates.UserCredentialsStore, manifest, bind);
            Write(root, Path.Combine(application, "IRefreshTokenStore.cs"), AuthTemplates.RefreshTokenStore, manifest, bind);
            Write(root, Path.Combine(application, "AuthTokensDto.cs"), AuthTemplates.AuthTokensDto, manifest, bind);
            Write(root, Path.Combine(application, "RegisterUser.cs"), AuthTemplates.RegisterUser, manifest, bind);
            Write(root, Path.Combine(application, "RegisterUserHandler.cs"), flows ? AuthFlowTemplates.RegisterUserHandlerWithFlows : AuthTemplates.RegisterUserHandler, manifest, bind);
            Write(root, Path.Combine(application, "RegisterUserValidator.cs"), flows ? AuthFlowTemplates.RegisterUserValidatorWithFlows : AuthTemplates.RegisterUserValidator, manifest, bind);
            Write(root, Path.Combine(application, "Login.cs"), flows ? AuthFlowTemplates.LoginWithFlows : AuthTemplates.Login, manifest, bind);
            Write(root, Path.Combine(application, "LoginHandler.cs"), flows ? AuthFlowTemplates.LoginHandlerWithFlows : AuthTemplates.LoginHandler, manifest, bind);
            Write(root, Path.Combine(application, "LoginValidator.cs"), AuthTemplates.LoginValidator, manifest, bind);
            Write(root, Path.Combine(application, "Refresh.cs"), AuthTemplates.Refresh, manifest, bind);
            Write(root, Path.Combine(application, "RefreshHandler.cs"), AuthTemplates.RefreshHandler, manifest, bind);
            Write(root, Path.Combine(application, "RefreshValidator.cs"), AuthTemplates.RefreshValidator, manifest, bind);

            Write(root, Path.Combine(infrastructure, "RefreshTokenRecord.cs"), AuthTemplates.RefreshTokenRecord, manifest, bind);
            Write(root, Path.Combine(infrastructure, "RefreshTokenConfiguration.cs"), AuthTemplates.RefreshTokenConfiguration, manifest, bind);
            Write(root, Path.Combine(infrastructure, "EfUserRepository.cs"), AuthTemplates.EfUserRepository, manifest, bind);
            Write(root, Path.Combine(infrastructure, "EfRefreshTokenStore.cs"), AuthTemplates.EfRefreshTokenStore, manifest, bind);

            if (provider == "identity")
            {
                Write(root, Path.Combine(infrastructure, "ApplicationUser.cs"), AuthTemplates.ApplicationUser, manifest, bind);
                Write(root, Path.Combine(infrastructure, "IdentityModelConfiguration.cs"), AuthTemplates.IdentityModelConfiguration, manifest, bind);
                Write(root, Path.Combine(infrastructure, "IdentityUserCredentialsStore.cs"), AuthTemplates.IdentityUserCredentialsStore, manifest, bind);
                WriteAccountsModule(root, manifest, AuthTemplates.AccountsModuleIdentity, flows, external, "IdentityAccountSecurityStore");
            }
            else
            {
                Write(root, Path.Combine(infrastructure, "UserCredential.cs"), AuthTemplates.UserCredential, manifest, bind);
                Write(root, Path.Combine(infrastructure, "UserCredentialConfiguration.cs"), AuthTemplates.UserCredentialConfiguration, manifest, bind);
                Write(root, Path.Combine(infrastructure, "EfUserCredentialsStore.cs"), AuthTemplates.EfUserCredentialsStore, manifest, bind);
                WriteAccountsModule(root, manifest, AuthTemplates.AccountsModule, flows, external, "EfAccountSecurityStore");
            }

            if (flows)
                WriteFlowScaffold(provider, bind, manifest, root);

            if (external)
                WriteExternalScaffold(bind, manifest, root);
        }

        private static void WriteFlowScaffold(string provider, BindContext? bind, TrussManifest manifest, string root)
        {
            var application = Path.Combine(manifest.ApplicationProject, "Accounts");
            var infrastructure = Path.Combine(manifest.InfrastructureProject, "Accounts");

            Write(root, Path.Combine(application, "IAccountSecurityStore.cs"), AuthFlowTemplates.AccountSecurityStore, manifest, bind);
            Write(root, Path.Combine(application, "IAccountTokenStore.cs"), AuthFlowTemplates.AccountTokens, manifest, bind);
            Write(root, Path.Combine(application, "InvalidToken.cs"), AuthFlowTemplates.InvalidToken, manifest, bind);
            Write(root, Path.Combine(application, "LoginResult.cs"), AuthFlowTemplates.LoginResult, manifest, bind);
            Write(root, Path.Combine(application, "RequestPasswordReset.cs"), AuthFlowTemplates.RequestPasswordReset, manifest, bind);
            Write(root, Path.Combine(application, "RequestPasswordResetHandler.cs"), AuthFlowTemplates.RequestPasswordResetHandler, manifest, bind);
            Write(root, Path.Combine(application, "RequestPasswordResetValidator.cs"), AuthFlowTemplates.RequestPasswordResetValidator, manifest, bind);
            Write(root, Path.Combine(application, "ResetPassword.cs"), AuthFlowTemplates.ResetPassword, manifest, bind);
            Write(root, Path.Combine(application, "ResetPasswordHandler.cs"), AuthFlowTemplates.ResetPasswordHandler, manifest, bind);
            Write(root, Path.Combine(application, "ResetPasswordValidator.cs"), AuthFlowTemplates.ResetPasswordValidator, manifest, bind);
            Write(root, Path.Combine(application, "ConfirmEmail.cs"), AuthFlowTemplates.ConfirmEmail, manifest, bind);
            Write(root, Path.Combine(application, "ConfirmEmailHandler.cs"), AuthFlowTemplates.ConfirmEmailHandler, manifest, bind);
            Write(root, Path.Combine(application, "ConfirmEmailValidator.cs"), AuthFlowTemplates.ConfirmEmailValidator, manifest, bind);
            Write(root, Path.Combine(application, "VerifyTwoFactor.cs"), AuthFlowTemplates.VerifyTwoFactor, manifest, bind);
            Write(root, Path.Combine(application, "VerifyTwoFactorHandler.cs"), AuthFlowTemplates.VerifyTwoFactorHandler, manifest, bind);
            Write(root, Path.Combine(application, "VerifyTwoFactorValidator.cs"), AuthFlowTemplates.VerifyTwoFactorValidator, manifest, bind);

            Write(root, Path.Combine(infrastructure, "AccountTokenRecord.cs"), AuthFlowTemplates.AccountTokenRecord, manifest, bind);
            Write(root, Path.Combine(infrastructure, "AccountTokenConfiguration.cs"), AuthFlowTemplates.AccountTokenConfiguration, manifest, bind);
            Write(root, Path.Combine(infrastructure, "EfAccountTokenStore.cs"), AuthFlowTemplates.EfAccountTokenStore, manifest, bind);

            if (provider == "identity")
                Write(root, Path.Combine(infrastructure, "IdentityAccountSecurityStore.cs"), AuthFlowTemplates.IdentityAccountSecurityStore, manifest, bind);
            else
                Write(root, Path.Combine(infrastructure, "EfAccountSecurityStore.cs"), AuthFlowTemplates.EfAccountSecurityStore, manifest, bind);
        }

        private static void WriteExternalScaffold(BindContext? bind, TrussManifest manifest, string root)
        {
            var application = Path.Combine(manifest.ApplicationProject, "Accounts");
            var infrastructure = Path.Combine(manifest.InfrastructureProject, "Accounts");

            WriteIfMissing(root, Path.Combine(application, "IExternalLoginStore.cs"), AuthBindingTemplates.ExternalLoginStore, manifest, bind);
            WriteIfMissing(root, Path.Combine(application, "ExternalLogin.cs"), AuthBindingTemplates.ExternalLogin, manifest, bind);
            WriteIfMissing(root, Path.Combine(application, "ExternalLoginValidator.cs"), AuthBindingTemplates.ExternalLoginValidator, manifest, bind);

            if (bind is { Merge: false })
            {
                WriteIfMissing(root, Path.Combine(application, "ExternalLoginHandler.cs"), AuthBindingTemplates.ExternalLoginHandlerBound, manifest, bind);
                WriteIfMissing(root, Path.Combine(application, "NoAccountForExternalLogin.cs"), AuthBindingTemplates.NoAccountForExternalLogin, manifest, bind);
            }
            else
            {
                WriteIfMissing(root, Path.Combine(application, "ExternalLoginHandler.cs"), AuthBindingTemplates.ExternalLoginHandler, manifest, bind);
            }

            WriteIfMissing(root, Path.Combine(infrastructure, "ExternalLoginRecord.cs"), AuthBindingTemplates.ExternalLoginRecord, manifest, bind);
            WriteIfMissing(root, Path.Combine(infrastructure, "ExternalLoginConfiguration.cs"), AuthBindingTemplates.ExternalLoginConfiguration, manifest, bind);
            WriteIfMissing(root, Path.Combine(infrastructure, "EfExternalLoginStore.cs"), AuthBindingTemplates.EfExternalLoginStore, manifest, bind);

            WriteIfMissing(root, Path.Combine(manifest.ApiProject, "ExternalAuthEndpoints.cs"), AuthBindingTemplates.ExternalAuthEndpoints, manifest, bind);
        }

        /// <summary>
        /// Adds packages, the authentication chain, the endpoint mapping and the
        /// configuration section for the requested external providers. Safe to run
        /// again with more providers: existing registrations are left alone.
        /// </summary>
        private static void WireExternal(string[] providers, BindContext? bind, TrussManifest manifest, string root, Action<string> log)
        {
            var apiCsproj = CsprojPath(root, manifest.ApiProject);
            var program = Path.Combine(root, manifest.ApiProject, "Program.cs");

            foreach (var provider in providers)
            {
                CsprojEditor.AddPackageReference(apiCsproj, provider switch
                {
                    "google" => "Microsoft.AspNetCore.Authentication.Google",
                    "microsoft" => "Microsoft.AspNetCore.Authentication.MicrosoftAccount",
                    _ => "AspNet.Security.OAuth.GitHub"
                }, "10.*");

                AppSettingsEditor.SetSection(root, manifest, $"Truss:Auth:External:{ProviderPascal(provider)}", new Dictionary<string, object>
                {
                    ["ClientId"] = string.Empty
                }, log);
            }

            var content = File.ReadAllText(program);

            if (!content.Contains(".AddCookie(\"truss.external\")"))
            {
                var block = AuthBindingTemplates.ProgramAuthenticationBlock
                    .Replace("__PROVIDERS__", string.Concat(providers.Select(AuthBindingTemplates.ProviderRegistration)));

                if (!SourceEditor.InsertBefore(program, "var app = builder.Build();", block))
                {
                    log("Could not update Program.cs automatically. Add before building the app:");
                    log(block);
                }
            }
            else
            {
                foreach (var provider in providers)
                {
                    if (File.ReadAllText(program).Contains($"(\"{provider}\", options =>"))
                        continue;

                    if (!InsertRawAfter(program, ".AddCookie(\"truss.external\")", AuthBindingTemplates.ProviderRegistration(provider)))
                        log($"Could not update Program.cs automatically. Chain onto AddAuthentication:{AuthBindingTemplates.ProviderRegistration(provider)}");
                }
            }

            if (!SourceEditor.InsertBefore(program, $"using {manifest.Name}.Application.Accounts;", $"using {manifest.Name}.Api;"))
                log($"Could not update Program.cs usings automatically. Add: using {manifest.Name}.Api;");

            if (!SourceEditor.InsertBefore(program, "app.Run();", AuthBindingTemplates.ProgramEndpoint))
                log($"Could not update Program.cs automatically. Add before app.Run(): {AuthBindingTemplates.ProgramEndpoint}");

            var installed = manifest.Settings.TryGetValue("auth.external", out var existing)
                ? existing.Split(',', StringSplitOptions.RemoveEmptyEntries)
                : [];

            manifest.Settings["auth.external"] = string.Join(",", installed.Concat(providers).Distinct().OrderBy(name => name, StringComparer.Ordinal));

            foreach (var provider in providers)
            {
                var pascal = ProviderPascal(provider);
                log($"Set Truss__Auth__External__{pascal}__ClientId and Truss__Auth__External__{pascal}__ClientSecret per environment; the secret never goes into appsettings.json.");
            }

            log($"Register the OAuth callback in each provider console as https://<host>/signin-<provider>. Start a login at GET /auth/external/<provider>.");

            if (bind is { Merge: false })
                log("With a bound User, external logins attach to the account registered for the same email; unknown emails are rejected with accounts.external-unlinked.");
        }

        private static void EnsureMergedConfiguration(BindContext bind, TrussManifest manifest, string root, Action<string> log)
        {
            var infrastructureRoot = Path.Combine(root, manifest.InfrastructureProject);

            var existing = Directory.Exists(infrastructureRoot)
                ? Directory.EnumerateFiles(infrastructureRoot, $"{bind.Aggregate}Configuration.cs", SearchOption.AllDirectories).FirstOrDefault()
                : null;

            if (existing is null)
            {
                Write(root, Path.Combine(manifest.InfrastructureProject, "Accounts", $"{bind.Aggregate}Configuration.cs"), AuthBindingTemplates.MergedAggregateConfiguration, manifest, bind);
                return;
            }

            if (!File.ReadAllText(existing).Contains(".Email"))
            {
                log($"Map the identity fields in {Path.GetFileName(existing)}:");
                log("    builder.Property(entity => entity.Email).HasMaxLength(320).IsRequired();");
                log("    builder.HasIndex(entity => entity.Email).IsUnique();");
            }
        }

        private static void WriteAccountsModule(string root, TrussManifest manifest, string template, bool flows, bool external, string securityStore)
        {
            var registrations = string.Empty;

            if (external)
                registrations += Environment.NewLine + "            services.AddScoped<IExternalLoginStore, EfExternalLoginStore>();";

            if (flows)
            {
                registrations += Environment.NewLine
                    + "            services.AddScoped<IAccountTokenStore, EfAccountTokenStore>();"
                    + Environment.NewLine
                    + $"            services.AddScoped<IAccountSecurityStore, {securityStore}>();";
            }

            var content = Render(template, manifest, bind: null).Replace("__FLOW_REGISTRATIONS__", registrations);
            var path = Path.Combine(root, manifest.InfrastructureProject, "AccountsModule.cs");

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content + Environment.NewLine);
        }

        private static void WireProgram(bool flows, TrussManifest manifest, string root, Action<string> log)
        {
            var program = Path.Combine(root, manifest.ApiProject, "Program.cs");

            if (!SourceEditor.InsertBefore(program, "using __NAME__.Infrastructure;".Replace("__NAME__", manifest.Name), Render(AuthTemplates.ProgramUsing, manifest, bind: null)))
                log($"Could not update Program.cs usings automatically. Add: {Render(AuthTemplates.ProgramUsing, manifest, bind: null)}");

            if (!SourceEditor.InsertBefore(program, "var app = builder.Build();", Render(AuthTemplates.ProgramServices, manifest, bind: null)))
            {
                log("Could not update Program.cs automatically. Add before building the app:");
                log(Render(AuthTemplates.ProgramServices, manifest, bind: null));
            }

            if (!SourceEditor.InsertAfter(program, "var app = builder.Build();", AuthTemplates.ProgramMiddleware))
                log("Could not update Program.cs automatically. Add after building the app: app.UseAuthentication(); app.UseAuthorization();");

            var endpoints = flows ? AuthFlowTemplates.ProgramEndpoints : AuthTemplates.ProgramEndpoints;

            if (!SourceEditor.InsertBefore(program, "app.Run();", endpoints))
            {
                log("Could not update Program.cs automatically. Add before app.Run():");
                log(endpoints);
            }
        }

        private static void WriteJwtSettings(TrussManifest manifest, string root)
        {
            var path = Path.Combine(root, manifest.ApiProject, "appsettings.json");
            var settings = JsonNode.Parse(File.ReadAllText(path))!.AsObject();

            // Merge into the Truss node instead of replacing it, so sections other
            // modules wrote before (email, for one) survive.
            if (settings["Truss"] is not JsonObject truss)
            {
                truss = new JsonObject();
                settings["Truss"] = truss;
            }

            truss["Auth"] = new JsonObject
            {
                ["Jwt"] = new JsonObject
                {
                    ["Issuer"] = manifest.Name,
                    ["Audience"] = manifest.Name,
                    ["SigningKey"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(48))
                }
            };

            File.WriteAllText(path, settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
        }

        private static string ProviderPascal(string provider) => provider switch
        {
            "google" => "Google",
            "microsoft" => "Microsoft",
            _ => "GitHub"
        };

        private static bool InsertRawAfter(string path, string anchor, string block)
        {
            if (!File.Exists(path))
                return false;

            var content = File.ReadAllText(path);
            var index = content.IndexOf(anchor, StringComparison.Ordinal);

            if (index < 0)
                return false;

            File.WriteAllText(path, content.Insert(index + anchor.Length, block));
            return true;
        }

        private static void Write(string root, string relativePath, string template, TrussManifest manifest, BindContext? bind)
        {
            var path = Path.Combine(root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, Render(template, manifest, bind) + Environment.NewLine);
        }

        private static void WriteIfMissing(string root, string relativePath, string template, TrussManifest manifest, BindContext? bind)
        {
            if (!File.Exists(Path.Combine(root, relativePath)))
                Write(root, relativePath, template, manifest, bind);
        }

        private static string Render(string template, TrussManifest manifest, BindContext? bind)
        {
            var content = template.Replace("__NAME__", manifest.Name);

            if (bind is null)
                return content;

            content = content
                .Replace("__AGGNS__", bind.Namespace)
                .Replace("__AGGID__", bind.IdType)
                .Replace("__AGGCAMEL__", bind.Camel)
                .Replace("__AGG__", bind.Aggregate);

            return bind.Merge ? content : ApplyReferenceBinding(content, manifest, bind);
        }

        /// <summary>
        /// Rewrites the rendered account scaffolding so registration carries the
        /// bound aggregate's id and every issued token claims it. The anchors are
        /// stable lines of our own templates; files without them pass through.
        /// </summary>
        private static string ApplyReferenceBinding(string content, TrussManifest manifest, BindContext bind)
        {
            var idFromCommand = bind.IdType == "Guid"
                ? $"command.{bind.Aggregate}Id"
                : $"new {bind.IdType}(command.{bind.Aggregate}Id)";

            var idValue = bind.IdType == "Guid"
                ? $"user.{bind.Aggregate}Id"
                : $"user.{bind.Aggregate}Id.Value";

            var accountsUsing = $"using {manifest.Name}.Domain.Accounts;";
            var aggregateUsing = $"using {bind.Namespace};";
            var needsUsing = bind.IdType != "Guid" && bind.Namespace != $"{manifest.Name}.Domain.Accounts";

            var updated = content.Replace(
                "RegisterUser(string Email, string Name, string Password)",
                $"RegisterUser(string Email, string Name, string Password, Guid {bind.Aggregate}Id)");

            if (updated.Contains("var user = User.Register(command.Email, command.Name);"))
            {
                updated = updated.Replace(
                    "var user = User.Register(command.Email, command.Name);",
                    $"var user = User.Register(command.Email, command.Name, {idFromCommand});");

                if (needsUsing && !updated.Contains(aggregateUsing))
                    updated = updated.Replace(accountsUsing, accountsUsing + "\n" + aggregateUsing);
            }

            updated = Regex.Replace(
                updated,
                "^([ \\t]*)new Claim\\(\"name\", user\\.Name\\)$",
                $"$1new Claim(\"name\", user.Name),\n$1new Claim(\"{bind.Camel}Id\", {idValue}.ToString())",
                RegexOptions.Multiline);

            updated = Regex.Replace(
                updated,
                "^([ \\t]*)RuleFor\\(command => command\\.Password\\)\\.NotEmpty\\(\\)\\.MinimumLength\\(8\\);$",
                $"$1RuleFor(command => command.Password).NotEmpty().MinimumLength(8);\n$1RuleFor(command => command.{bind.Aggregate}Id).NotEmpty();",
                RegexOptions.Multiline);

            if (bind.IdType != "Guid" && updated.Contains("builder.HasIndex(user => user.Email).IsUnique();"))
            {
                updated = updated.Replace(
                    "builder.HasIndex(user => user.Email).IsUnique();",
                    "builder.HasIndex(user => user.Email).IsUnique();\n\n"
                    + $"            builder.Property(user => user.{bind.Aggregate}Id)\n"
                    + $"                .HasConversion(id => id.Value, value => new {bind.IdType}(value));\n\n"
                    + $"            builder.HasIndex(user => user.{bind.Aggregate}Id);");

                if (needsUsing && !updated.Contains(aggregateUsing))
                    updated = updated.Replace(accountsUsing, accountsUsing + "\n" + aggregateUsing);
            }

            return updated;
        }

        private static string CsprojPath(string root, string projectDirectory)
        {
            var directory = Path.Combine(root, projectDirectory);
            return Directory.EnumerateFiles(directory, "*.csproj").First();
        }
    }
}
