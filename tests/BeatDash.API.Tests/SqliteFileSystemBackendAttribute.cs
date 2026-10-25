using System.Reflection;
using Xunit.Sdk;

namespace Shiron.BeatDash.API.Tests;

/// <summary>Runs a theory against an SQLite file and file-system storage in a temporary directory.</summary>
public sealed class SqliteFileSystemBackendAttribute : DataAttribute {
    /// <inheritdoc/>
    public override IEnumerable<object[]> GetData(MethodInfo testMethod) => [[TestBackend.SqliteFileSystem]];
}
