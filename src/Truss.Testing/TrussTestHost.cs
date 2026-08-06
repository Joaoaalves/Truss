using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Truss.Application;
using Truss.Jobs;
using Truss.Messaging;

namespace Truss.Testing
{
    /// <summary>
    /// Boots a Truss application for integration tests: the full pipeline with
    /// validation and unit of work, a throwaway sqlite database, the in-memory
    /// transport with the outbox, and the job runtime, all tuned for fast tests.
    /// Dispose the host to stop the background services and delete the database.
    /// </summary>
    public sealed class TrussTestHost : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly string? _databasePath;

        private TrussTestHost(ServiceProvider provider, string? databasePath)
        {
            _provider = provider;
            _databasePath = databasePath;
        }

        /// <summary>
        /// Gets the root service provider of the host.
        /// </summary>
        public IServiceProvider Services => _provider;

        /// <summary>
        /// Starts a host without a database. Messaging publishes directly and
        /// jobs use the in-memory store.
        /// </summary>
        /// <param name="configure">The host configuration.</param>
        public static Task<TrussTestHost> Start(Action<TrussTestHostOptions> configure)
        {
            return StartCore(configure, registerDatabase: null, initializeDatabase: null);
        }

        /// <summary>
        /// Starts a host around the given context, backed by a throwaway sqlite
        /// database created from the context model and deleted on dispose.
        /// The context keeps its own OnModelCreating, so the Truss tables it applies
        /// (outbox, jobs) exist exactly as in the real application.
        /// </summary>
        /// <typeparam name="TDbContext">The application context.</typeparam>
        /// <param name="configure">The host configuration.</param>
        public static Task<TrussTestHost> Start<TDbContext>(Action<TrussTestHostOptions> configure)
            where TDbContext : DbContext
        {
            var databasePath = Path.Combine(Path.GetTempPath(), $"truss-test-{Guid.NewGuid():N}.db");

            return StartCore(
                configure,
                registerDatabase: (services, options) =>
                {
                    services.AddDbContext<TDbContext>(builder => builder.UseSqlite($"Data Source={databasePath}"));
                    services.AddTrussEntityFramework<TDbContext>();

                    if (options.MessagingEnabled)
                    {
                        services.AddTrussOutbox<TDbContext>(outbox =>
                        {
                            outbox.PollingInterval = TimeSpan.FromMilliseconds(50);
                            outbox.RetryBaseDelay = TimeSpan.FromMilliseconds(20);
                            options.OutboxConfiguration?.Invoke(outbox);
                        });
                    }

                    if (options.JobsEnabled)
                        services.AddTrussJobsEntityFramework<TDbContext>();
                },
                initializeDatabase: provider =>
                {
                    using var scope = provider.CreateScope();
                    scope.ServiceProvider.GetRequiredService<TDbContext>().Database.EnsureCreated();
                },
                databasePath);
        }

        private static async Task<TrussTestHost> StartCore(
            Action<TrussTestHostOptions> configure,
            Action<IServiceCollection, TrussTestHostOptions>? registerDatabase,
            Action<IServiceProvider>? initializeDatabase,
            string? databasePath = null)
        {
            ArgumentNullException.ThrowIfNull(configure);

            var options = new TrussTestHostOptions();
            configure(options);

            if (options.Assemblies.Count == 0)
            {
                throw new InvalidOperationException(
                    "At least one assembly must be registered. Use options.AddAssembly<TMarker>() to expose the assemblies that contain your handlers."
                );
            }

            var services = new ServiceCollection();
            services.AddLogging();

            services.AddTruss(truss =>
            {
                foreach (var assembly in options.Assemblies)
                    truss.AddAssembly(assembly);
            });

            if (options.MessagingEnabled)
            {
                services.AddTrussMessaging(messaging =>
                {
                    foreach (var assembly in options.Assemblies)
                        messaging.AddAssembly(assembly);
                });

                services.AddTrussInMemoryTransport();
            }

            if (options.JobsEnabled)
            {
                services.AddTrussJobs(jobs =>
                {
                    foreach (var assembly in options.Assemblies)
                        jobs.AddAssembly(assembly);

                    jobs.ScheduledPollingInterval = TimeSpan.FromMilliseconds(50);
                    jobs.RecurringTickInterval = TimeSpan.FromMilliseconds(100);
                    jobs.RetryBaseDelay = TimeSpan.FromMilliseconds(50);
                    jobs.CancellationPollingInterval = TimeSpan.FromMilliseconds(50);
                    options.JobsConfiguration?.Invoke(jobs);
                });
            }

            registerDatabase?.Invoke(services, options);

            foreach (var configureServices in options.ServiceConfigurations)
                configureServices(services);

            var provider = services.BuildServiceProvider();

            try
            {
                initializeDatabase?.Invoke(provider);

                foreach (var hostedService in provider.GetServices<IHostedService>())
                    await hostedService.StartAsync(CancellationToken.None);
            }
            catch
            {
                await provider.DisposeAsync();
                throw;
            }

            return new TrussTestHost(provider, databasePath);
        }

        /// <summary>
        /// Dispatches a request through the full pipeline in its own scope,
        /// exactly as an endpoint would: validation runs and the unit of work commits.
        /// </summary>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        /// <param name="request">The request to dispatch.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response produced by the handler.</returns>
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            using var scope = _provider.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            return await dispatcher.Send(request, cancellationToken);
        }

        /// <summary>
        /// Runs an action inside a fresh service scope. Use it to reach the
        /// database context or any scoped service for assertions.
        /// </summary>
        /// <typeparam name="TResult">The type returned by the action.</typeparam>
        /// <param name="action">The action to run.</param>
        public async Task<TResult> ExecuteScoped<TResult>(Func<IServiceProvider, Task<TResult>> action)
        {
            ArgumentNullException.ThrowIfNull(action);

            using var scope = _provider.CreateScope();
            return await action(scope.ServiceProvider);
        }

        /// <summary>
        /// Returns the current snapshot of a job, or null when it does not exist.
        /// </summary>
        /// <param name="jobId">The job identifier.</param>
        public Task<JobSnapshot?> GetJob(Guid jobId)
        {
            return ExecuteScoped(provider => provider.GetRequiredService<IJobMonitor>().Get(jobId));
        }

        /// <summary>
        /// Waits until a job reaches the given status, failing with the last
        /// observed state when the timeout elapses.
        /// </summary>
        /// <param name="jobId">The job identifier.</param>
        /// <param name="status">The status to wait for.</param>
        /// <param name="timeout">The wait limit. Defaults to 10 seconds.</param>
        /// <returns>The snapshot in the awaited status.</returns>
        public async Task<JobSnapshot> WaitForJob(Guid jobId, JobStatus status, TimeSpan? timeout = null)
        {
            var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));

            while (DateTime.UtcNow < deadline)
            {
                var snapshot = await GetJob(jobId);

                if (snapshot?.Status == status)
                    return snapshot;

                await Task.Delay(25);
            }

            var last = await GetJob(jobId);
            throw new TimeoutException($"Job {jobId} did not reach {status}; last state: {last?.Status.ToString() ?? "missing"} ({last?.Error}).");
        }

        /// <summary>
        /// Publishes every pending outbox message and waits until the in-memory
        /// transport has handled everything, so delivery is deterministic instead
        /// of racing the background services.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The number of messages this call published; messages the
        /// background processor claimed first are not counted but are waited for.</returns>
        public async Task<int> DrainOutbox(CancellationToken cancellationToken = default)
        {
            var processor = _provider.GetService<OutboxProcessor>();

            if (processor is null)
            {
                throw new InvalidOperationException(
                    "The outbox is not registered. Start the host with a database context and call options.UseMessaging()."
                );
            }

            var total = 0;
            int processed;

            do
            {
                processed = await processor.ProcessPendingAsync(cancellationToken);
                total += processed;
            }
            while (processed > 0);

            if (_provider.GetService<InMemoryTransport>() is { } transport)
                await transport.WaitForIdle(cancellationToken);

            return total;
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            foreach (var hostedService in _provider.GetServices<IHostedService>())
                await hostedService.StopAsync(CancellationToken.None);

            await _provider.DisposeAsync();

            if (_databasePath is not null)
            {
                SqliteConnection.ClearAllPools();
                File.Delete(_databasePath);
            }
        }
    }
}
