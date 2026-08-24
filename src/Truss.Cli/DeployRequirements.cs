namespace Truss.Cli
{
    /// <summary>
    /// One value the application will demand at boot in a real environment.
    /// Secret values must never live in appsettings.json, so presence in the
    /// environment is the only thing that satisfies them.
    /// </summary>
    internal sealed record DeployRequirement(string Key, string Reason, bool Secret);

    /// <summary>
    /// Derives, from the manifest, the exact list of environment values the
    /// installed modules will demand in production. This list is the number
    /// one cause of first-deploy crashloops, and the framework is the only
    /// one who knows it completely.
    /// </summary>
    internal static class DeployRequirements
    {
        public static IReadOnlyList<DeployRequirement> Build(TrussManifest manifest)
        {
            var requirements = new List<DeployRequirement>();

            if (manifest.UsesEntityFramework)
                requirements.Add(new DeployRequirement("ConnectionStrings__Default", $"the {manifest.Database} database", Secret: true));

            if (manifest.Settings.TryGetValue("messaging.transport", out var transport))
            {
                if (transport == "rabbitmq")
                    requirements.Add(new DeployRequirement("ConnectionStrings__RabbitMq", "the RabbitMQ transport", Secret: true));
                else if (transport == "redis")
                    requirements.Add(new DeployRequirement("ConnectionStrings__Redis", "the Redis transport", Secret: true));
            }

            if (manifest.Modules.Contains("auth"))
            {
                requirements.Add(new DeployRequirement(
                    "Truss__Auth__Jwt__SigningKey",
                    "auth: appsettings.json carries the scaffold's development key; production must override it",
                    Secret: true));
            }

            if (manifest.Settings.TryGetValue("auth.external", out var external))
            {
                foreach (var provider in external.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var pascal = provider switch { "google" => "Google", "microsoft" => "Microsoft", _ => "GitHub" };

                    requirements.Add(new DeployRequirement($"Truss__Auth__External__{pascal}__ClientId", $"{provider} login", Secret: false));
                    requirements.Add(new DeployRequirement($"Truss__Auth__External__{pascal}__ClientSecret", $"{provider} login", Secret: true));
                }
            }

            if (manifest.Settings.TryGetValue("email.provider", out var email))
            {
                if (email == "resend")
                    requirements.Add(new DeployRequirement("Truss__Email__Resend__ApiKey", "the Resend email provider", Secret: true));
                else if (email == "smtp")
                    requirements.Add(new DeployRequirement("Truss__Email__Smtp__Host", "the SMTP server; appsettings.json points at the development Mailpit", Secret: false));
            }

            if (manifest.Settings.ContainsKey("observability.dashboard"))
            {
                requirements.Add(new DeployRequirement(
                    "OTEL_EXPORTER_OTLP_ENDPOINT",
                    "telemetry export; the development value points at localhost",
                    Secret: false));
            }

            return requirements;
        }

        /// <summary>
        /// Facts worth stating even when every value is present.
        /// </summary>
        public static IReadOnlyList<string> Warnings(TrussManifest manifest, string root)
        {
            var warnings = new List<string>();

            manifest.Settings.TryGetValue("messaging.transport", out var transport);

            var hosts = 1
                + Directory.GetDirectories(Path.Combine(root, "src"), $"{manifest.Name}.*.Api").Length
                + (Directory.Exists(Path.Combine(root, "src", $"{manifest.Name}.Worker")) ? 1 : 0);

            if (manifest.Modules.Contains("messaging") && transport is null or "inmemory" && hosts > 1)
                warnings.Add("The inmemory transport does not cross processes; with more than one host, production needs postgres, rabbitmq or redis.");

            if (manifest.Database == "sqlite" && hosts > 1)
                warnings.Add("With sqlite each process opens its own database file; more than one host sharing state needs postgres or sqlserver.");

            foreach (var service in Directory.GetDirectories(Path.Combine(root, "src"), $"{manifest.Name}.*.Api"))
            {
                var name = Path.GetFileName(service);
                warnings.Add($"{name} is its own deployment: it needs the same values in its own environment, with its own ConnectionStrings__Default when it owns its database.");
            }

            return warnings;
        }
    }
}
