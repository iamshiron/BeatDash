using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shiron.BeatDash.Data.Backup;
using Shiron.BeatDash.DB;

namespace Shiron.BeatDash.API.Services.Backup;

/// <summary>
/// SQLite snapshots read inside one transaction. Each table payload is a row stream in which
/// every value carries its SQLite storage class, so restoring reproduces the stored values exactly.
/// AUTOINCREMENT counters from <c>sqlite_sequence</c> are the sequences.
/// </summary>
public sealed class SqliteBackupDatabase(BeatDashDbContext db) : BackupDatabase(db) {
    private const byte EndOfTable = 0;
    private const byte Row = 1;

    private enum ValueTag : byte { Null, Integer, Real, Text, Blob }

    private SqliteConnection Connection => (SqliteConnection) Db.Database.GetDbConnection();

    /// <inheritdoc/>
    public override BackupDatabaseEngine Engine => BackupDatabaseEngine.Sqlite;

    internal override string TableName(ITable table) => Quote(table.Name);

    internal override Task<int> GetMajorVersionAsync(CancellationToken ct) => Task.FromResult(0);

    internal override async Task<BackupSequenceDTO[]> GetSequencesAsync(CancellationToken ct) {
        await Db.Database.OpenConnectionAsync(ct);
        return await ReadSequencesAsync(null, ct);
    }

    internal override async Task<BackupSequenceDTO[]> ExportAsync(Func<string, Func<Stream, Task>, Task> addTable, CancellationToken ct) {
        await Db.Database.OpenConnectionAsync(ct);
        await using var transaction = (SqliteTransaction) await Connection.BeginTransactionAsync(ct);
        foreach (var table in Tables) await addTable(TableName(table), output => WriteTableAsync(table, transaction, output, ct));
        var sequences = await ReadSequencesAsync(transaction, ct);
        await transaction.CommitAsync(ct);
        return sequences;
    }

    internal override async Task RestoreAsync(BackupManifest manifest, Func<BackupPayloadDTO, Stream> open, CancellationToken ct) {
        await Db.Database.OpenConnectionAsync(ct);
        var sequences = await ReadSequencesAsync(null, ct);
        if (!sequences.Select(s => s.Name).SequenceEqual(manifest.Sequences.Select(s => s.Name)))
            throw new InvalidDataException("Database sequence inventory does not match.");
        await using var transaction = (SqliteTransaction) await Connection.BeginTransactionAsync(ct);
        // Rows arrive in dependency order, but self-references within a table may not.
        await ExecuteAsync(transaction, "PRAGMA defer_foreign_keys = ON", ct);
        foreach (var table in Enumerable.Reverse(Tables)) await ExecuteAsync(transaction, $"DELETE FROM {TableName(table)}", ct);
        foreach (var table in Tables) {
            await using var input = open(manifest.Tables.Single(t => t.Name == TableName(table)).Payload);
            await ReadTableAsync(table, transaction, input, ct);
        }
        foreach (var sequence in manifest.Sequences) {
            await using var command = Command(transaction, "DELETE FROM sqlite_sequence WHERE name = $name");
            command.Parameters.AddWithValue("$name", sequence.Name);
            await command.ExecuteNonQueryAsync(ct);
            if (!sequence.IsCalled) continue;
            command.CommandText = "INSERT INTO sqlite_sequence (name, seq) VALUES ($name, $seq)";
            command.Parameters.AddWithValue("$seq", sequence.LastValue);
            await command.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }

    private async Task<BackupSequenceDTO[]> ReadSequencesAsync(SqliteTransaction? transaction, CancellationToken ct) {
        var tables = Tables.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        var names = new List<string>();
        await using (var command = Command(transaction, "SELECT name FROM sqlite_master WHERE type = 'table' AND sql LIKE '%AUTOINCREMENT%'"))
        await using (var reader = await command.ExecuteReaderAsync(ct)) {
            while (await reader.ReadAsync(ct)) {
                var name = reader.GetString(0);
                if (tables.Contains(name)) names.Add(name);
            }
        }
        var result = new List<BackupSequenceDTO>();
        foreach (var name in names.Order(StringComparer.Ordinal)) {
            await using var command = Command(transaction, "SELECT seq FROM sqlite_sequence WHERE name = $name");
            command.Parameters.AddWithValue("$name", name);
            var value = await command.ExecuteScalarAsync(ct);
            result.Add(value is long seq ? new BackupSequenceDTO(name, seq, true) : new BackupSequenceDTO(name, 0, false));
        }
        return result.ToArray();
    }

    private async Task WriteTableAsync(ITable table, SqliteTransaction transaction, Stream output, CancellationToken ct) {
        await using var command = Command(transaction, $"SELECT {ColumnList(table)} FROM {TableName(table)}");
        await using var reader = await command.ExecuteReaderAsync(ct);
        await using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        var columns = reader.FieldCount;
        while (await reader.ReadAsync(ct)) {
            writer.Write(Row);
            for (var i = 0; i < columns; i++) WriteValue(writer, reader, i);
        }
        writer.Write(EndOfTable);
    }

    private static void WriteValue(BinaryWriter writer, SqliteDataReader reader, int ordinal) {
        if (reader.IsDBNull(ordinal)) {
            writer.Write((byte) ValueTag.Null);
            return;
        }
        // GetFieldType reports the storage class of the current row's value, not the declared column type.
        var type = reader.GetFieldType(ordinal);
        if (type == typeof(long)) {
            writer.Write((byte) ValueTag.Integer);
            writer.Write(reader.GetInt64(ordinal));
        } else if (type == typeof(double)) {
            writer.Write((byte) ValueTag.Real);
            writer.Write(reader.GetDouble(ordinal));
        } else if (type == typeof(string)) {
            writer.Write((byte) ValueTag.Text);
            writer.Write(reader.GetString(ordinal));
        } else {
            var blob = reader.GetFieldValue<byte[]>(ordinal);
            writer.Write((byte) ValueTag.Blob);
            writer.Write7BitEncodedInt(blob.Length);
            writer.Write(blob);
        }
    }

    private async Task ReadTableAsync(ITable table, SqliteTransaction transaction, Stream input, CancellationToken ct) {
        var columns = table.Columns.ToArray();
        await using var command = Command(transaction,
            $"INSERT INTO {TableName(table)} ({ColumnList(table)}) VALUES ({string.Join(", ", columns.Select((_, i) => "$p" + i))})");
        var parameters = columns.Select((_, i) => command.Parameters.Add(new SqliteParameter("$p" + i, null))).ToArray();
        using var reader = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);
        while (ReadMarker(reader) == Row) {
            foreach (var parameter in parameters) parameter.Value = ReadValue(reader);
            await command.ExecuteNonQueryAsync(ct);
        }
    }

    private static byte ReadMarker(BinaryReader reader) => reader.ReadByte() switch {
        Row => Row,
        EndOfTable => EndOfTable,
        _ => throw new InvalidDataException("Malformed SQLite table payload.")
    };

    private static object ReadValue(BinaryReader reader) => (ValueTag) reader.ReadByte() switch {
        ValueTag.Null => DBNull.Value,
        ValueTag.Integer => reader.ReadInt64(),
        ValueTag.Real => reader.ReadDouble(),
        ValueTag.Text => reader.ReadString(),
        ValueTag.Blob => ReadBlob(reader),
        _ => throw new InvalidDataException("Malformed SQLite table payload.")
    };

    private static byte[] ReadBlob(BinaryReader reader) {
        var length = reader.Read7BitEncodedInt();
        var blob = length >= 0 ? reader.ReadBytes(length) : [];
        if (blob.Length != length) throw new InvalidDataException("Truncated SQLite table payload.");
        return blob;
    }

    private static string ColumnList(ITable table) => string.Join(", ", table.Columns.Select(c => Quote(c.Name)));

    private SqliteCommand Command(SqliteTransaction? transaction, string sql) =>
        new(sql, Connection, transaction);

    private async Task ExecuteAsync(SqliteTransaction transaction, string sql, CancellationToken ct) {
        await using var command = Command(transaction, sql);
        await command.ExecuteNonQueryAsync(ct);
    }
}
