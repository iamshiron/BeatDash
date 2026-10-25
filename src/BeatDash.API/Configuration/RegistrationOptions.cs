namespace Shiron.BeatDash.API.Configuration;

/// <summary>Controls public account creation. Changes require a server restart.</summary>
public sealed class RegistrationOptions {
    /// <summary>When true, the registration API route is not mapped.</summary>
    public bool DisableSignup { get; set; }
}
