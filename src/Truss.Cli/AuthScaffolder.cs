using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Truss.Cli.Templates;

namespace Truss.Cli
{
    internal static class AuthScaffolder
    {
        public static int Install(TrussManifest manifest, string root, Action<string> log)
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

            WriteScaffold(manifest, root);
            WireProgram(manifest, root, log);
            WriteJwtSettings(manifest, root);

            manifest.Settings["auth.provider"] = "jwt";
            return 0;
        }

        private static void WriteScaffold(TrussManifest manifest, string root)
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
            Write(root, Path.Combine(application, "RegisterUserHandler.cs"), AuthTemplates.RegisterUserHandler, manifest);
            Write(root, Path.Combine(application, "RegisterUserValidator.cs"), AuthTemplates.RegisterUserValidator, manifest);
            Write(root, Path.Combine(application, "Login.cs"), AuthTemplates.Login, manifest);
            Write(root, Path.Combine(application, "LoginHandler.cs"), AuthTemplates.LoginHandler, manifest);
            Write(root, Path.Combine(application, "LoginValidator.cs"), AuthTemplates.LoginValidator, manifest);
            Write(root, Path.Combine(application, "Refresh.cs"), AuthTemplates.Refresh, manifest);
            Write(root, Path.Combine(application, "RefreshHandler.cs"), AuthTemplates.RefreshHandler, manifest);
            Write(root, Path.Combine(application, "RefreshValidator.cs"), AuthTemplates.RefreshValidator, manifest);

            Write(root, Path.Combine(infrastructure, "UserConfiguration.cs"), AuthTemplates.UserConfiguration, manifest);
            Write(root, Path.Combine(infrastructure, "UserCredential.cs"), AuthTemplates.UserCredential, manifest);
            Write(root, Path.Combine(infrastructure, "UserCredentialConfiguration.cs"), AuthTemplates.UserCredentialConfiguration, manifest);
            Write(root, Path.Combine(infrastructure, "RefreshTokenRecord.cs"), AuthTemplates.RefreshTokenRecord, manifest);
            Write(root, Path.Combine(infrastructure, "RefreshTokenConfiguration.cs"), AuthTemplates.RefreshTokenConfiguration, manifest);
            Write(root, Path.Combine(infrastructure, "EfUserRepository.cs"), AuthTemplates.EfUserRepository, manifest);
            Write(root, Path.Combine(infrastructure, "EfUserCredentialsStore.cs"), AuthTemplates.EfUserCredentialsStore, manifest);
            Write(root, Path.Combine(infrastructure, "EfRefreshTokenStore.cs"), AuthTemplates.EfRefreshTokenStore, manifest);
            Write(root, Path.Combine(manifest.InfrastructureProject, "AccountsModule.cs"), AuthTemplates.AccountsModule, manifest);
        }

        private static void WireProgram(TrussManifest manifest, string root, Action<string> log)
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

            if (!SourceEditor.InsertBefore(program, "app.Run();", AuthTemplates.ProgramEndpoints))
            {
                log("Could not update Program.cs automatically. Add before app.Run():");
                log(AuthTemplates.ProgramEndpoints);
            }
        }

        private static void WriteJwtSettings(TrussManifest manifest, string root)
        {
            var path = Path.Combine(root, manifest.ApiProject, "appsettings.json");
            var settings = JsonNode.Parse(File.ReadAllText(path))!.AsObject();

            settings["Truss"] = new JsonObject
            {
                ["Auth"] = new JsonObject
                {
                    ["Jwt"] = new JsonObject
                    {
                        ["Issuer"] = manifest.Name,
                        ["Audience"] = manifest.Name,
                        ["SigningKey"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(48))
                    }
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
