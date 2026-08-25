using System.Reflection;
using System.Text.Json;

namespace Truss.Jobs.Runtime
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

            foreach (var assembly in assemblies.Distinct())
            {
                // Jobs registered at compile time arrive with their typed
                // invoker already built; only assemblies without a generated
                // registration are scanned.
                if (TrussJobsGeneratedRegistry.TryGetJobs(assembly, out var generated))
                {
                    foreach (var descriptor in generated)
                        registry.Add(descriptor);

                    continue;
                }

                foreach (var type in assembly.GetTypes().Where(type => type.IsClass && !type.IsAbstract && !type.IsGenericType))
                {
                    foreach (var @interface in type.GetInterfaces())
                    {
                        if (!@interface.IsGenericType || @interface.GetGenericTypeDefinition() != typeof(IJob<>))
                            continue;

                        var argsType = @interface.GetGenericArguments()[0];
                        var name = type.GetCustomAttribute<JobNameAttribute>()?.Name ?? type.FullName!;

                        var invoker = (JobInvoker)Activator.CreateInstance(
                            typeof(JobInvoker<,>).MakeGenericType(type, argsType))!;

                        registry.Add(new JobDescriptor(name, type, argsType, invoker));
                    }
                }
            }

            return registry;
        }

        private void Add(JobDescriptor descriptor)
        {
            if (_byName.TryGetValue(descriptor.Name, out var existing) && existing.JobType != descriptor.JobType)
            {
                throw new InvalidOperationException(
                    $"Job name '{descriptor.Name}' is declared by both {existing.JobType.FullName} and {descriptor.JobType.FullName}."
                );
            }

            _byName[descriptor.Name] = descriptor;
            _byType[descriptor.JobType] = descriptor;
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
