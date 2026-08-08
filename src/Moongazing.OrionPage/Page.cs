namespace Moongazing.OrionPage;

using System.Collections.Generic;

/// <summary>
/// One page of a keyset-paginated result: the rows, an opaque forward cursor for the next page, and
/// whether more rows exist. Deliberately carries no total count — an unbounded <c>COUNT(*)</c> is
/// itself an O(n) cost most infinite-scroll UIs do not need; <see cref="HasMore"/> covers "is there a
/// next page?" without it.
/// </summary>
/// <typeparam name="T">The row type.</typeparam>
public sealed class Page<T>
{
    /// <summary>Create a page.</summary>
    /// <param name="items">The rows in this page (already trimmed to the page size).</param>
    /// <param name="nextCursor">The opaque cursor to fetch the next page, or null when <paramref name="hasMore"/> is false.</param>
    /// <param name="hasMore">Whether at least one more row exists after this page.</param>
    public Page(IReadOnlyList<T> items, string? nextCursor, bool hasMore)
    {
        System.ArgumentNullException.ThrowIfNull(items);
        Items = items;
        NextCursor = nextCursor;
        HasMore = hasMore;
    }

    /// <summary>The rows in this page.</summary>
    public IReadOnlyList<T> Items { get; }

    /// <summary>The opaque cursor for the next page, or null when there is no next page.</summary>
    public string? NextCursor { get; }

    /// <summary>Whether at least one more row exists after this page.</summary>
    public bool HasMore { get; }

    /// <summary>The number of rows in this page.</summary>
    public int Count => Items.Count;
}
