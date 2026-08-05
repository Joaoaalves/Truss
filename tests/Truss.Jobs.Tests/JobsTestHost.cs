using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Truss.Jobs.Tests.Fakes;

namespace Truss.Jobs.Tests
{
    public sealed class JobsTestHost : IAsyncDisposable
    {
        private readonly string _databasePath;

        public ServiceProvider Provider { get; }

        public JobsTestHost(Action<TrussJobsOptions>? jobs = null, bool startHostedServices = true)
        {
            _databasePath = Path.Combine(Path.GetTempPath(), $"truss-jobs-{Guid.NewGuid():N}.db");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<TickCounter>();
            services.AddDbContext<JobsDbContext>(options => options.UseSqlite($"Data Source={_databasePath}"));
            services.AddTruss(options => options.AddAssembly<StartReportCommand>());
            services.AddTrussEntityFramework<JobsDbContext>();
            services.AddTrussMessaging(options => options.AddAssembly<StartReportCommand>());
            services.AddTrussInMemoryTransport();
            services.AddTrussOutbox<JobsDbContext>(options => options.PollingInterval = TimeSpan.FromMilliseconds(50));
            services.AddTrussJobs(options =>
            {
                options.AddAssembly<ReportJob>();
                options.MaxAttempts = 2;
                options.ScheduledPollingInterval = TimeSpan.FromMilliseconds(50);
                options.RecurringTickInterval = TimeSpan.FromMilliseconds(100);
                jobs?.Invoke(options);
            });
            services.AddTrussJobsEntityFramework<JobsDbContext>();

            Provider = services.BuildServiceProvider();

            using var scope = Provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<JobsDbContext>().Database.EnsureCreated();

            if (startHostedServices)
            {
                foreach (var hostedService in Provider.GetServices<IHostedService>())
                    hostedService.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
        }

        public async Task<TResponse> Send<TResponse>(Truss.Application.IRequest<TResponse> request)
        {
            using var scope = Provider.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<Truss.Application.IDispatcher>();
            return await dispatcher.Send(request);
        }

        public async Task<JobSnapshot?> Snapshot(Guid jobId)
        {
            using var scope = Provider.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IJobMonitor>().Get(jobId);
        }

        public async Task<JobSnapshot> WaitForStatus(Guid jobId, JobStatus status, int timeoutSeconds = 10)
        {
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

            while (DateTime.UtcNow < deadline)
            {
                var snapshot = await Snapshot(jobId);

                if (snapshot?.Status == status)
                    return snapshot;

                await Task.Delay(25);
            }

            var last = await Snapshot(jobId);
            throw new TimeoutException($"Job {jobId} did not reach {status}; last state: {last?.Status.ToString() ?? "missing"} ({last?.Error}).");
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var hostedService in Provider.GetServices<IHostedService>())
                await hostedService.StopAsync(CancellationToken.None);

            await Provider.DisposeAsync();
            SqliteConnection.ClearAllPools();
            File.Delete(_databasePath);
        }
    }
}
