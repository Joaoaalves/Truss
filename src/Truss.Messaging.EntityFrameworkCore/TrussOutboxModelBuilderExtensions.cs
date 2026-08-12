using Truss.Messaging;

namespace Microsoft.EntityFrameworkCore
{
    /// <summary>
    /// Provides the model configuration for the Truss outbox.
    /// Lives in the Microsoft.EntityFrameworkCore namespace so it is available
    /// inside OnModelCreating without additional usings.
    /// </summary>
    public static class TrussOutboxModelBuilderExtensions
    {
        /// <summary>
        /// Adds the outbox message table to the model.
        /// Call this from OnModelCreating in the context passed to AddTrussOutbox.
        /// Timestamps are stored as UTC ticks so filtering and ordering translate on every provider.
        /// </summary>
        /// <param name="modelBuilder">The model builder.</param>
        /// <returns>The updated <see cref="ModelBuilder"/>.</returns>
        public static ModelBuilder ApplyTrussOutbox(this ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            modelBuilder.Entity<OutboxMessage>(builder =>
            {
                builder.ToTable("TrussOutbox");
                builder.HasKey(message => message.Id);

                builder.Property(message => message.Name).HasMaxLength(512).IsRequired();
                builder.Property(message => message.Version).IsRequired();
                builder.Property(message => message.Payload).IsRequired();
                builder.Property(message => message.Status).HasConversion<int>();
                builder.Property(message => message.TraceParent).HasMaxLength(128);

                builder.Property(message => message.OccurredOn)
                    .HasConversion(value => value.UtcTicks, value => new DateTimeOffset(value, TimeSpan.Zero));

                builder.Property(message => message.NextAttemptOn)
                    .HasConversion(
                        value => value.HasValue ? value.Value.UtcTicks : (long?)null,
                        value => value.HasValue ? new DateTimeOffset(value.Value, TimeSpan.Zero) : null);

                builder.Property(message => message.ProcessedOn)
                    .HasConversion(
                        value => value.HasValue ? value.Value.UtcTicks : (long?)null,
                        value => value.HasValue ? new DateTimeOffset(value.Value, TimeSpan.Zero) : null);

                builder.HasIndex(message => new { message.Status, message.NextAttemptOn });
            });

            return modelBuilder;
        }
    }
}
