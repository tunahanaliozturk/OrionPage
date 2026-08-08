namespace Moongazing.OrionPage.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using Xunit;

/// <summary>Unit tests for the framework-free core: cursor codec, value formatting, predicate builder.</summary>
public sealed class CursorTests
{
    [Theory]
    [InlineData("a", "b", "c")]
    [InlineData("", "value with spaces", "unicode: café ☕")]
    [InlineData("2026-07-29T10:20:30.0000000Z", "12345")]
    public void Cursor_round_trips_its_segments(params string[] segments)
    {
        var encoded = Cursor.Encode(segments);
        Assert.True(Cursor.TryDecode(encoded, out var decoded));
        Assert.Equal(segments, decoded);
    }

    [Fact]
    public void Cursor_is_url_safe()
    {
        var encoded = Cursor.Encode(new[] { new string('x', 200), "y/z+w" });
        Assert.DoesNotContain('+', encoded);
        Assert.DoesNotContain('/', encoded);
        Assert.DoesNotContain('=', encoded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("!!!not base64!!!")]
    [InlineData("////")]
    public void Malformed_cursors_decode_to_false(string bad)
    {
        Assert.False(Cursor.TryDecode(bad, out _));
    }

    [Fact]
    public void A_tampered_cursor_with_an_over_long_length_prefix_is_rejected()
    {
        // Craft bytes: length prefix 0x7FFFFFFF followed by no data.
        var tampered = new byte[] { 0x7F, 0xFF, 0xFF, 0xFF };
        var token = Convert.ToBase64String(tampered).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Assert.False(Cursor.TryDecode(token, out _));
    }

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
        // keys: (CreatedUtc desc, Id desc), cursor (t, 100)
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
        Assert.False(test(new Row { CreatedUtc = t, Id = 100 }));               // the cursor row itself -> not after
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
