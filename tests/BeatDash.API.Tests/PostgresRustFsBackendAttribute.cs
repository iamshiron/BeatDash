using System.Reflection;
using Xunit.Sdk;

namespace Shiron.BeatDash.API.Tests;

/// <summary>Runs a theory against PostgreSQL and RustFS, or skips it when that infrastructure isn't configured.</summary>
public sealed class PostgresRustFsBackendAttribute : DataAttribute {
    public PostgresRustFsBackendAttribute() {
        if (Environment.GetEnvironmentVariable("BEATDASH_TEST_POSTGRES") is null
            || Environment.GetEnvironmentVariable("BEATDASH_TEST_RUSTFS") is null)
            Skip = "Set BEATDASH_TEST_POSTGRES and BEATDASH_TEST_RUSTFS to disposable test infrastructure.";
    }

    /// <inheritdoc/>
    public override IEnumerable<object[]> GetData(MethodInfo testMethod) => [[TestBackend.PostgresRustFs]];
}
