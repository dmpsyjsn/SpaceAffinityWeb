using Npgsql;

namespace SpaceAffinityApi.Context
{
    public class PostgresPlainSqlContext<T> : IPlainSqlContext<T>
    {
        private readonly NpgsqlDataSource _dataSource;
        private readonly SqlEntityMap<T> _map;
        private readonly Dictionary<SqlStatement, string> _sql;

        private enum SqlStatement { Select, Insert, Upsert }

        public PostgresPlainSqlContext(NpgsqlDataSource dataSource, SqlEntityMap<T> map)
        {
            _dataSource = dataSource;
            _map = map;

            var key = Quote(map.KeyColumn);
            var table = Quote(map.Table);
            var cols = string.Join(", ", map.Columns.Select(Quote));
            var values = string.Join(", ", map.Columns.Select((_, i) => $"@p{i}"));
            var updates = string.Join(", ", map.Columns.Select(c => $"{Quote(c)} = EXCLUDED.{Quote(c)}"));

            _sql = new Dictionary<SqlStatement, string>
            {
                [SqlStatement.Select] = $"SELECT {key}, {cols} FROM {table} ORDER BY {key} LIMIT @take OFFSET @skip",
                // Key is database generated (identity), so insert without it.
                [SqlStatement.Insert] = $"INSERT INTO {table} ({cols}) VALUES ({values}) RETURNING {key}",
                [SqlStatement.Upsert] = $"INSERT INTO {table} ({key}, {cols}) VALUES (@p_key, {values}) " +
                                        $"ON CONFLICT ({key}) DO UPDATE SET {updates} RETURNING {key}"
            };
        }

        public async Task EnsureTableExists()
        {
            await using var conn = await _dataSource.OpenConnectionAsync();
            await using var tx = await conn.BeginTransactionAsync();

            // Serialize concurrent startups (e.g. several pods); CREATE TABLE IF NOT EXISTS alone can race.
            await using (var lockCmd = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext(@name))", conn, tx))
            {
                lockCmd.Parameters.AddWithValue("name", _map.Table);
                await lockCmd.ExecuteNonQueryAsync();
            }

            await using (var createCmd = new NpgsqlCommand(_map.CreateTableSql, conn, tx))
            {
                await createCmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }

        public async Task<T[]> GetData(int skip, int take)
        {
            await using var cmd = _dataSource.CreateCommand(_sql[SqlStatement.Select]);
            cmd.Parameters.AddWithValue("skip", skip);
            cmd.Parameters.AddWithValue("take", take);

            await using var reader = await cmd.ExecuteReaderAsync();
            var results = new List<T>();
            while (await reader.ReadAsync())
            {
                results.Add(_map.Read(reader));
            }
            return [.. results];
        }

        public async Task<int> UpsertItem(T item)
        {
            await using var cmd = _dataSource.CreateCommand(_sql[_map.HasKey(item) ? SqlStatement.Upsert : SqlStatement.Insert]);
            _map.Bind(cmd.Parameters, item);
            var id = (int) (await cmd.ExecuteScalarAsync() ?? -1);

            return id;
        }

        private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
    }
}
