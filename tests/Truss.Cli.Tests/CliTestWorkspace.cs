using Truss.Cli;

namespace Truss.Cli.Tests
{
    public sealed class CliTestWorkspace : IDisposable
    {
        public string Directory { get; } = Path.Combine(Path.GetTempPath(), "truss-cli-tests", Guid.NewGuid().ToString("N"));

        public CliTestWorkspace()
        {
            System.IO.Directory.CreateDirectory(Directory);
        }

        public int Run(params string[] args)
        {
            return TrussCliApp.Build().Run(args);
        }

        /// <summary>
        /// Runs a command and returns everything it printed, for the cases where
        /// the guidance itself is the behavior under test.
        /// </summary>
        public string Capture(params string[] args)
        {
            var original = Console.Out;
            using var writer = new StringWriter();

            Console.SetOut(writer);

            try
            {
                Run(args);
            }
            finally
            {
                Console.SetOut(original);
            }

            return writer.ToString();
        }

        public int Scaffold(string name, string database, params string[] extraArgs)
        {
            string[] args = ["new", name, "--database", database, "--output", Directory, .. extraArgs];
            return Run(args);
        }

        public string Root(string name) => Path.Combine(Directory, name);

        public string ReadFile(string name, params string[] segments)
        {
            return File.ReadAllText(Path.Combine([Root(name), .. segments]));
        }

        public bool FileExists(string name, params string[] segments)
        {
            return File.Exists(Path.Combine([Root(name), .. segments]));
        }

        public void Dispose()
        {
            try
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
            catch
            {
            }
        }
    }
}
