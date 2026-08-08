namespace Moongazing.OrionPage;

using System;

/// <summary>
/// Thrown when a cursor cannot be used: it is malformed, was tampered with, or no longer matches the
/// query's sort order (its key count differs from the ordering). Callers should treat it as a client
/// error — the Wave 3 web binding maps it to a <c>400</c> <c>problem+json</c> rather than a 500.
/// </summary>
public sealed class InvalidCursorException : Exception
{
    /// <summary>Create the exception with a human-readable <paramref name="message"/>.</summary>
    /// <param name="message">What was wrong with the cursor.</param>
    public InvalidCursorException(string message)
        : base(message)
    {
    }

    /// <summary>Create the exception wrapping the underlying parse failure.</summary>
    /// <param name="message">What was wrong with the cursor.</param>
    /// <param name="innerException">The underlying failure.</param>
    public InvalidCursorException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
