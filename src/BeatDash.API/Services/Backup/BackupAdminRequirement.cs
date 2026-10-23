using Microsoft.AspNetCore.Authorization;

namespace Shiron.BeatDash.API.Services.Backup;

/// <summary>Requires a current administrator and a dedicated backup session.</summary>
public sealed class BackupAdminRequirement : IAuthorizationRequirement;
