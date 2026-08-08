namespace Moongazing.OrionPage.Diagnostics;

using System.Diagnostics;

using Moongazing.Orion.Abstractions.Diagnostics;

/// <summary>
/// OpenTelemetry instrumentation for keyset pagination. Built on the Orion family's
/// <see cref="OrionInstrumentation"/> spine: an <see cref="ActivitySource"/> named
/// <c>Moongazing.OrionPage</c> emitting a <c>OrionPage.keyset</c> span per page with the attributes
/// <c>orion.page.size</c> (the requested page size) and <c>orion.page.has_more</c> (whether a next
/// page exists). A process-wide <see cref="Shared"/> instance makes telemetry emit by default.
/// </summary>
public sealed class PageDiagnostics : OrionInstrumentation
{
    /// <summary>The activity-source name OpenTelemetry consumers subscribe to.</summary>
    public const string SourceName = "Moongazing.OrionPage";

    /// <summary>The activity name of the span covering one page fetch.</summary>
    public const string KeysetActivityName = "OrionPage.keyset";

    /// <summary>The span attribute carrying the requested page size.</summary>
    public const string SizeAttribute = "orion.page.size";

    /// <summary>The span attribute carrying whether a next page exists.</summary>
    public const string HasMoreAttribute = "orion.page.has_more";

    private static readonly System.Lazy<PageDiagnostics> SharedInstance =
        new(static () => new PageDiagnostics());

    /// <summary>Create the instrumentation surface.</summary>
    public PageDiagnostics()
        : base(OrionTelemetry.ScopeName("OrionPage"), MeterVersion.Value)
    {
    }

    /// <summary>The process-wide default instance, so telemetry emits without explicit wiring.</summary>
    public static PageDiagnostics Shared => SharedInstance.Value;

    /// <summary>Start the span covering one page fetch, tagged with the requested size, or null when nothing listens.</summary>
    /// <param name="pageSize">The requested page size.</param>
    /// <returns>The started <see cref="Activity"/>, or null.</returns>
    public Activity? StartKeyset(int pageSize)
    {
        var activity = ActivitySource.StartActivity(KeysetActivityName, ActivityKind.Internal);
        activity?.SetTag(SizeAttribute, pageSize);
        return activity;
    }

    /// <summary>Record on <paramref name="activity"/> whether a next page exists.</summary>
    /// <param name="activity">The span from <see cref="StartKeyset"/>, or null.</param>
    /// <param name="hasMore">Whether a next page exists.</param>
    public static void SetHasMore(Activity? activity, bool hasMore)
    {
        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag(HasMoreAttribute, hasMore);
        }
    }
}
