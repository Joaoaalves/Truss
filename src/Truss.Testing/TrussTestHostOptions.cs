using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Truss.Jobs;
using Truss.Messaging;

namespace Truss.Testing
{
    /// <summary>
    /// Options for the test host.
    /// </summary>
    public sealed class TrussTestHostOptions
    {
        internal List<Assembly> Assemblies { get; } = [];

        internal bool MessagingEnabled { get; private set; }

        internal bool JobsEnabled { get; private set; }

        internal Action<TrussOutboxOptions>? OutboxConfiguration { get; private set; }

        internal Action<TrussJobsOptions>? JobsConfiguration { get; private set; }

        internal List<Action<IServiceCollection>> ServiceConfigurations { get; } = [];

        /// <summary>
        /// Adds an assembly to be scanned for handlers, events and jobs.
        /// </summary>
        /// <param name="assembly">The assembly to scan.</param>
        public void AddAssembly(Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);
            Assemblies.Add(assembly);
        }

        /// <summary>
        /// Adds the assembly containing the marker type to be scanned for handlers, events and jobs.
        /// </summary>
        /// <typeparam name="TMarker">A type contained in the assembly to scan.</typeparam>
        public void AddAssembly<TMarker>()
        {
            Assemblies.Add(typeof(TMarker).Assembly);
        }

        /// <summary>
        /// Enables messaging over the in-memory transport. With a database, the outbox
        /// is installed too, tuned for fast test turnaround.
        /// </summary>
        /// <param name="configure">Optional outbox configuration applied on top of the test defaults.</param>
        public void UseMessaging(Action<TrussOutboxOptions>? configure = null)
        {
            MessagingEnabled = true;
            OutboxConfiguration = configure;
        }

        /// <summary>
        /// Enables the job runtime with schedulers tuned for fast test turnaround.
        /// Implies messaging.
        /// </summary>
        /// <param name="configure">Optional jobs configuration applied on top of the test defaults.</param>
        public void UseJobs(Action<TrussJobsOptions>? configure = null)
        {
            MessagingEnabled = true;
            JobsEnabled = true;
            JobsConfiguration = configure;
        }

        /// <summary>
        /// Registers additional services, for example repositories and fakes.
        /// Runs after the Truss registrations, so replacements win.
        /// </summary>
        /// <param name="configure">The registration action.</param>
        public void ConfigureServices(Action<IServiceCollection> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            ServiceConfigurations.Add(configure);
        }
    }
}
