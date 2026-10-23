using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Shiron.BeatDash.Data.Backup;

namespace Shiron.BeatDash.API.Services.Backup;

/// <summary>Validates an entire archive before any application data is changed.</summary>
public static class BackupArchive {
    /// <summary>The supported manifest format.</summary>
    public const int FormatVersion = 1;
    internal const string ManifestEntry = "manifest.json";

    /// <summary>Rejects incomplete, corrupt, oversized, or ambiguous archives.</summary>
    public static async Task<BackupManifest> ValidateAsync(ZipArchive archive, long maxBytes, CancellationToken ct) {
        if (archive.Entries.Select(e => e.FullName).Distinct(StringComparer.Ordinal).Count() != archive.Entries.Count)
            throw new InvalidDataException("Duplicate archive entries.");
        var entry = archive.GetEntry(ManifestEntry) ?? throw new InvalidDataException("Missing backup manifest.");
        if (entry.Length > 32 * 1024 * 1024) throw new InvalidDataException("Manifest is too large.");
        await using var stream = entry.Open();
        var manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(stream, cancellationToken: ct)
            ?? throw new InvalidDataException("Invalid backup manifest.");
        if (manifest.FormatVersion != FormatVersion || !Enum.IsDefined(manifest.Scope)
            || manifest.Tables is null || manifest.Objects is null || manifest.Sequences is null)
            throw new InvalidDataException("Unsupported backup manifest.");
        if (manifest.Scope == BackupScope.Storage && (manifest.Tables.Length != 0 || manifest.Sequences.Length != 0)
            || manifest.Scope == BackupScope.Database && manifest.Objects.Length != 0)
            throw new InvalidDataException("Inventory does not match the archive scope.");
        if (manifest.Tables.Any(t => t is null || string.IsNullOrEmpty(t.Name) || t.Payload is null)
            || manifest.Objects.Any(o => o is null || o.Bucket is not ("assets" or "user-data")
                || string.IsNullOrEmpty(o.Key) || string.IsNullOrEmpty(o.ContentType) || o.Payload is null)
            || manifest.Sequences.Any(s => s is null || string.IsNullOrEmpty(s.Name)))
            throw new InvalidDataException("Invalid inventory item.");
        if (manifest.Tables.Select(t => t.Name).Distinct().Count() != manifest.Tables.Length
            || manifest.Objects.Select(o => (o.Bucket, o.Key)).Distinct().Count() != manifest.Objects.Length
            || manifest.Sequences.Select(s => s.Name).Distinct().Count() != manifest.Sequences.Length)
            throw new InvalidDataException("Duplicate inventory items.");
        var payloads = manifest.Tables.Select(t => t.Payload).Concat(manifest.Objects.Select(o => o.Payload)).ToArray();
        if (payloads.Select(p => p.Entry).Distinct().Count() != payloads.Length || archive.Entries.Count != payloads.Length + 1)
            throw new InvalidDataException("Archive inventory does not match its entries.");
        long total = entry.Length;
        foreach (var payload in payloads) {
            if (string.IsNullOrEmpty(payload.Entry) || !payload.Entry.StartsWith("payload/", StringComparison.Ordinal)
                || payload.Length < 0 || payload.Length > maxBytes - total)
                throw new InvalidDataException("Invalid payload or archive size limit exceeded.");
            total += payload.Length;
            var content = archive.GetEntry(payload.Entry) ?? throw new InvalidDataException("Missing payload.");
            if (content.Length != payload.Length) throw new InvalidDataException("Payload size mismatch.");
            await using var input = content.Open();
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(input, ct));
            if (!string.Equals(hash, payload.Sha256, StringComparison.Ordinal))
                throw new InvalidDataException("Payload checksum mismatch.");
        }
        return manifest;
    }
}
