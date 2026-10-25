using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Shiron.BeatDash.DB;

/// <summary>Case-insensitive text search translated natively by each database provider.</summary>
public static class SearchQueryableExtensions {
    private static readonly MethodInfo ILikeMethod = typeof(NpgsqlDbFunctionsExtensions).GetMethod(
        nameof(NpgsqlDbFunctionsExtensions.ILike), [typeof(DbFunctions), typeof(string), typeof(string)])!;
    private static readonly MethodInfo LikeMethod = typeof(DbFunctionsExtensions).GetMethod(
        nameof(DbFunctionsExtensions.Like), [typeof(DbFunctions), typeof(string), typeof(string)])!;

    /// <summary>
    /// Keeps rows where any of <paramref name="columns"/> matches the LIKE <paramref name="pattern"/>,
    /// ignoring case: PostgreSQL uses <c>ILIKE</c>; SQLite's <c>LIKE</c> already ignores ASCII case.
    /// </summary>
    public static IQueryable<T> WhereAnyLikeIgnoreCase<T>(this IQueryable<T> query, DatabaseFacade database, string pattern,
        params Expression<Func<T, string?>>[] columns) {
        var like = database.IsNpgsql() ? ILikeMethod : LikeMethod;
        var row = Expression.Parameter(typeof(T), "row");
        // A captured variable, unlike a constant, is sent as a parameter and keeps the query plan cacheable.
        Expression<Func<string>> patternParameter = () => pattern;
        var functions = Expression.Property(null, typeof(EF), nameof(EF.Functions));
        var predicate = columns
            .Select(column => (Expression) Expression.Call(like, functions, Expression.Invoke(column, row), patternParameter.Body))
            .Aggregate(Expression.OrElse);
        return query.Where(Expression.Lambda<Func<T, bool>>(predicate, row));
    }
}
