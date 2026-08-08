namespace Moongazing.OrionPage.DependencyInjection;

using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Moongazing.OrionPage.Diagnostics;

/// <summary>
/// DI wiring for pagination.
/// </summary>
public static class OrionPageServiceCollectionExtensions
{
    /// <summary>
    /// Register pagination options and the shared <see cref="PageDiagnostics"/>. The keyset query
    /// extension (<c>ToKeysetPageAsync</c>, in <c>OrionPage.EntityFrameworkCore</c>) works without any
    /// DI; this registration supplies the <see cref="PageOptions"/> bounds the Wave 3 web binding enforces.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration of the pagination options.</param>
    /// <returns>The same <paramref name="services"/>, for chaining.</returns>
    public static IServiceCollection AddOrionPage(this IServiceCollection services, Action<PageOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<PageOptions>();
        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }
        optionsBuilder.PostConfigure(static o => o.Validate());

        services.TryAddSingleton<PageDiagnostics>();
        return services;
    }
}
