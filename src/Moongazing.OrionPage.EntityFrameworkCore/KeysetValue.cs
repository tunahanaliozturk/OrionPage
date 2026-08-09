namespace Moongazing.OrionPage.EntityFrameworkCore;

using System;
using System.Globalization;

/// <summary>
/// Formats and parses sort-key values for the cursor, invariantly and round-trippably, without
/// reflection — so the cursor codec is NativeAOT- and trimming-clean. Only cleanly, unambiguously
/// ordered key types are supported in this release; string, <see cref="Guid"/>, and nullable sort
/// keys (whose ordering is collation- or provider-dependent) are hardened in a later wave.
/// </summary>
internal static class KeysetValue
{
    /// <summary>Whether <paramref name="type"/> (or its non-nullable underlying type) is a supported sort-key type.</summary>
    public static bool IsSupported(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        return t == typeof(sbyte) || t == typeof(byte)
            || t == typeof(short) || t == typeof(ushort)
            || t == typeof(int) || t == typeof(uint)
            || t == typeof(long) || t == typeof(ulong)
            || t == typeof(float) || t == typeof(double) || t == typeof(decimal)
            || t == typeof(DateTime) || t == typeof(DateTimeOffset)
            || t == typeof(DateOnly) || t == typeof(TimeOnly);
    }

    /// <summary>Throw a clear error if <paramref name="type"/> is not a supported sort-key type.</summary>
    public static void EnsureSupported(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (!IsSupported(type))
        {
            throw Unsupported(type);
        }
    }

    /// <summary>Format <paramref name="value"/> to an invariant, round-trippable string.</summary>
    public static string Format(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value switch
        {
            sbyte v => v.ToString(CultureInfo.InvariantCulture),
            byte v => v.ToString(CultureInfo.InvariantCulture),
            short v => v.ToString(CultureInfo.InvariantCulture),
            ushort v => v.ToString(CultureInfo.InvariantCulture),
            int v => v.ToString(CultureInfo.InvariantCulture),
            uint v => v.ToString(CultureInfo.InvariantCulture),
            long v => v.ToString(CultureInfo.InvariantCulture),
            ulong v => v.ToString(CultureInfo.InvariantCulture),
            float v => v.ToString("R", CultureInfo.InvariantCulture),
            double v => v.ToString("R", CultureInfo.InvariantCulture),
            decimal v => v.ToString(CultureInfo.InvariantCulture),
            DateTime v => v.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset v => v.ToString("O", CultureInfo.InvariantCulture),
            DateOnly v => v.ToString("O", CultureInfo.InvariantCulture),
            TimeOnly v => v.ToString("O", CultureInfo.InvariantCulture),
            _ => throw Unsupported(value.GetType()),
        };
    }

    /// <summary>Parse <paramref name="text"/> back to a value of <paramref name="type"/> (or its underlying type).</summary>
    public static object Parse(string text, Type type)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(type);
        var t = Nullable.GetUnderlyingType(type) ?? type;
        var c = CultureInfo.InvariantCulture;

        if (t == typeof(sbyte)) { return sbyte.Parse(text, c); }
        if (t == typeof(byte)) { return byte.Parse(text, c); }
        if (t == typeof(short)) { return short.Parse(text, c); }
        if (t == typeof(ushort)) { return ushort.Parse(text, c); }
        if (t == typeof(int)) { return int.Parse(text, c); }
        if (t == typeof(uint)) { return uint.Parse(text, c); }
        if (t == typeof(long)) { return long.Parse(text, c); }
        if (t == typeof(ulong)) { return ulong.Parse(text, c); }
        if (t == typeof(float)) { return float.Parse(text, NumberStyles.Float, c); }
        if (t == typeof(double)) { return double.Parse(text, NumberStyles.Float, c); }
        if (t == typeof(decimal)) { return decimal.Parse(text, NumberStyles.Number, c); }
        if (t == typeof(DateTime)) { return DateTime.Parse(text, c, DateTimeStyles.RoundtripKind); }
        if (t == typeof(DateTimeOffset)) { return DateTimeOffset.Parse(text, c, DateTimeStyles.RoundtripKind); }
        if (t == typeof(DateOnly)) { return DateOnly.Parse(text, c); }
        if (t == typeof(TimeOnly)) { return TimeOnly.Parse(text, c); }

        throw Unsupported(type);
    }

    private static NotSupportedException Unsupported(Type type) =>
        new($"OrionPage cannot use '{type}' as a sort key. Supported key types are the integer, floating-point, " +
            "decimal, and date/time types. Use a numeric or temporal tie-breaker (e.g. a long Id); string/Guid/nullable " +
            "keys are supported in a later release.");
}
