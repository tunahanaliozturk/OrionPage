namespace Moongazing.OrionPage.EntityFrameworkCore;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using Moongazing.OrionPage;

/// <summary>
/// Reads the trailing <c>OrderBy</c>/<c>OrderByDescending</c>/<c>ThenBy</c>/<c>ThenByDescending</c>
/// chain off an ordered query's expression tree into the sort keys the cursor and predicate need.
/// Because the ordering must be the final operators (the extension takes an
/// <see cref="IOrderedQueryable{T}"/>), the outermost call is the last <c>ThenBy</c>; the walk peels
/// them off and reverses to primary-first order.
/// </summary>
internal static class KeysetOrderingParser
{
    private static readonly HashSet<string> OrderingMethods = new(StringComparer.Ordinal)
    {
        "OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending",
    };

    public static IReadOnlyList<KeysetSortKey> Parse(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var keys = new List<KeysetSortKey>();
        var current = expression;
        while (current is MethodCallExpression call
            && call.Method.DeclaringType == typeof(Queryable)
            && OrderingMethods.Contains(call.Method.Name)
            && call.Arguments.Count == 2)
        {
            var descending = call.Method.Name is "OrderByDescending" or "ThenByDescending";
            keys.Add(new KeysetSortKey(ExtractLambda(call.Arguments[1]), descending));
            current = call.Arguments[0];
        }

        if (keys.Count == 0)
        {
            throw new InvalidOperationException(
                "ToKeysetPageAsync requires the query to be ordered, with OrderBy/ThenBy as the final operators " +
                "(apply any Select before the ordering). The last sort key must be a unique tie-breaker (e.g. Id) " +
                "so the ordering is a stable total order; otherwise keyset paging can skip or repeat rows.");
        }

        keys.Reverse(); // collected outermost-first (last ThenBy) -> reverse to primary-first
        return keys;
    }

    private static LambdaExpression ExtractLambda(Expression argument) => argument switch
    {
        UnaryExpression { NodeType: ExpressionType.Quote, Operand: LambdaExpression lambda } => lambda,
        LambdaExpression lambda => lambda,
        _ => throw new InvalidOperationException("Could not read a key selector from the OrderBy chain."),
    };
}
