using Truss.Messaging;

namespace Microsoft.EntityFrameworkCore
{
    /// <summary>
    /// Provides the model configuration for the Truss inbox.
    /// Lives in the Microsoft.EntityFrameworkCore namespace so it is available
    /// inside OnModelCreating without additional usings.
    /// </summary>
    public static class TrussInboxModelBuilderExtensions
    {
        /// <summary>
        /// Adds the inbox record table to the model.
        /// Call this from OnModelCreating in the context passed to AddTrussInbox.
        /// The timestamp is stored as UTC ticks so filtering translates on every provider.
        /// </summary>
        /// <param name="modelBuilder">The model builder.</param>
        /// <returns>The updated <see cref="ModelBuilder"/>.</returns>
        public static ModelBuilder ApplyTrussInbox(this ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            modelBuilder.Entity<InboxRecord>(builder =>
            {
                builder.ToTable("TrussInbox");
                builder.HasKey(record => record.MessageId);

                builder.Property(record => record.Name).HasMaxLength(512).IsRequired();

                builder.Property(record => record.ProcessedOn)
                    .HasConversion(value => value.UtcTicks, value => new DateTimeOffset(value, TimeSpan.Zero));

                builder.HasIndex(record => record.ProcessedOn);
            });

            return modelBuilder;
        }
    }
}
