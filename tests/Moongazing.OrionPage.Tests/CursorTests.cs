namespace Moongazing.OrionPage.Tests;

using System;

using Xunit;

/// <summary>Unit tests for the framework-free cursor codec (the AOT-clean core surface).</summary>
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
        // Length prefix 0x7FFFFFFF followed by no data: a naive offset+length bounds check overflows
        // to negative and would slip past — this must decode to false, not throw.
        var tampered = new byte[] { 0x7F, 0xFF, 0xFF, 0xFF };
        var token = Convert.ToBase64String(tampered).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Assert.False(Cursor.TryDecode(token, out _));
    }
}
