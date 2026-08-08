// NativeAOT smoke test for the OrionPage core. Publishing this with PublishAot=true must produce
// zero trim/AOT warnings, and running it must exit 0 - that pair is the core's AOT exit criterion.
// The keyset predicate is BUILT but never Compile()'d here: expression construction is AOT-clean,
// expression compilation is not (it lives in the EF package, which is not AOT-published).
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

using Moongazing.OrionPage;
using Moongazing.OrionPage.Diagnostics;

// Cursor codec round-trips.
var segments = new[] { "2026-07-29T10:20:30.0000000Z", "12345", "value" };
var token = Cursor.Encode(segments);
Check(Cursor.TryDecode(token, out var decoded) && decoded.Count == 3 && decoded[0] == segments[0], "cursor round-trip failed");
Check(!Cursor.TryDecode("!!!not-base64!!!", out _), "malformed cursor should decode to false");

// Predicate builder constructs a lambda (no Compile — that is not AOT-clean). It also validates and
// formats key values internally, so this exercises the value codec through the public surface.
Expression<Func<Row, DateTime>> created = r => r.CreatedUtc;
Expression<Func<Row, long>> id = r => r.Id;
var keys = new List<KeysetSortKey> { new(created, descending: true), new(id, descending: true) };
var predicate = KeysetPredicateBuilder.BuildAfter<Row>(keys, new object?[] { DateTime.UnixEpoch, 42L });
Check(predicate.Body is not null && predicate.Parameters.Count == 1, "predicate build failed");

// Page + telemetry.
var page = new Page<Row>(new[] { new Row { Id = 1, CreatedUtc = DateTime.UnixEpoch } }, nextCursor: token, hasMore: true);
Check(page.Count == 1 && page.HasMore && page.NextCursor == token, "page shape wrong");

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

internal sealed class Row
{
    public long Id { get; set; }

    public DateTime CreatedUtc { get; set; }
}
