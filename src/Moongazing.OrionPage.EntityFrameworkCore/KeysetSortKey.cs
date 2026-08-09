namespace Moongazing.OrionPage.EntityFrameworkCore;

using System;
using System.Linq.Expressions;

/// <summary>
/// One key in a keyset ordering: the member the query is sorted by and its direction. The sort keys,
/// in order, form the tuple that both the cursor encodes and the keyset predicate compares against.
/// </summary>
public sealed class KeysetSortKey
{
    /// <summary>Create a sort key.</summary>
    /// <param name="selector">The key selector, a <c>Func&lt;TEntity, TKey&gt;</c> lambda.</param>
    /// <param name="descending">Whether the query is ordered descending on this key.</param>
    public KeysetSortKey(LambdaExpression selector, bool descending)
    {
        ArgumentNullException.ThrowIfNull(selector);
        if (selector.Parameters.Count != 1)
        {
            throw new ArgumentException("A key selector must take exactly one parameter.", nameof(selector));
        }
        Selector = selector;
        Descending = descending;
    }

    /// <summary>The key selector lambda.</summary>
    public LambdaExpression Selector { get; }

    /// <summary>Whether the ordering on this key is descending.</summary>
    public bool Descending { get; }

    /// <summary>The CLR type the selector returns (the key type).</summary>
    public Type KeyType => Selector.Body.Type;
}
