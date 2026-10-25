namespace Shiron.BeatDash.API.Tests;

/// <summary>A database and object storage combination an integration test runs against.</summary>
public enum TestBackend {
    /// <summary>The first-class server setup; requires <c>BEATDASH_TEST_POSTGRES</c> and <c>BEATDASH_TEST_RUSTFS</c>.</summary>
    PostgresRustFs,
    /// <summary>The embedded single-machine setup; needs no external infrastructure.</summary>
    SqliteFileSystem
}
