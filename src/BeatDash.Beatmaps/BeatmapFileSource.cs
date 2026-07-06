using System.IO.Compression;

namespace Shiron.BeatDash.Beatmaps;

/// <summary>
/// Abstracts the container a custom level is read from (a zip in object storage
/// or a folder on disk) so the parser is transport-agnostic. Files are matched
/// by their base name, case-insensitively.
/// </summary>
public interface IBeatmapFileSource {
    /// <summary>A human label for the source (used only for the level's folder name).</summary>
    string Name { get; }

    /// <summary>Reads a file's bytes by base name (case-insensitive), or null if absent.</summary>
    byte[]? TryReadFile(string filename);
}

/// <summary>Reads a custom level from a zip archive (e.g. a BeatSaver map download).</summary>
public sealed class ZipBeatmapFileSource : IBeatmapFileSource, IDisposable {
    private readonly ZipArchive _archive;

    public string Name { get; }

    public ZipBeatmapFileSource(Stream zipStream, string name, bool leaveOpen = false) {
        _archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen);
        Name = name;
    }

    public byte[]? TryReadFile(string filename) {
        foreach (var entry in _archive.Entries) {
            if (string.Equals(Path.GetFileName(entry.FullName), filename, StringComparison.OrdinalIgnoreCase)) {
                using var stream = entry.Open();
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                return ms.ToArray();
            }
        }
        return null;
    }

    public void Dispose() => _archive.Dispose();
}

/// <summary>Reads a custom level from an extracted folder on disk.</summary>
public sealed class DirectoryBeatmapFileSource : IBeatmapFileSource {
    private readonly string _dir;

    public string Name { get; }

    public DirectoryBeatmapFileSource(string directory) {
        _dir = directory;
        Name = new DirectoryInfo(directory.TrimEnd(Path.DirectorySeparatorChar)).Name;
    }

    public byte[]? TryReadFile(string filename) {
        foreach (var path in Directory.EnumerateFiles(_dir)) {
            if (string.Equals(Path.GetFileName(path), filename, StringComparison.OrdinalIgnoreCase)) {
                return File.ReadAllBytes(path);
            }
        }
        return null;
    }
}
