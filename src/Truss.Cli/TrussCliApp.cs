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

                // The generators are typed dozens of times a day, so every one of
                // them answers to a short alias too: truss g agg Order --crud.
                config.AddBranch("generate", generate =>
                {
                    generate.SetDescription("Generate building blocks inside the project. Alias: g");

                    generate.AddCommand<GenerateContextCommand>("context")
                        .WithAlias("ctx")
                        .WithDescription("Create the folders of a bounded context. Alias: ctx");

                    generate.AddCommand<GenerateAggregateCommand>("aggregate")
                        .WithAlias("agg")
                        .WithDescription("Create an aggregate with its typed id, event and starter rule. Alias: agg")
                        .WithExample("g", "agg", "Order", "--context", "Sales", "--crud");

                    generate.AddCommand<GenerateEntityCommand>("entity")
                        .WithAlias("ent")
                        .WithDescription("Create an entity with its typed id. Alias: ent");

                    generate.AddCommand<GenerateValueObjectCommand>("vo")
                        .WithDescription("Create a value object that guards its own invariants.")
                        .WithExample("g", "vo", "Money", "-c", "Shared", "-f", "Amount:decimal", "-f", "Currency:string");

                    generate.AddCommand<GenerateCommandCommand>("command")
                        .WithAlias("cmd")
                        .WithDescription("Create a command with its handler and validator. Alias: cmd");

                    generate.AddCommand<GenerateQueryCommand>("query")
                        .WithAlias("qry")
                        .WithDescription("Create a query with its handler. Alias: qry");
                })
                .WithAlias("g")
                .WithAlias("gen");

                config.AddBranch("remove", remove =>
                {
                    remove.SetDescription("Remove generated building blocks from the project. Alias: rm");
                    remove.AddCommand<RemoveContextCommand>("context")
                        .WithAlias("ctx")
                        .WithDescription("Delete a bounded context's folders and clean its wiring. Alias: ctx")
                        .WithExample("remove", "context", "Catalog");
                })
                .WithAlias("rm");

                config.AddCommand<DevCommand>("dev")
                    .WithDescription("Start the local dependencies and run the API with hot reload.");

                config.AddBranch("db", db =>
                {
                    db.SetDescription("Manage the database schema through EF Core migrations.");
                    db.AddCommand<DbAddCommand>("add")
                        .WithDescription("Add a migration capturing the current model changes.")
                        .WithExample("db", "add", "InitialCreate");
                    db.AddCommand<DbMigrateCommand>("migrate")
                        .WithDescription("Apply pending migrations to the database.");
                });

                config.AddCommand<UpdateCommand>("update")
                    .WithDescription("Point every Truss package at this CLI's version.");

                config.AddCommand<DoctorCommand>("doctor")
                    .WithDescription("Verify that the project matches its manifest.");
            });

            return app;
        }
    }
}
