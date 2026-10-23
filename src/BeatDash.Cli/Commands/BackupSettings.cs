using System.ComponentModel;
using Shiron.BeatDash.Data.Backup;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shiron.BeatDash.Cli.Commands;

/// <summary>Connection and scope options shared by backup commands.</summary>
public class BackupSettings : CommandSettings {
    /// <summary>Backend address; remote connections require HTTPS.</summary>
    [CommandOption("--host")]
    [Description("BeatDash API URL (HTTPS required except on loopback)")]
    public string Host { get; init; } = "http://localhost:1811";

    /// <summary>Administrator login email.</summary>
    [CommandOption("--email")]
    [Description("Administrator email (or BEATDASH_ADMIN_EMAIL)")]
    public string? Email { get; init; }

    /// <summary>Environment variable holding the password for unattended operation.</summary>
    [CommandOption("--password-env")]
    [Description("Password environment variable; otherwise prompt securely")]
    public string PasswordEnvironmentVariable { get; init; } = "BEATDASH_ADMIN_PASSWORD";

    /// <summary>Environment variable holding an authenticator code when two-factor authentication is enabled.</summary>
    [CommandOption("--two-factor-env")]
    [Description("Authenticator code environment variable (optional)")]
    public string TwoFactorEnvironmentVariable { get; init; } = "BEATDASH_ADMIN_TWO_FACTOR_CODE";

    /// <summary>Components included in this operation.</summary>
    [CommandOption("--scope")]
    [Description("Database, Storage, or Both (default: Both)")]
    public BackupScope Scope { get; init; } = BackupScope.Both;

    /// <inheritdoc/>
    public override ValidationResult Validate() {
        if (!Uri.TryCreate(Host, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)
            || !string.IsNullOrEmpty(uri.UserInfo))
            return ValidationResult.Error("Use an HTTPS backend URL, or HTTP on loopback.");
        return Enum.IsDefined(Scope) ? ValidationResult.Success() : ValidationResult.Error("Invalid backup scope.");
    }
}
