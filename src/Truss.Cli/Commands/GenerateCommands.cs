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

            [CommandOption("-c|--context <CONTEXT>")]
            public string? Context { get; init; }

            [CommandOption("-p|--project <PATH>")]
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
            [CommandOption("--as-projects")]
            public bool AsProjects { get; init; }
        }

        protected override IReadOnlyList<string> Generate(TrussManifest manifest, string root, Settings settings)
        {
            return settings.AsProjects
                ? ContextProjects.Create(manifest, root, settings.Name, Console.WriteLine)
                : CodeGenerator.GenerateContext(manifest, root, settings.Name);
        }
    }

    internal sealed class GenerateAggregateCommand : GenerateCommandBase<GenerateAggregateCommand.Settings>
    {
        public sealed class Settings : GenerateSettings
        {
            [CommandOption("--crud")]
            public bool Crud { get; init; }

            [CommandOption("--vo <MEMBER>")]
            public string[]? Vo { get; init; }
        }

        protected override IReadOnlyList<string> Generate(TrussManifest manifest, string root, Settings settings)
        {
            var files = CodeGenerator.GenerateAggregate(manifest, root, settings.Name, settings.Context, settings.Crud, settings.Vo, Console.WriteLine);

            if (settings.Crud)
                CodeGenerator.WireCrud(manifest, root, settings.Name, settings.Context, Console.WriteLine);

            return files;
        }
    }

    internal sealed class GenerateEntityCommand : GenerateCommandBase<GenerateEntityCommand.Settings>
    {
        public sealed class Settings : GenerateSettings
        {
            [CommandOption("-a|--aggregate <AGGREGATE>")]
            public string? Aggregate { get; init; }

            [CommandOption("--vo <MEMBER>")]
            public string[]? Vo { get; init; }
        }

        protected override IReadOnlyList<string> Generate(TrussManifest manifest, string root, Settings settings)
        {
            return CodeGenerator.GenerateEntity(manifest, root, settings.Name, settings.Context, settings.Aggregate, settings.Vo, Console.WriteLine);
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

    internal sealed class GenerateValueObjectCommand : GenerateCommandBase<GenerateValueObjectCommand.Settings>
    {
        public sealed class Settings : GenerateSettings
        {
            [CommandOption("-f|--field <FIELD>")]
            public string[]? Field { get; init; }

            [CommandOption("--vo <MEMBER>")]
            public string[]? Vo { get; init; }

            [CommandOption("-a|--aggregate <AGGREGATE>")]
            public string? Aggregate { get; init; }
        }

        protected override IReadOnlyList<string> Generate(TrussManifest manifest, string root, Settings settings)
        {
            return CodeGenerator.GenerateValueObject(manifest, root, settings.Name, settings.Context, settings.Field, settings.Vo, settings.Aggregate, Console.WriteLine);
        }
    }

    internal sealed class GenerateQueryCommand : GenerateCommandBase<GenerateQueryCommand.Settings>
    {
        public sealed class Settings : GenerateSettings
        {
            [CommandOption("-r|--result <TYPE>")]
            public string Result { get; init; } = "string";

            [CommandOption("--paged")]
            public bool Paged { get; init; }
        }

        protected override IReadOnlyList<string> Generate(TrussManifest manifest, string root, Settings settings)
        {
            return CodeGenerator.GenerateQuery(manifest, root, settings.Name, settings.Context, settings.Result, settings.Paged);
        }
    }
}
