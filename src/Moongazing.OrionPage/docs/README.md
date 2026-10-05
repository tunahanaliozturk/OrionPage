# OrionPage

The framework-free core of OrionPage, keyset (cursor) pagination for .NET: `Page<T>`, the opaque `Cursor` codec, `InvalidCursorException`, `PageOptions` with `AddOrionPage`, and OpenTelemetry instrumentation. Reflection-free and NativeAOT-clean.

![OrionPage packages: your endpoint calls ToKeysetPageAsync in OrionPage.EntityFrameworkCore, which queries EF Core and returns the core's Page<T>; the optional AddOrionPage registers PageOptions and PageDiagnostics, and the core emits the OrionPage.keyset span](https://raw.githubusercontent.com/tunahanaliozturk/OrionPage/master/docs/diagrams/overview.png)

## Install

    dotnet add package OrionPage

To page an EF Core query, install `OrionPage.EntityFrameworkCore` instead; it references this package and adds `ToKeysetPageAsync`.

## Quick start

```csharp
using Moongazing.OrionPage;
using Moongazing.OrionPage.DependencyInjection;

// Optional: options for the page-size bounds.
builder.Services.AddOrionPage(o =>
{
    o.DefaultPageSize = 20;
    o.MaxPageSize = 100;
});

// The cursor codec: sort-key values in, opaque URL-safe token out.
string token = Cursor.Encode(["2026-01-01T00:15:00.0000000Z", "45"]);
if (!Cursor.TryDecode(token, out IReadOnlyList<string> segments))
{
    throw new InvalidCursorException("The cursor is malformed or was tampered with.");
}

// The page shape every OrionPage query returns.
var page = new Page<string>(["a", "b"], nextCursor: token, hasMore: true);
```

## Types

- `Page<T>` - `Items`, `NextCursor` (null when there is no next page), `HasMore` and `Count`. No total count by design.
- `Cursor` - `Encode` writes length-prefixed UTF-8 segments as base64url; `TryDecode` returns false for a malformed or tampered token instead of throwing. The token is opaque but not signed yet.
- `InvalidCursorException` - the client-error exception for an unusable cursor.
- `PageOptions` - `DefaultPageSize` (default 20) and `MaxPageSize` (default 100), both positive, `MaxPageSize` at least `DefaultPageSize`. `AddOrionPage` validates them when the options are first resolved and throws `ArgumentOutOfRangeException` on a bad value. `ToKeysetPageAsync` takes the page size as an argument and does not read these options.

## Telemetry and AOT

- `PageDiagnostics` uses the `Moongazing.OrionPage` activity source and emits an `OrionPage.keyset` span per page with `orion.page.size` and `orion.page.has_more`. `PageDiagnostics.Shared` is used when you do not pass one. Built on `OrionInstrumentation` from `Orion.Abstractions`.
- Targets net8.0, net9.0 and net10.0. `IsAotCompatible`; a NativeAOT publish of the core is smoke-tested in CI.

## Related packages

- `OrionPage.EntityFrameworkCore` - `ToKeysetPageAsync` for EF Core queries, with the keyset predicate builder.

## Links

- Documentation and full README: https://github.com/tunahanaliozturk/OrionPage
- Changelog: https://github.com/tunahanaliozturk/OrionPage/blob/master/CHANGELOG.md
- License: MIT
