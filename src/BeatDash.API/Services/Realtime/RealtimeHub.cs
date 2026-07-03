using Microsoft.AspNetCore.SignalR;

namespace Shiron.BeatDash.API.Services.Realtime;

/// <summary>
/// SignalR hub for real-time communication with the web frontend.
/// Each authenticated connection is automatically added to a user-specific group
/// so the backend can target all of a user's browser tabs at once.
/// </summary>
public sealed class RealtimeHub : Hub<IRealtimeClient> {
    private const string GroupPrefix = "user:";

    /// <summary>
    /// Returns the SignalR group name for a given user.
    /// All browser connections belonging to the user are added to this group.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <returns>The group name in the format <c>user:{"{userId}"}</c>.</returns>
    public static string GroupForUser(Guid userId) => $"{GroupPrefix}{userId}";

    /// <inheritdoc/>
    public override async Task OnConnectedAsync() {
        var userId = Context.User is not null ? IdentityUtils.GetUserID(Context.User) : null;
        if (userId.HasValue) {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupForUser(userId.Value));
        }
        await base.OnConnectedAsync();
    }
}
