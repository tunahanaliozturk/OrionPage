// NativeAOT smoke test for the OrionPage core. Publishing this with PublishAot=true must produce
// zero trim/AOT warnings, and running it must exit 0 - that pair is the core's AOT exit criterion.
// The core is the AOT-clean surface: Page, the cursor codec, and telemetry. The keyset predicate
// builder lives in OrionPage.EntityFrameworkCore (it uses operator-method reflection that NativeAOT
// trims, and it only ever feeds EF's SQL translation), so it is not exercised or claimed here.
using System;

using Moongazing.OrionPage;
using Moongazing.OrionPage.Diagnostics;

// Cursor codec round-trips and rejects malformed input.
var segments = new[] { "2026-07-29T10:20:30.0000000Z", "12345", "value" };
var token = Cursor.Encode(segments);
Check(Cursor.TryDecode(token, out var decoded) && decoded.Count == 3 && decoded[0] == segments[0], "cursor round-trip failed");
Check(!Cursor.TryDecode("!!!not-base64!!!", out _), "malformed cursor should decode to false");

// Page shape.
var items = new[] { "a", "b" };
var page = new Page<string>(items, nextCursor: token, hasMore: true);
Check(page.Count == 2 && page.HasMore && page.NextCursor == token, "page shape wrong");

// Options validate.
var options = new PageOptions { DefaultPageSize = 20, MaxPageSize = 100 };
options.Validate();

// Telemetry surface.
using var diagnostics = new PageDiagnostics();
Check(diagnostics.ActivitySource.Name == PageDiagnostics.SourceName, "activity source name wrong");
using (var activity = diagnostics.StartKeyset(20))
{
    PageDiagnostics.SetHasMore(activity, true); // no listener attached; must not throw
}

Console.WriteLine("OrionPage AOT smoke test passed.");
return 0;

static void Check(bool condition, string message)
{
    if (!condition)
    {
        Console.Error.WriteLine($"AOT smoke test failed: {message}");
        Environment.Exit(1);
    }
}
