namespace Moongazing.OrionPage.EntityFrameworkCore;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using Moongazing.OrionPage;
using Moongazing.OrionPage.Diagnostics;

/// <summary>
/// The keyset pagination query extension. Reads the query's <c>OrderBy</c>/<c>ThenBy</c> chain, and —
/// on a continuation — appends the tuple-comparison <c>WHERE</c> that selects the rows after the
/// cursor, so deep pages stay constant-time (an index seek) instead of <c>OFFSET</c>'s linear scan.
/// </summary>
public static class KeysetPaginationExtensions
{
    /// <summary>
    /// Fetch one keyset page from an ordered query. Order the query first (with <c>OrderBy</c>/
    /// <c>ThenBy</c> as the final operators, ending in a unique tie-breaker such as <c>Id</c>), then
    /// call this. Pass <paramref name="cursor"/> = null for the first page, then feed each returned
    /// <see cref="Page{T}.NextCursor"/> back in.
    /// </summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <param name="query">The ordered query.</param>
    /// <param name="cursor">The opaque cursor from a previous page, or null for the first page.</param>
    /// <param name="pageSize">The number of rows per page (must be positive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The page of rows plus a forward cursor.</returns>
    /// <exception cref="InvalidCursorException">The cursor is malformed, tampered, or does not match the sort order.</exception>
    public static Task<Page<T>> ToKeysetPageAsync<T>(
        this IOrderedQueryable<T> query,
        string? cursor,
        int pageSize,
        CancellationToken cancellationToken = default)
        => ToKeysetPageAsync(query, cursor, pageSize, diagnostics: null, cancellationToken);

    /// <summary>Fetch one keyset page, reporting through a specific <paramref name="diagnostics"/> instance.</summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <param name="query">The ordered query.</param>
    /// <param name="cursor">The opaque cursor from a previous page, or null for the first page.</param>
    /// <param name="pageSize">The number of rows per page (must be positive).</param>
    /// <param name="diagnostics">Instrumentation; defaults to <see cref="PageDiagnostics.Shared"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The page of rows plus a forward cursor.</returns>
    public static async Task<Page<T>> ToKeysetPageAsync<T>(
        this IOrderedQueryable<T> query,
        string? cursor,
        int pageSize,
        PageDiagnostics? diagnostics,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "pageSize must be positive.");
        }

        var keys = KeysetOrderingParser.Parse(query.Expression);
        diagnostics ??= PageDiagnostics.Shared;
        using var activity = diagnostics.StartKeyset(pageSize);

        IQueryable<T> q = query;
        if (!string.IsNullOrEmpty(cursor))
        {
            var predicate = BuildAfterPredicate<T>(keys, cursor);
            q = q.Where(predicate);
        }

        // Fetch one extra row to learn whether a next page exists, without a second COUNT query.
        var rows = await q.Take(pageSize + 1).ToListAsync(cancellationToken).ConfigureAwait(false);

        var hasMore = rows.Count > pageSize;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        string? nextCursor = null;
        if (hasMore && rows.Count > 0)
        {
            nextCursor = EncodeCursor(keys, rows[^1]);
        }

        PageDiagnostics.SetHasMore(activity, hasMore);
        return new Page<T>(rows, nextCursor, hasMore);
    }

    private static Expression<Func<T, bool>> BuildAfterPredicate<T>(IReadOnlyList<KeysetSortKey> keys, string cursor)
    {
        if (!Cursor.TryDecode(cursor, out var segments))
        {
            throw new InvalidCursorException("The cursor is malformed or was tampered with.");
        }
        if (segments.Count != keys.Count)
        {
            throw new InvalidCursorException(
                $"The cursor has {segments.Count} value(s) but the query is ordered by {keys.Count} key(s); the sort order likely changed since the cursor was issued.");
        }

        var values = new object?[keys.Count];
        for (var i = 0; i < keys.Count; i++)
        {
            try
            {
                values[i] = KeysetValue.Parse(segments[i], keys[i].KeyType);
            }
            catch (FormatException ex)
            {
                throw new InvalidCursorException("The cursor holds a value that does not match the sort key type.", ex);
            }
        }

        return KeysetPredicateBuilder.BuildAfter<T>(keys, values);
    }

    private static string EncodeCursor<T>(IReadOnlyList<KeysetSortKey> keys, T lastRow)
    {
        var segments = new string[keys.Count];
        for (var i = 0; i < keys.Count; i++)
        {
            var extractor = CompileExtractor<T>(keys[i]);
            var value = extractor(lastRow)
                ?? throw new InvalidOperationException(
                    "A null sort-key value cannot be encoded into a cursor in this release (nullable keys are hardened later).");
            segments[i] = KeysetValue.Format(value);
        }
        return Cursor.Encode(segments);
    }

    // Compiling the selector is why this lives in the EF package (not AOT-clean); it runs once per key
    // per page, on the single last row.
    private static Func<T, object?> CompileExtractor<T>(KeysetSortKey key)
    {
        var parameter = key.Selector.Parameters[0];
        var body = Expression.Convert(key.Selector.Body, typeof(object));
        return Expression.Lambda<Func<T, object?>>(body, parameter).Compile();
    }
}
