namespace Shiron.BeatDash.API.Endpoints;

/// <summary>The server status and generated release version.</summary>
/// <param name="Message">The server's availability message.</param>
/// <param name="Version">The backend/web release's semantic version.</param>
public sealed record StatusDto(string Message, string Version);
