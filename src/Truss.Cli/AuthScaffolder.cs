using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Truss.Cli.Templates;

namespace Truss.Cli
{
    internal static class AuthScaffolder
    {
        public static int Install(string provider, TrussManifest manifest, string root, Action<string> log)
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

            WriteScaffold(provider, flows, manifest, root);
            WireProgram(flows, manifest, root, log);
            WriteJwtSettings(manifest, root);

            manifest.Settings["auth.provider"] = provider;

            if (flows)
                log("Account flows were scaffolded too: password reset, email confirmation and two factor login by email.");
            else
                log("Install the email module before auth to also scaffold password reset, email confirmation and two factor login.");

            return 0;
        }

        private static void WriteScaffold(string provider, bool flows, TrussManifest manifest, string root)
        {
            var domain = Path.Combine(manifest.DomainProject, "Accounts");
            var application = Path.Combine(manifest.ApplicationProject, "Accounts");
            var infrastructure = Path.Combine(manifest.InfrastructureProject, "Accounts");

            Write(root, Path.Combine(domain, "UserId.cs"), AuthTemplates.UserId, manifest);
            Write(root, Path.Combine(domain, "User.cs"), AuthTemplates.User, manifest);
            Write(root, Path.Combine(domain, "UserRegistered.cs"), AuthTemplates.UserRegistered, manifest);
            Write(root, Path.Combine(domain, "EmailMustBeUnique.cs"), AuthTemplates.EmailMustBeUnique, manifest);

            Write(root, Path.Combine(application, "InvalidCredentials.cs"), AuthTemplates.InvalidCredentials, manifest);
            Write(root, Path.Combine(application, "IUserRepository.cs"), AuthTemplates.UserRepository, manifest);
            Write(root, Path.Combine(application, "IUserCredentialsStore.cs"), AuthTemplates.UserCredentialsStore, manifest);
            Write(root, Path.Combine(application, "IRefreshTokenStore.cs"), AuthTemplates.RefreshTokenStore, manifest);
            Write(root, Path.Combine(application, "AuthTokensDto.cs"), AuthTemplates.AuthTokensDto, manifest);
            Write(root, Path.Combine(application, "RegisterUser.cs"), AuthTemplates.RegisterUser, manifest);
            Write(root, Path.Combine(application, "RegisterUserHandler.cs"), flows ? AuthFlowTemplates.RegisterUserHandlerWithFlows : AuthTemplates.RegisterUserHandler, manifest);
            Write(root, Path.Combine(application, "RegisterUserValidator.cs"), flows ? AuthFlowTemplates.RegisterUserValidatorWithFlows : AuthTemplates.RegisterUserValidator, manifest);
            Write(root, Path.Combine(application, "Login.cs"), flows ? AuthFlowTemplates.LoginWithFlows : AuthTemplates.Login, manifest);
            Write(root, Path.Combine(application, "LoginHandler.cs"), flows ? AuthFlowTemplates.LoginHandlerWithFlows : AuthTemplates.LoginHandler, manifest);
            Write(root, Path.Combine(application, "LoginValidator.cs"), AuthTemplates.LoginValidator, manifest);
            Write(root, Path.Combine(application, "Refresh.cs"), AuthTemplates.Refresh, manifest);
            Write(root, Path.Combine(application, "RefreshHandler.cs"), AuthTemplates.RefreshHandler, manifest);
            Write(root, Path.Combine(application, "RefreshValidator.cs"), AuthTemplates.RefreshValidator, manifest);

            Write(root, Path.Combine(infrastructure, "UserConfiguration.cs"), AuthTemplates.UserConfiguration, manifest);
            Write(root, Path.Combine(infrastructure, "RefreshTokenRecord.cs"), AuthTemplates.RefreshTokenRecord, manifest);
            Write(root, Path.Combine(infrastructure, "RefreshTokenConfiguration.cs"), AuthTemplates.RefreshTokenConfiguration, manifest);
            Write(root, Path.Combine(infrastructure, "EfUserRepository.cs"), AuthTemplates.EfUserRepository, manifest);
            Write(root, Path.Combine(infrastructure, "EfRefreshTokenStore.cs"), AuthTemplates.EfRefreshTokenStore, manifest);

            if (provider == "identity")
            {
                Write(root, Path.Combine(infrastructure, "ApplicationUser.cs"), AuthTemplates.ApplicationUser, manifest);
                Write(root, Path.Combine(infrastructure, "IdentityModelConfiguration.cs"), AuthTemplates.IdentityModelConfiguration, manifest);
                Write(root, Path.Combine(infrastructure, "IdentityUserCredentialsStore.cs"), AuthTemplates.IdentityUserCredentialsStore, manifest);
                WriteAccountsModule(root, manifest, AuthTemplates.AccountsModuleIdentity, flows, "IdentityAccountSecurityStore");
            }
            else
            {
                Write(root, Path.Combine(infrastructure, "UserCredential.cs"), AuthTemplates.UserCredential, manifest);
                Write(root, Path.Combine(infrastructure, "UserCredentialConfiguration.cs"), AuthTemplates.UserCredentialConfiguration, manifest);
                Write(root, Path.Combine(infrastructure, "EfUserCredentialsStore.cs"), AuthTemplates.EfUserCredentialsStore, manifest);
                WriteAccountsModule(root, manifest, AuthTemplates.AccountsModule, flows, "EfAccountSecurityStore");
            }

            if (flows)
                WriteFlowScaffold(provider, manifest, root);
        }

        private static void WriteFlowScaffold(string provider, TrussManifest manifest, string root)
        {
            var application = Path.Combine(manifest.ApplicationProject, "Accounts");
            var infrastructure = Path.Combine(manifest.InfrastructureProject, "Accounts");

            Write(root, Path.Combine(application, "IAccountSecurityStore.cs"), AuthFlowTemplates.AccountSecurityStore, manifest);
            Write(root, Path.Combine(application, "IAccountTokenStore.cs"), AuthFlowTemplates.AccountTokens, manifest);
            Write(root, Path.Combine(application, "InvalidToken.cs"), AuthFlowTemplates.InvalidToken, manifest);
            Write(root, Path.Combine(application, "LoginResult.cs"), AuthFlowTemplates.LoginResult, manifest);
            Write(root, Path.Combine(application, "RequestPasswordReset.cs"), AuthFlowTemplates.RequestPasswordReset, manifest);
            Write(root, Path.Combine(application, "RequestPasswordResetHandler.cs"), AuthFlowTemplates.RequestPasswordResetHandler, manifest);
            Write(root, Path.Combine(application, "RequestPasswordResetValidator.cs"), AuthFlowTemplates.RequestPasswordResetValidator, manifest);
            Write(root, Path.Combine(application, "ResetPassword.cs"), AuthFlowTemplates.ResetPassword, manifest);
            Write(root, Path.Combine(application, "ResetPasswordHandler.cs"), AuthFlowTemplates.ResetPasswordHandler, manifest);
            Write(root, Path.Combine(application, "ResetPasswordValidator.cs"), AuthFlowTemplates.ResetPasswordValidator, manifest);
            Write(root, Path.Combine(application, "ConfirmEmail.cs"), AuthFlowTemplates.ConfirmEmail, manifest);
            Write(root, Path.Combine(application, "ConfirmEmailHandler.cs"), AuthFlowTemplates.ConfirmEmailHandler, manifest);
            Write(root, Path.Combine(application, "ConfirmEmailValidator.cs"), AuthFlowTemplates.ConfirmEmailValidator, manifest);
            Write(root, Path.Combine(application, "VerifyTwoFactor.cs"), AuthFlowTemplates.VerifyTwoFactor, manifest);
            Write(root, Path.Combine(application, "VerifyTwoFactorHandler.cs"), AuthFlowTemplates.VerifyTwoFactorHandler, manifest);
            Write(root, Path.Combine(application, "VerifyTwoFactorValidator.cs"), AuthFlowTemplates.VerifyTwoFactorValidator, manifest);

            Write(root, Path.Combine(infrastructure, "AccountTokenRecord.cs"), AuthFlowTemplates.AccountTokenRecord, manifest);
            Write(root, Path.Combine(infrastructure, "AccountTokenConfiguration.cs"), AuthFlowTemplates.AccountTokenConfiguration, manifest);
            Write(root, Path.Combine(infrastructure, "EfAccountTokenStore.cs"), AuthFlowTemplates.EfAccountTokenStore, manifest);

            if (provider == "identity")
                Write(root, Path.Combine(infrastructure, "IdentityAccountSecurityStore.cs"), AuthFlowTemplates.IdentityAccountSecurityStore, manifest);
            else
                Write(root, Path.Combine(infrastructure, "EfAccountSecurityStore.cs"), AuthFlowTemplates.EfAccountSecurityStore, manifest);
        }

        private static void WriteAccountsModule(string root, TrussManifest manifest, string template, bool flows, string securityStore)
        {
            var registrations = flows
                ? Environment.NewLine
                    + "            services.AddScoped<IAccountTokenStore, EfAccountTokenStore>();"
                    + Environment.NewLine
                    + $"            services.AddScoped<IAccountSecurityStore, {securityStore}>();"
                : string.Empty;

            var content = Render(template, manifest).Replace("__FLOW_REGISTRATIONS__", registrations);
            var path = Path.Combine(root, manifest.InfrastructureProject, "AccountsModule.cs");

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content + Environment.NewLine);
        }

        private static void WireProgram(bool flows, TrussManifest manifest, string root, Action<string> log)
        {
            var program = Path.Combine(root, manifest.ApiProject, "Program.cs");

            if (!SourceEditor.InsertBefore(program, "using __NAME__.Infrastructure;".Replace("__NAME__", manifest.Name), Render(AuthTemplates.ProgramUsing, manifest)))
                log($"Could not update Program.cs usings automatically. Add: {Render(AuthTemplates.ProgramUsing, manifest)}");

            if (!SourceEditor.InsertBefore(program, "var app = builder.Build();", Render(AuthTemplates.ProgramServices, manifest)))
            {
                log("Could not update Program.cs automatically. Add before building the app:");
                log(Render(AuthTemplates.ProgramServices, manifest));
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

        private static void Write(string root, string relativePath, string template, TrussManifest manifest)
        {
            var path = Path.Combine(root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, Render(template, manifest) + Environment.NewLine);
        }

        private static string Render(string template, TrussManifest manifest)
        {
            return template.Replace("__NAME__", manifest.Name);
        }

        private static string CsprojPath(string root, string projectDirectory)
        {
            var directory = Path.Combine(root, projectDirectory);
            return Directory.EnumerateFiles(directory, "*.csproj").First();
        }
    }
}
