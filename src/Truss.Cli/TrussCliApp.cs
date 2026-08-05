using Spectre.Console.Cli;
using Truss.Cli.Commands;

namespace Truss.Cli
{
    /// <summary>
    /// Builds the truss command line application.
    /// </summary>
    public static class TrussCliApp
    {
        /// <summary>
        /// Creates the configured command application.
        /// </summary>
        public static CommandApp Build()
        {
            var app = new CommandApp();

            app.Configure(config =>
            {
                config.SetApplicationName("truss");

                config.AddCommand<NewCommand>("new")
                    .WithDescription("Scaffold a new Truss project.")
                    .WithExample("new", "MyShop", "--database", "postgres", "--docker");

                config.AddCommand<AddCommand>("add")
                    .WithDescription("Install a Truss module into an existing project.")
                    .WithExample("add", "messaging", "--transport", "redis")
                    .WithExample("add", "observability", "--dashboard", "aspire");

                config.AddBranch("generate", generate =>
                {
                    generate.SetDescription("Generate building blocks inside the project.");
                    generate.AddCommand<GenerateContextCommand>("context");
                    generate.AddCommand<GenerateAggregateCommand>("aggregate");
                    generate.AddCommand<GenerateCommandCommand>("command");
                    generate.AddCommand<GenerateQueryCommand>("query");
                });

                config.AddCommand<DoctorCommand>("doctor")
                    .WithDescription("Verify that the project matches its manifest.");
            });

            return app;
        }
    }
}
