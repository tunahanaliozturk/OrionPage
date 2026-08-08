namespace Moongazing.OrionPage;

using System;
using System.Collections.Generic;
using System.Linq.Expressions;

/// <summary>
/// Builds the "strictly after the cursor" predicate for a keyset ordering: the lexicographic tuple
/// comparison a database can evaluate against the sort columns' index in constant time. For sort keys
/// <c>k1..kn</c> with per-key directions, the predicate is
/// <code>
/// (k1 OP1 v1)
///   OR (k1 = v1 AND k2 OP2 v2)
///   OR (k1 = v1 AND k2 = v2 AND k3 OP3 v3) ...
/// </code>
/// where <c>OPi</c> is <c>&lt;</c> for a descending key and <c>&gt;</c> for an ascending one. This is
/// pure <see cref="Expression"/> construction (no compilation, no reflection emit), so it is
/// NativeAOT- and trimming-clean and translates to SQL rather than running client-side.
/// </summary>
public static class KeysetPredicateBuilder
{
    /// <summary>Build the after-cursor predicate for <paramref name="keys"/> at the cursor <paramref name="values"/>.</summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <param name="keys">The sort keys, primary first.</param>
    /// <param name="values">The cursor's key values, aligned with <paramref name="keys"/>.</param>
    /// <returns>A predicate selecting rows strictly after the cursor in the given ordering.</returns>
    public static Expression<Func<T, bool>> BuildAfter<T>(IReadOnlyList<KeysetSortKey> keys, IReadOnlyList<object?> values)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(values);
        if (keys.Count == 0)
        {
            throw new ArgumentException("A keyset ordering needs at least one sort key.", nameof(keys));
        }
        if (keys.Count != values.Count)
        {
            throw new ArgumentException($"The cursor has {values.Count} value(s) but the ordering has {keys.Count} key(s). The sort order likely changed since the cursor was issued.", nameof(values));
        }

        var parameter = Expression.Parameter(typeof(T), "e");
        var bodies = new Expression[keys.Count];
        var constants = new Expression[keys.Count];
        for (var i = 0; i < keys.Count; i++)
        {
            // Surfaces the supported-types guidance rather than a raw "operator not defined" error.
            KeysetValue.EnsureSupported(keys[i].KeyType);
            bodies[i] = ParameterRebinder.Rebind(keys[i].Selector.Body, keys[i].Selector.Parameters[0], parameter);
            constants[i] = MakeConstant(values[i], bodies[i].Type);
        }

        Expression? predicate = null;
        for (var i = 0; i < keys.Count; i++)
        {
            // term_i: all earlier keys equal, this key strictly past the cursor in its direction.
            Expression? term = null;
            for (var j = 0; j < i; j++)
            {
                var equal = Expression.Equal(bodies[j], constants[j]);
                term = term is null ? equal : Expression.AndAlso(term, equal);
            }
            var comparison = keys[i].Descending
                ? Expression.LessThan(bodies[i], constants[i])
                : Expression.GreaterThan(bodies[i], constants[i]);
            term = term is null ? comparison : Expression.AndAlso(term, comparison);

            predicate = predicate is null ? term : Expression.OrElse(predicate, term);
        }

        return Expression.Lambda<Func<T, bool>>(predicate!, parameter);
    }

    private static Expression MakeConstant(object? value, Type targetType)
    {
        if (value is null)
        {
            return Expression.Constant(null, targetType);
        }
        var constant = Expression.Constant(value);
        return constant.Type == targetType ? constant : Expression.Convert(constant, targetType);
    }

    private sealed class ParameterRebinder : ExpressionVisitor
    {
        private readonly ParameterExpression from;
        private readonly ParameterExpression to;

        private ParameterRebinder(ParameterExpression from, ParameterExpression to)
        {
            this.from = from;
            this.to = to;
        }

        public static Expression Rebind(Expression body, ParameterExpression from, ParameterExpression to) =>
            new ParameterRebinder(from, to).Visit(body);

        protected override Expression VisitParameter(ParameterExpression node) =>
            node == from ? to : base.VisitParameter(node);
    }
}
