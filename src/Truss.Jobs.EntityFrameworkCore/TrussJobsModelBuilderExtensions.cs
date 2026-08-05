using Truss.Jobs;

namespace Microsoft.EntityFrameworkCore
{
    /// <summary>
    /// Provides the model configuration for the Truss job store.
    /// Lives in the Microsoft.EntityFrameworkCore namespace so it is available
    /// inside OnModelCreating without additional usings.
    /// </summary>
    public static class TrussJobsModelBuilderExtensions
    {
        /// <summary>
        /// Adds the job record table to the model.
        /// Call this from OnModelCreating in the context passed to AddTrussJobsEntityFramework.
        /// Timestamps are stored as UTC ticks so filtering and ordering translate on every provider.
        /// </summary>
        /// <param name="modelBuilder">The model builder.</param>
        /// <returns>The updated <see cref="ModelBuilder"/>.</returns>
        public static ModelBuilder ApplyTrussJobs(this ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            modelBuilder.Entity<JobRecord>(builder =>
            {
                builder.ToTable("TrussJobs");
                builder.HasKey(record => record.Id);

                builder.Property(record => record.Name).HasMaxLength(512).IsRequired();
                builder.Property(record => record.ArgsPayload).IsRequired();
                builder.Property(record => record.Status).HasConversion<int>();

                builder.Property(record => record.CreatedOn)
                    .HasConversion(value => value.UtcTicks, value => new DateTimeOffset(value, TimeSpan.Zero));

                builder.Property(record => record.ScheduledFor)
                    .HasConversion(
                        value => value.HasValue ? value.Value.UtcTicks : (long?)null,
                        value => value.HasValue ? new DateTimeOffset(value.Value, TimeSpan.Zero) : null);

                builder.Property(record => record.StartedOn)
                    .HasConversion(
                        value => value.HasValue ? value.Value.UtcTicks : (long?)null,
                        value => value.HasValue ? new DateTimeOffset(value.Value, TimeSpan.Zero) : null);

                builder.Property(record => record.CompletedOn)
                    .HasConversion(
                        value => value.HasValue ? value.Value.UtcTicks : (long?)null,
                        value => value.HasValue ? new DateTimeOffset(value.Value, TimeSpan.Zero) : null);

                builder.HasIndex(record => new { record.Status, record.ScheduledFor });
            });

            return modelBuilder;
        }
    }
}
