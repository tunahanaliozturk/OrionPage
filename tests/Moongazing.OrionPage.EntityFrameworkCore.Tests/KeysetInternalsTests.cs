namespace Moongazing.OrionPage.EntityFrameworkCore.Tests;

using System;
using System.Collections.Generic;
using System.Linq.Expressions;

using Moongazing.OrionPage.EntityFrameworkCore;

using Xunit;

/// <summary>
/// Unit tests for the keyset value codec and predicate builder. These live in the EF test project
/// (not the AOT-clean core tests) because the predicate builder uses operator-method reflection that
/// NativeAOT trims — it is an EF-layer concern that only ever feeds EF's SQL translation.
/// </summary>
public sealed class KeysetInternalsTests
{
    [Fact]
    public void Keyset_value_round_trips_supported_types()
    {
        AssertRoundTrip(42L);
        AssertRoundTrip(-7);
        AssertRoundTrip(3.14159d);
        AssertRoundTrip(2.5m);
        AssertRoundTrip(new DateTime(2026, 7, 29, 10, 20, 30, DateTimeKind.Utc));
        AssertRoundTrip(new DateTimeOffset(2026, 7, 29, 10, 20, 30, TimeSpan.FromHours(3)));
        AssertRoundTrip(new DateOnly(2026, 7, 29));
    }

    private static void AssertRoundTrip<T>(T value)
        where T : notnull
    {
        var text = KeysetValue.Format(value);
        var parsed = KeysetValue.Parse(text, typeof(T));
        Assert.Equal(value, (T)parsed);
    }

    [Fact]
    public void Unsupported_key_types_are_rejected_with_guidance()
    {
        var ex = Assert.Throws<NotSupportedException>(() => KeysetValue.EnsureSupported(typeof(Guid)));
        Assert.Contains("Guid", ex.Message);
    }

    [Fact]
    public void Predicate_builder_produces_a_descending_tuple_comparison()
    {
        Expression<Func<Row, DateTime>> created = r => r.CreatedUtc;
        Expression<Func<Row, int>> id = r => r.Id;
        var keys = new List<KeysetSortKey>
        {
            new(created, descending: true),
            new(id, descending: true),
        };
        var t = new DateTime(2026, 7, 29, 0, 0, 0, DateTimeKind.Utc);
        var predicate = KeysetPredicateBuilder.BuildAfter<Row>(keys, new object?[] { t, 100 });
        var test = predicate.Compile();

        Assert.True(test(new Row { CreatedUtc = t.AddMinutes(-1), Id = 999 }));  // earlier time -> after (desc)
        Assert.True(test(new Row { CreatedUtc = t, Id = 99 }));                  // same time, smaller id -> after
        Assert.False(test(new Row { CreatedUtc = t, Id = 100 }));               // the cursor row itself
        Assert.False(test(new Row { CreatedUtc = t, Id = 101 }));               // same time, larger id -> before
        Assert.False(test(new Row { CreatedUtc = t.AddMinutes(1), Id = 1 }));   // later time -> before (desc)
    }

    [Fact]
    public void Predicate_builder_rejects_a_cursor_arity_mismatch()
    {
        Expression<Func<Row, int>> id = r => r.Id;
        var keys = new List<KeysetSortKey> { new(id, descending: false) };
        Assert.Throws<ArgumentException>(() => KeysetPredicateBuilder.BuildAfter<Row>(keys, new object?[] { 1, 2 }));
    }

    private sealed class Row
    {
        public int Id { get; set; }

        public DateTime CreatedUtc { get; set; }
    }
}
