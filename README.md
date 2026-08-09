<p align="center">
  <img src="docs/logo.png" alt="OrionPage" width="150" />
</p>

# OrionPage

[![CI/CD](https://github.com/tunahanaliozturk/OrionPage/actions/workflows/ci-cd.yml/badge.svg)](https://github.com/tunahanaliozturk/OrionPage/actions/workflows/ci-cd.yml)
[![NuGet](https://img.shields.io/nuget/v/OrionPage.svg)](https://www.nuget.org/packages/OrionPage/)

**Pagination that stays fast on page 10,000.** Keyset (cursor) paging for EF Core with an opaque cursor and a one-line query extension — because `OFFSET 100000` is a table scan, and every list endpoint eventually pays for it.

Almost every list endpoint ships `.Skip(page * size).Take(size)`. It works in the demo and rots in production: `OFFSET 100000 LIMIT 20` makes the database read and discard 100,000 rows every time (deep pages get linearly slower), and inserting a row mid-scan shifts everything so clients skip or duplicate records. The correct fix — keyset paging (`WHERE (created, id) < (@c, @i) ORDER BY created DESC, id DESC LIMIT 20`) — is constant-time and stable, but composing the tuple predicate, encoding a multi-column cursor, and handling ties is fiddly enough that most teams don't, or do it wrong. OrionPage owns that mechanism so nobody hand-writes tuple-comparison predicates again.

## Packages

- **`OrionPage`** — the framework-free core: `Page<T>` and the opaque `Cursor` codec. Reflection-free and AOT-clean.
- **`OrionPage.EntityFrameworkCore`** — the `ToKeysetPageAsync` query extension plus the keyset engine (`KeysetPredicateBuilder`, `KeysetSortKey`): reads your `OrderBy` chain, builds the tuple-comparison predicate, and executes. (Not AOT-published — the predicate builder relies on operator-method reflection that NativeAOT trims, and EF Core is not AOT-clean.)

## Install

```bash
dotnet add package OrionPage.EntityFrameworkCore
```

## Quick start

Order the query (with `OrderBy`/`ThenBy` as the **final** operators, ending in a unique tie-breaker like `Id`), then page it:

```csharp
using Moongazing.OrionPage;
using Moongazing.OrionPage.EntityFrameworkCore;

public Task<Page<Order>> ListAsync(string? cursor, CancellationToken ct) =>
    db.Orders
      .OrderByDescending(o => o.CreatedUtc).ThenByDescending(o => o.Id) // stable total ordering
      .ToKeysetPageAsync(cursor, pageSize: 20, ct);
```

`Page<T>` carries the rows, an opaque forward `NextCursor`, and `HasMore`:

```jsonc
{ "items": [ /* ... */ ], "nextCursor": "eyJjIjoiMjAyNi0wNy0...", "hasMore": true }
```

Fetch the next page by feeding `NextCursor` back in. When `HasMore` is false, `NextCursor` is null.

### Projecting to a DTO

Order and page the entity, then map the returned items — the always-translatable shape:

```csharp
var page = await db.Orders
    .OrderByDescending(o => o.CreatedUtc).ThenByDescending(o => o.Id)
    .ToKeysetPageAsync(cursor, 20, ct);

var dtos = page.Items.Select(o => new OrderDto(o.Id, o.CreatedUtc, o.Total)).ToList();
```

You can also project **before** ordering when EF can translate the ordering — an anonymous-type projection ordered by its members works; ordering through a positional-record constructor does not.

## What it guarantees

- **Constant-time deep pages.** A continuation is a `WHERE` tuple-comparison seek against your sort index — never `OFFSET`. Page 10,000 costs the same as page 1.
- **No skipped or duplicated rows** across concurrent writes, provided the ordering is a stable *total* order — which is why the last sort key must be a unique tie-breaker (e.g. `Id`). Ties on earlier keys are handled correctly.
- **Opaque cursors.** The cursor is a compact, URL-safe, reflection-free binary token; a malformed or tampered cursor is a typed `InvalidCursorException` (mapped to `400` by the Wave 3 web binding), not a 500. HMAC signing lands in a later wave.
- **No unbounded `COUNT(*)`.** `HasMore` answers "is there a next page?" without the O(n) cost of a total count.

## Observability

A `Moongazing.OrionPage` activity source emits an `OrionPage.keyset` span per page with `orion.page.size` and `orion.page.has_more` attributes, on the family's `OrionInstrumentation` spine.

## Roadmap

This is the **Wave 1** keyset engine (v0.1): keyset paging over EF Core, an opaque cursor, and multi-column ordering read from the `OrderBy` chain, AOT-clean. Later waves add HMAC-signed cursors and nullable/multi-directional sort-key hardening (W2), the minimal-API `[AsCursor]` binding + `.WithKeysetPaging()` filter + OpenAPI + `OrionEnvelope` `meta.page` (W3, GA), and bidirectional cursors + an offset-compat shim + a Dapper adapter (W4). See [CHANGELOG.md](CHANGELOG.md).

Supported sort-key types in this release are the integer, floating-point, decimal, and date/time types; `string`, `Guid`, and nullable keys are hardened in Wave 2. OrionPage does not do "jump to page N" (keyset is sequential by nature — that is the anti-pattern being retired), does not compute a total count by default, and paginates whatever `IQueryable` it is given (compose authorization filters upstream).

## Versioning

Follows [Semantic Versioning](https://semver.org/). Multi-targets `net8.0`, `net9.0`, and `net10.0`. Binds to `Orion.Abstractions` 1.x; the EF Core extension requires EF Core 8+. The framework-free core is AOT- and trim-clean (verified by a native-binary smoke test in CI); the EF Core extension is not NativeAOT-published because EF Core itself is not AOT-clean.

## Documentation

- [CHANGELOG.md](CHANGELOG.md) — release notes.

## Contributing

Contributions are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) and the [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

## More from the Orion family

Focused .NET libraries built to one quality bar. Each is usable on its own; several share the small [`Orion.Abstractions`](https://github.com/tunahanaliozturk/Orion.Abstractions) contracts spine, but there is no deep dependency web — pick only what you need:

- [Orion.Abstractions](https://github.com/tunahanaliozturk/Orion.Abstractions) — the shared contracts spine: telemetry, options, result, clock
- [OrionClock](https://github.com/tunahanaliozturk/OrionClock) — a `TimeProvider`-based clock with TTL / deadline vocabulary
- [OrionResult](https://github.com/tunahanaliozturk/OrionResult) — Result/Option types and a shared error vocabulary
- [OrionResilience](https://github.com/tunahanaliozturk/OrionResilience) — retry, backoff, and timeout on OrionClock
- [OrionRate](https://github.com/tunahanaliozturk/OrionRate) — rate limiting (token-bucket / sliding-window) on OrionClock
- [OrionGuard](https://github.com/tunahanaliozturk/OrionGuard) — validation, guard clauses, DDD primitives, domain events
- [OrionAudit](https://github.com/tunahanaliozturk/OrionAudit) — automatic EF Core change-audit trail
- [OrionBeacon](https://github.com/tunahanaliozturk/OrionBeacon) — leader election with fencing tokens
- [OrionGrant](https://github.com/tunahanaliozturk/OrionGrant) — permission / authorization checks
- [OrionInbox](https://github.com/tunahanaliozturk/OrionInbox) — transactional inbox for exactly-once effects
- [OrionKey](https://github.com/tunahanaliozturk/OrionKey) — source-generated strongly-typed IDs
- [OrionLedger](https://github.com/tunahanaliozturk/OrionLedger) — API-key issuance, verification, and rotation
- [OrionLens](https://github.com/tunahanaliozturk/OrionLens) — ambient correlation-context propagation
- [OrionLock](https://github.com/tunahanaliozturk/OrionLock) — distributed locks with fencing tokens
- [OrionOnce](https://github.com/tunahanaliozturk/OrionOnce) — idempotency keys for exactly-once request handling
- [OrionPatch](https://github.com/tunahanaliozturk/OrionPatch) — transactional outbox for EF Core
- [OrionRelay](https://github.com/tunahanaliozturk/OrionRelay) — outbound webhook delivery (HMAC, retries, backoff)
- [OrionSaga](https://github.com/tunahanaliozturk/OrionSaga) — sagas / process managers for long-running workflows
- [OrionShade](https://github.com/tunahanaliozturk/OrionShade) — sensitive-data redaction for logs and telemetry
- [OrionStream](https://github.com/tunahanaliozturk/OrionStream) — server-sent events / streaming hub
- [OrionVault](https://github.com/tunahanaliozturk/OrionVault) — field-level encryption for EF Core

See it all working together in [OrionShowcase](https://github.com/tunahanaliozturk/OrionShowcase), a production-shaped banking sample.

## License

[MIT](LICENSE).
