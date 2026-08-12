using Npgsql;

namespace Truss.Messaging.Postgres
{
    internal static class PostgresSchema
    {
        public const string MessagesTable = "truss_messages";
        public const string DeadLetterTable = "truss_messages_dead";

        // One DO block, one transaction: the advisory lock serializes instances
        // racing to create the schema on first boot, and the steady state takes
        // no exclusive lock that could deadlock against consumers holding row
        // locks.
        private const string CreateSql = $"""
            DO $truss_schema$
            BEGIN
                PERFORM pg_advisory_xact_lock(hashtext('{MessagesTable}'));

                CREATE TABLE IF NOT EXISTS {MessagesTable} (
                    sequence bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    message_id uuid NOT NULL,
                    name text NOT NULL,
                    version integer NOT NULL,
                    occurred_on timestamptz NOT NULL,
                    payload text NOT NULL,
                    attempts integer NOT NULL DEFAULT 0,
                    visible_on timestamptz NOT NULL DEFAULT now(),
                    trace_parent text
                );

                CREATE INDEX IF NOT EXISTS ix_{MessagesTable}_visible_on ON {MessagesTable} (visible_on);

                CREATE TABLE IF NOT EXISTS {DeadLetterTable} (
                    sequence bigint PRIMARY KEY,
                    message_id uuid NOT NULL,
                    name text NOT NULL,
                    version integer NOT NULL,
                    occurred_on timestamptz NOT NULL,
                    payload text NOT NULL,
                    attempts integer NOT NULL,
                    error text NOT NULL,
                    failed_on timestamptz NOT NULL,
                    trace_parent text
                );
            END $truss_schema$;
            """;

        public static void ValidateChannel(string channel)
        {
            if (string.IsNullOrEmpty(channel) || !channel.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') || char.IsAsciiDigit(channel[0]))
            {
                throw new InvalidOperationException(
                    $"Invalid Postgres notification channel '{channel}'. Use letters, digits and underscores, starting with a letter or underscore."
                );
            }
        }

        public static async Task EnsureCreated(TrussPostgresTransportOptions options, CancellationToken cancellationToken)
        {
            if (!options.AutoCreateSchema)
                return;

            await using var connection = new NpgsqlConnection(options.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand(CreateSql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
