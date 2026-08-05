using System.Reflection;
using System.Text.Json;

namespace Truss.Jobs
{
    internal abstract class JobInvoker
    {
        public abstract Task Invoke(IServiceProvider provider, string argsPayload, JobContext context, CancellationToken cancellationToken);
    }

    internal sealed class JobInvoker<TJob, TArgs> : JobInvoker where TJob : IJob<TArgs>
    {
        public override Task Invoke(IServiceProvider provider, string argsPayload, JobContext context, CancellationToken cancellationToken)
        {
            var args = JsonSerializer.Deserialize<TArgs>(argsPayload)!;
            var job = (IJob<TArgs>)provider.GetService(typeof(TJob))!;
            return job.Execute(args, context, cancellationToken);
        }
    }

    internal sealed record JobDescriptor(string Name, Type JobType, Type ArgsType, JobInvoker Invoker);

    /// <summary>
    /// Maps job types to their stable names and typed invokers.
    /// Built once at startup from the assemblies registered in the jobs module.
    /// </summary>
    public sealed class JobTypeRegistry
    {
        private readonly Dictionary<string, JobDescriptor> _byName = [];
        private readonly Dictionary<Type, JobDescriptor> _byType = [];

        internal static JobTypeRegistry FromAssemblies(IEnumerable<Assembly> assemblies)
        {
            var registry = new JobTypeRegistry();

            var jobTypes = assemblies
                .Distinct()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => type.IsClass && !type.IsAbstract && !type.IsGenericType);

            foreach (var type in jobTypes)
            {
                foreach (var @interface in type.GetInterfaces())
                {
                    if (!@interface.IsGenericType || @interface.GetGenericTypeDefinition() != typeof(IJob<>))
                        continue;

                    var argsType = @interface.GetGenericArguments()[0];
                    var name = type.GetCustomAttribute<JobNameAttribute>()?.Name ?? type.FullName!;

                    if (registry._byName.TryGetValue(name, out var existing))
                    {
                        throw new InvalidOperationException(
                            $"Job name '{name}' is declared by both {existing.JobType.FullName} and {type.FullName}."
                        );
                    }

                    var invoker = (JobInvoker)Activator.CreateInstance(
                        typeof(JobInvoker<,>).MakeGenericType(type, argsType))!;

                    var descriptor = new JobDescriptor(name, type, argsType, invoker);
                    registry._byName[name] = descriptor;
                    registry._byType[type] = descriptor;
                }
            }

            return registry;
        }

        internal JobDescriptor DescriptorFor(Type jobType)
        {
            if (_byType.TryGetValue(jobType, out var descriptor))
                return descriptor;

            throw new InvalidOperationException(
                $"Job type {jobType.FullName} is not registered. Expose its assembly with options.AddAssembly<TMarker>() when calling AddTrussJobs."
            );
        }

        internal JobDescriptor? Resolve(string name)
        {
            _byName.TryGetValue(name, out var descriptor);
            return descriptor;
        }

        internal IEnumerable<Type> JobTypes => _byType.Keys;
    }
}
