using Spectre.Console.Cli;

namespace Truss.Cli.Commands
{
    internal abstract class GenerateCommandBase<TSettings> : Command<TSettings>
        where TSettings : GenerateCommandBase<TSettings>.GenerateSettings
    {
        public abstract class GenerateSettings : CommandSettings
        {
            [CommandArgument(0, "<name>")]
            public string Name { get; init; } = string.Empty;

            [CommandOption("--context <CONTEXT>")]
            public string? Context { get; init; }

            [CommandOption("--project <PATH>")]
            public string? Project { get; init; }
        }

        protected override int Execute(CommandContext context, TSettings settings, CancellationToken cancellationToken)
        {
            var located = TrussManifest.Locate(settings.Project ?? Directory.GetCurrentDirectory());

            if (located is not { } project)
            {
                Console.WriteLine("No truss.json found. Run this command inside a project created with truss new, or pass --project.");
                return 1;
            }

            try
            {
                var written = Generate(project.Manifest, project.Root, settings);

                foreach (var path in written)
                    Console.WriteLine($"created {Path.GetRelativePath(project.Root, path)}");

                return 0;
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                Console.WriteLine(exception.Message);
                return 1;
            }
        }

        protected abstract IReadOnlyList<string> Generate(TrussManifest manifest, string root, TSettings settings);
    }

    internal sealed class GenerateContextCommand : GenerateCommandBase<GenerateContextCommand.Settings>
    {
        public sealed class Settings : GenerateSettings
        {
        }

        protected override IReadOnlyList<string> Generate(TrussManifest manifest, string root, Settings settings)
        {
            return CodeGenerator.GenerateContext(manifest, root, settings.Name);
        }
    }

    internal sealed class GenerateAggregateCommand : GenerateCommandBase<GenerateAggregateCommand.Settings>
    {
        public sealed class Settings : GenerateSettings
        {
        }

        protected override IReadOnlyList<string> Generate(TrussManifest manifest, string root, Settings settings)
        {
            return CodeGenerator.GenerateAggregate(manifest, root, settings.Name, settings.Context);
        }
    }

    internal sealed class GenerateCommandCommand : GenerateCommandBase<GenerateCommandCommand.Settings>
    {
        public sealed class Settings : GenerateSettings
        {
        }

        protected override IReadOnlyList<string> Generate(TrussManifest manifest, string root, Settings settings)
        {
            return CodeGenerator.GenerateCommand(manifest, root, settings.Name, settings.Context);
        }
    }

    internal sealed class GenerateQueryCommand : GenerateCommandBase<GenerateQueryCommand.Settings>
    {
        public sealed class Settings : GenerateSettings
        {
            [CommandOption("--result <TYPE>")]
            public string Result { get; init; } = "string";
        }

        protected override IReadOnlyList<string> Generate(TrussManifest manifest, string root, Settings settings)
        {
            return CodeGenerator.GenerateQuery(manifest, root, settings.Name, settings.Context, settings.Result);
        }
    }
}
