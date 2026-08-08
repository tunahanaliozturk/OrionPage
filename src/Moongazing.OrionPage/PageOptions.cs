namespace Moongazing.OrionPage;

using System;

/// <summary>
/// Pagination defaults and bounds. The default and maximum page size guard against a client asking
/// for an unbounded page (a denial-of-service vector on any list endpoint). The maximum is enforced
/// by the Wave 3 web binding; a library caller passes the page size explicitly.
/// </summary>
public sealed class PageOptions
{
    /// <summary>The page size used when a caller does not specify one. Defaults to 20. Must be positive.</summary>
    public int DefaultPageSize { get; set; } = 20;

    /// <summary>The largest page size a caller may request. Defaults to 100. Must be positive and ≥ <see cref="DefaultPageSize"/>.</summary>
    public int MaxPageSize { get; set; } = 100;

    /// <summary>Validate the option values, throwing on an unusable configuration.</summary>
    public void Validate()
    {
        if (DefaultPageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(DefaultPageSize), DefaultPageSize, "DefaultPageSize must be positive.");
        }
        if (MaxPageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxPageSize), MaxPageSize, "MaxPageSize must be positive.");
        }
        if (MaxPageSize < DefaultPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxPageSize), MaxPageSize, "MaxPageSize cannot be smaller than DefaultPageSize.");
        }
    }
}
