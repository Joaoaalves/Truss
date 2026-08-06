using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Truss.Application;
using Truss.Persistence.EntityFrameworkCore;

namespace Truss.Persistence.EntityFrameworkCore
{
    /// <summary>
    /// Options for idempotent command storage.
    /// Bindable from configuration, for example the "Truss:Idempotency" section or
    /// environment variables such as Truss__Idempotency__RetentionPeriod.
    /// </summary>
    public sealed class TrussIdempotencyOptions
    {
        /// <summary>
        /// Gets or sets how long stored responses are kept. Defaults to 24 hours,
        /// which is how long a client retry can still expect a replay.
        /// Set to null to keep records forever.
        /// </summary>
        public TimeSpan? RetentionPeriod { get; set; } = TimeSpan.FromHours(24);

        /// <summary>
        /// Gets or sets how often expired records are swept. Defaults to 1 hour.
        /// </summary>
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);
    }

    internal sealed class IdempotencyCleanupService(
        IServiceScopeFactory scopeFactory,
        Microsoft.Extensions.Options.IOptions<TrussIdempotencyOptions> options,
        ILogger<IdempotencyCleanupService> logger,
        TimeProvider timeProvider) : BackgroundService
    {
        private readonly TrussIdempotencyOptions _options = options.Value;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_options.RetentionPeriod is not { } retention)
                return;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();

                    var deleted = await store.DeleteBefore(timeProvider.GetUtcNow() - retention, stoppingToken);

                    if (deleted > 0)
                        logger.LogInformation("Idempotency cleanup removed {Count} records older than {Retention}.", deleted, retention);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Idempotency cleanup iteration failed.");
                }

                try
                {
                    await Task.Delay(_options.CleanupInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}

namespace Microsoft.EntityFrameworkCore
{
    using Truss.Persistence.EntityFrameworkCore;

    /// <summary>
    /// Provides the model configuration for the idempotency store.
    /// Lives in the Microsoft.EntityFrameworkCore namespace so it is available
    /// inside OnModelCreating without additional usings.
    /// </summary>
    public static class TrussIdempotencyModelBuilderExtensions
    {
        /// <summary>
        /// Adds the idempotency table to the model.
        /// Call this from OnModelCreating in the context passed to AddTrussIdempotency.
        /// </summary>
        /// <param name="modelBuilder">The model builder.</param>
        /// <returns>The updated <see cref="ModelBuilder"/>.</returns>
        public static ModelBuilder ApplyTrussIdempotency(this ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            modelBuilder.Entity<IdempotencyRecord>(builder =>
            {
                builder.ToTable("TrussIdempotency");
                builder.HasKey(record => record.Key);

                builder.Property(record => record.Key).HasMaxLength(512);
                builder.Property(record => record.ResponsePayload).IsRequired();

                builder.Property(record => record.ProcessedOn)
                    .HasConversion(value => value.UtcTicks, value => new DateTimeOffset(value, TimeSpan.Zero));
            });

            return modelBuilder;
        }
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    using Truss.Application;
    using Truss.Persistence.EntityFrameworkCore;

    /// <summary>
    /// Provides methods to register idempotent command storage.
    /// Lives in the Microsoft.Extensions.DependencyInjection namespace so registration
    /// is available in the composition root without additional usings.
    /// </summary>
    public static class TrussIdempotencyModule
    {
        /// <summary>
        /// Registers the idempotency behavior backed by the given context, with a
        /// background sweep of expired records.
        /// Call after AddTrussEntityFramework so the behavior sits inside the unit
        /// of work, add the table with modelBuilder.ApplyTrussIdempotency(), and
        /// feed the key with UseTrussIdempotency in the HTTP pipeline.
        /// </summary>
        /// <typeparam name="TDbContext">The context that owns the idempotency table.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Optional configuration of retention.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddTrussIdempotency<TDbContext>(
            this IServiceCollection services,
            Action<TrussIdempotencyOptions>? configure = null)
            where TDbContext : DbContext
        {
            services.AddOptions<TrussIdempotencyOptions>();

            if (configure is not null)
                services.Configure(configure);

            services.TryAddSingleton(TimeProvider.System);
            services.AddScoped<IIdempotencyStore, EfIdempotencyStore<TDbContext>>();
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(IdempotencyBehavior<,>));
            services.AddHostedService<IdempotencyCleanupService>();

            return services;
        }
    }
}
