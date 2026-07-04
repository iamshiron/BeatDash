namespace Shiron.BeatDash.API;

/// <summary>
/// A paginated result wrapper for list endpoints.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
public sealed record PagedResult<T>(
    IList<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
) {
    public static PagedResult<T> Empty(int page, int pageSize) => new([], 0, page, pageSize, 0);
}
