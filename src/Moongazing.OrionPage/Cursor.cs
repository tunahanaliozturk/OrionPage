namespace Moongazing.OrionPage;

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// The opaque cursor codec: encodes the sort-key tuple of the last row on a page into a compact,
/// URL-safe token, and decodes it back. The encoding is a hand-written, reflection-free,
/// length-prefixed binary format (base64url) — no JSON, no reflection — so it is NativeAOT- and
/// trimming-clean. The token is opaque to clients; HMAC signing (so a client cannot tamper the
/// encoded predicate) is added in a later wave.
/// </summary>
public static class Cursor
{
    /// <summary>Encode the formatted sort-key segments of a row into an opaque cursor token.</summary>
    /// <param name="segments">The invariant-formatted sort-key values, in sort order.</param>
    /// <returns>A URL-safe opaque cursor.</returns>
    public static string Encode(IReadOnlyList<string> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        using var buffer = new MemoryStream();
        Span<byte> lengthPrefix = stackalloc byte[4];
        foreach (var segment in segments)
        {
            var bytes = Encoding.UTF8.GetBytes(segment ?? string.Empty);
            BinaryPrimitives.WriteInt32BigEndian(lengthPrefix, bytes.Length);
            buffer.Write(lengthPrefix);
            buffer.Write(bytes, 0, bytes.Length);
        }
        return ToBase64Url(buffer.ToArray());
    }

    /// <summary>Decode an opaque cursor token back into its sort-key segments.</summary>
    /// <param name="cursor">The token produced by <see cref="Encode"/>.</param>
    /// <param name="segments">The decoded segments on success.</param>
    /// <returns>True if the token was well-formed; false if it was malformed or tampered.</returns>
    public static bool TryDecode(string? cursor, out IReadOnlyList<string> segments)
    {
        segments = Array.Empty<string>();
        if (string.IsNullOrEmpty(cursor))
        {
            return false;
        }

        byte[] data;
        try
        {
            data = FromBase64Url(cursor);
        }
        catch (FormatException)
        {
            return false;
        }

        var result = new List<string>();
        var offset = 0;
        while (offset < data.Length)
        {
            if (offset + 4 > data.Length)
            {
                return false; // truncated length prefix
            }
            var length = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset, 4));
            offset += 4;
            // Compare against remaining space (no `offset + length`, which overflows for a tampered
            // length near int.MaxValue and would slip past the bounds check).
            if (length < 0 || length > data.Length - offset)
            {
                return false; // length points past the buffer → tampered / corrupt
            }
            result.Add(Encoding.UTF8.GetString(data, offset, length));
            offset += length;
        }

        segments = result;
        return true;
    }

    private static string ToBase64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
            case 1: throw new FormatException("Invalid base64url length.");
            default: break;
        }
        return Convert.FromBase64String(s);
    }
}
