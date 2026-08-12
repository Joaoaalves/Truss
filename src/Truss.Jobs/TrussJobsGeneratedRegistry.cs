using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;

namespace Truss.Jobs
{
    /// <summary>
    /// Receives the job registrations produced at compile time by the
    /// Truss.Generators package. Each job arrives with its typed invoker, so
    /// building the registry needs neither an assembly scan nor
    /// MakeGenericType. Not intended to be called from application code.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class TrussJobsGeneratedRegistry
    {
        private static readonly ConcurrentDictionary<Assembly, ConcurrentDictionary<Type, JobDescriptor>> Jobs = new();

        /// <summary>
        /// Stores the generated descriptor of one job type.
        /// </summary>
        /// <typeparam name="TJob">The job type.</typeparam>
        /// <typeparam name="TArgs">The type of the job's arguments.</typeparam>
        /// <param name="assembly">The assembly the job was generated for.</param>
        public static void RegisterJob<TJob, TArgs>(Assembly assembly)
            where TJob : IJob<TArgs>
        {
            ArgumentNullException.ThrowIfNull(assembly);

            var name = typeof(TJob).GetCustomAttribute<JobNameAttribute>()?.Name ?? typeof(TJob).FullName!;

            Jobs.GetOrAdd(assembly, static _ => new ConcurrentDictionary<Type, JobDescriptor>())[typeof(TJob)] =
                new JobDescriptor(name, typeof(TJob), typeof(TArgs), new JobInvoker<TJob, TArgs>());
        }

        /// <summary>
        /// Marks an assembly as fully described by generated registrations, so
        /// the registry skips its scan even when it declares no jobs at all.
        /// </summary>
        /// <param name="assembly">The assembly the registration was generated for.</param>
        public static void RegisterAssembly(Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);

            Jobs.GetOrAdd(assembly, static _ => new ConcurrentDictionary<Type, JobDescriptor>());
        }

        internal static bool TryGetJobs(Assembly assembly, out IReadOnlyCollection<JobDescriptor> descriptors)
        {
            if (Jobs.TryGetValue(assembly, out var jobs))
            {
                descriptors = jobs.Values.ToArray();
                return true;
            }

            descriptors = [];
            return false;
        }
    }
}
