# OrionPage.EntityFrameworkCore

Keyset (cursor) pagination for EF Core: `ToKeysetPageAsync` reads your `OrderBy`/`ThenBy` chain, turns the cursor into a tuple-comparison `WHERE`, and returns a `Page<T>` with an opaque forward cursor. Deep pages stay constant-time because there is no `OFFSET`.

![ToKeysetPageAsync flow: pageSize and ordering checks, a null or empty cursor fetches the first page, otherwise the cursor is decoded, checked and parsed (each failure raises InvalidCursorException) before the keyset WHERE is appended; pageSize + 1 rows are fetched and the extra row decides HasMore and NextCursor](https://raw.githubusercontent.com/tunahanaliozturk/OrionPage/master/docs/diagrams/keyset-page.png)

## Install

    dotnet add package OrionPage.EntityFrameworkCore

This brings in the `OrionPage` core (`Page<T>`, `Cursor`, `InvalidCursorException`). Requires EF Core 8 or later.

## Quick start

```csharp
using Moongazing.OrionPage;
using Moongazing.OrionPage.EntityFrameworkCore;

public Task<Page<Order>> ListAsync(string? cursor, CancellationToken ct) =>
    db.Orders
      .OrderByDescending(o => o.CreatedUtc).ThenByDescending(o => o.Id) // end in a unique tie-breaker
      .ToKeysetPageAsync(cursor, pageSize: 20, ct);
```

Pass `null` for the first page, then feed each `page.NextCursor` back in. When `HasMore` is false, `NextCursor` is null. No DI registration is needed.

## Rules for the query

- `OrderBy`/`ThenBy` must be the final operators; apply `Where` and `Select` before the ordering.
- The last sort key must be unique (for example `Id`), so the ordering is a stable total order. Ties on earlier keys are handled.
- Supported sort-key types: the integer, floating-point and `decimal` types, `DateTime`, `DateTimeOffset`, `DateOnly` and `TimeOnly`. `string`, `Guid` and null key values are not supported yet.

## Failure behaviour

- A malformed or tampered cursor, a cursor with the wrong number of values, or a value that does not parse as its key type throws `InvalidCursorException`. Treat it as a client error (400), not a 500.
- `pageSize` of zero or less throws `ArgumentOutOfRangeException`.
- An unsupported key type throws `NotSupportedException` when a cursor has to be read or written; a null key value on the last row of a page throws `InvalidOperationException`.

## How it stays fast

- A continuation is a `WHERE` seek on your sort index: `(k1 < v1) OR (k1 = v1 AND k2 < v2) ...`, with `>` for ascending keys.
- `pageSize + 1` rows are fetched; the extra row sets `HasMore`, so there is no `COUNT(*)`.
- Each page emits an `OrionPage.keyset` span (`orion.page.size`, `orion.page.has_more`) on the `Moongazing.OrionPage` activity source.
- This package is not NativeAOT-published (EF Core is not AOT-clean); the `OrionPage` core is.

## Related packages

- `OrionPage` - the framework-free core: `Page<T>`, the `Cursor` codec, `PageOptions` and `AddOrionPage`, telemetry.

## Links

- Documentation and full README: https://github.com/tunahanaliozturk/OrionPage
- Changelog: https://github.com/tunahanaliozturk/OrionPage/blob/master/CHANGELOG.md
- License: MIT
