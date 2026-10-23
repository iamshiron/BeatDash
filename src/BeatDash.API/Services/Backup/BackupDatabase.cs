using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using Shiron.BeatDash.Data.Backup;
using Shiron.BeatDash.DB;

namespace Shiron.BeatDash.API.Services.Backup;

/// <summary>Complete, schema-bound database snapshots, executed exclusively inside the backend.</summary>
public sealed class BackupDatabase(BeatDashDbContext db) {
    private NpgsqlConnection Connection => (NpgsqlConnection) db.Database.GetDbConnection();

    internal ITable[] Tables {
        get {
            var remaining = db.Model.GetRelationalModel().Tables.ToList();
            var ordered = new List<ITable>();
            while (remaining.Count > 0) {
                var next = remaining.FirstOrDefault(t => t.ForeignKeyConstraints.All(f =>
                    f.PrincipalTable == t || ordered.Contains(f.PrincipalTable)))
                    ?? throw new InvalidOperationException("The database contains cyclic table dependencies.");
                ordered.Add(next);
                remaining.Remove(next);
            }
            return ordered.ToArray();
        }
    }

    internal static string Quote(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";
    internal static string TableName(ITable table) => Quote(table.Schema ?? "public") + "." + Quote(table.Name);
    internal string SchemaHash => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(db.Database.GenerateCreateScript())));

    internal async Task<int> GetMajorVersionAsync(CancellationToken ct) {
        await db.Database.OpenConnectionAsync(ct);
        return Connection.PostgreSqlVersion.Major;
    }

    internal async Task<BackupSequenceDTO[]> GetSequencesAsync(CancellationToken ct) {
        await db.Database.OpenConnectionAsync(ct);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var table in Tables) {
            foreach (var column in table.Columns) {
                await using var command = new NpgsqlCommand("SELECT pg_get_serial_sequence(@table, @column)", Connection);
                command.Parameters.AddWithValue("table", TableName(table));
                command.Parameters.AddWithValue("column", column.Name);
                if (await command.ExecuteScalarAsync(ct) is string name) names.Add(name);
            }
        }
        var result = new List<BackupSequenceDTO>();
        foreach (var name in names.Order(StringComparer.Ordinal)) {
            // Names originate exclusively from PostgreSQL's catalog, never from an archive.
            await using var command = new NpgsqlCommand($"SELECT last_value, is_called FROM {name}", Connection);
            await using var reader = await command.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            result.Add(new BackupSequenceDTO(name, reader.GetInt64(0), reader.GetBoolean(1)));
        }
        return result.ToArray();
    }

    internal async Task<BackupSequenceDTO[]> ExportAsync(Func<string, Stream, CancellationToken, Task> write, CancellationToken ct) {
        await db.Database.OpenConnectionAsync(ct);
        await using var transaction = await Connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        foreach (var table in Tables) {
            await using var copy = await Connection.BeginRawBinaryCopyAsync(CopySql(table, "TO STDOUT"), ct);
            await write(TableName(table), copy, ct);
        }
        var sequences = await GetSequencesAsync(ct);
        await transaction.CommitAsync(ct);
        return sequences;
    }

    internal async Task RestoreAsync(BackupManifest manifest, Func<BackupPayloadDTO, Stream> open, CancellationToken ct) {
        await db.Database.OpenConnectionAsync(ct);
        var sequences = await GetSequencesAsync(ct);
        if (!sequences.Select(s => s.Name).SequenceEqual(manifest.Sequences.Select(s => s.Name)))
            throw new InvalidDataException("Database sequence inventory does not match.");
        await using var transaction = await Connection.BeginTransactionAsync(ct);
        await using (var truncate = new NpgsqlCommand("TRUNCATE " + string.Join(", ", Tables.Select(TableName)), Connection))
            await truncate.ExecuteNonQueryAsync(ct);
        foreach (var table in Tables) {
            var payload = manifest.Tables.Single(t => t.Name == TableName(table)).Payload;
            await using var input = open(payload);
            await using var copy = await Connection.BeginRawBinaryCopyAsync(CopySql(table, "FROM STDIN"), ct);
            await input.CopyToAsync(copy, ct);
        }
        foreach (var sequence in manifest.Sequences) {
            await using var command = new NpgsqlCommand("SELECT setval(@name::regclass, @value, @called)", Connection);
            command.Parameters.AddWithValue("name", sequence.Name);
            command.Parameters.AddWithValue("value", sequence.LastValue);
            command.Parameters.AddWithValue("called", sequence.IsCalled);
            await command.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }

    private static string CopySql(ITable table, string direction) =>
        $"COPY {TableName(table)} ({string.Join(", ", table.Columns.Select(c => Quote(c.Name)))}) {direction} (FORMAT BINARY)";
}
