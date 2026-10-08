using Npgsql;

namespace SpaceAffinityApi.Context;

/// <summary>
/// Reflection-free description of how a type maps to a table. Delegates are supplied
/// explicitly so the context stays trim/AOT safe.
/// </summary>
public sealed class SqlEntityMap<T>
{
    public required string Table { get; init; }
    public required string KeyColumn { get; init; }

    /// <summary>Idempotent DDL (CREATE TABLE IF NOT EXISTS ...) run at startup.</summary>
    public required string CreateTableSql { get; init; }

    /// <summary>Non-key columns, in the order <see cref="Read"/> and <see cref="Bind"/> use them.</summary>
    public required string[] Columns { get; init; }

    /// <summary>True when the item already has a key (otherwise the database generates one).</summary>
    public required Func<T, bool> HasKey { get; init; }

    /// <summary>Reads a row shaped as: key column, then <see cref="Columns"/>.</summary>
    public required Func<NpgsqlDataReader, T> Read { get; init; }

    /// <summary>Adds parameters named p_key and p0..pN (matching <see cref="Columns"/>).</summary>
    public required Action<NpgsqlParameterCollection, T> Bind { get; init; }
}
