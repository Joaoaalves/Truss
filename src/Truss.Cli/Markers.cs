namespace Truss.Cli
{
    /// <summary>
    /// The comment markers the scaffold leaves in composition roots. Every
    /// insertion targets its marker, so the user can reformat everything else
    /// freely; deleting a marker makes truss add print what to paste instead
    /// of guessing. Blocks accumulate above their marker, so the order in the
    /// file is the order the modules were installed.
    /// </summary>
    internal static class Markers
    {
        /// <summary>Registrations, above var app = builder.Build().</summary>
        public const string Services = "// truss: services";

        /// <summary>Middleware, below builder.Build().</summary>
        public const string Middleware = "// truss: middleware";

        /// <summary>Route mappings, above app.Run().</summary>
        public const string Endpoints = "// truss: endpoints";

        /// <summary>Model configuration, inside OnModelCreating.</summary>
        public const string Model = "// truss: model";
    }
}
