using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shiron.BeatDash.Data.Backup;
using Shiron.BeatDash.DB;

namespace Shiron.BeatDash.API.Services.Backup;

/// <summary>Complete, schema-bound database snapshots, executed exclusively inside the backend.</summary>
public abstract class BackupDatabase(BeatDashDbContext db) {
    /// <summary>The context whose connection snapshots are read from and restored into.</summary>
    protected BeatDashDbContext Db { get; } = db;

    /// <summary>The engine whose native payload format this implementation reads and writes.</summary>
    public abstract BackupDatabaseEngine Engine { get; }

    internal ITable[] Tables {
        get {
            var remaining = Db.Model.GetRelationalModel().Tables.ToList();
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
    internal string SchemaHash => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Db.Database.GenerateCreateScript())));

    /// <summary>The archive and SQL name of a table.</summary>
    internal abstract string TableName(ITable table);

    /// <summary>The server major version payloads are bound to, or 0 if they are version independent.</summary>
    internal abstract Task<int> GetMajorVersionAsync(CancellationToken ct);

    /// <summary>The engine's identifier generators, in ordinal name order.</summary>
    internal abstract Task<BackupSequenceDTO[]> GetSequencesAsync(CancellationToken ct);

    /// <summary>Writes every table from one consistent snapshot through <paramref name="addTable"/>, which receives the table name and a payload writer.</summary>
    internal abstract Task<BackupSequenceDTO[]> ExportAsync(Func<string, Func<Stream, Task>, Task> addTable, CancellationToken ct);

    /// <summary>Atomically replaces every table and sequence with the archive's content.</summary>
    internal abstract Task RestoreAsync(BackupManifest manifest, Func<BackupPayloadDTO, Stream> open, CancellationToken ct);
}
