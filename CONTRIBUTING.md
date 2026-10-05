# Contributing to OrionPage

Thanks for taking the time to look at this. OrionPage is keyset (cursor) pagination for EF Core: constant-time deep paging with an opaque cursor, because `OFFSET` is a table scan. The project is small and the bar for contributions is "does it make the package clearer, faster, or safer without expanding the public surface needlessly."

## Before you open a PR

For anything beyond a typo, a docs tweak, or a one-line fix, please open an issue first. Five minutes of alignment up front saves an afternoon of rework later. State:

- The use case you are trying to solve
- What you tried that did not work
- Whether you want to send the patch yourself or are flagging the gap

For typos, docs polish, comment fixes, single-line changes, please skip the issue and send a PR directly. Title it `docs: ...` or `chore: ...` so it is obvious from the queue.

## Local development

```bash
git clone https://github.com/tunahanaliozturk/OrionPage
cd OrionPage
dotnet restore
dotnet build -c Release
dotnet test
```

The .NET 10 SDK is required: the libraries target `net8.0`, `net9.0` and `net10.0`, and the NativeAOT smoke test targets `net10.0`. Running the tests on every target also needs the .NET 8 and 9 runtimes. The multi-target dimension is intentional and not optional. The EF Core tests run against a real, in-memory SQLite database — no Docker required.

Branch from `master`. Name the branch after intent: `feat/...`, `fix/...`, `docs/...`, `refactor/...`, `chore/...`, `test/...`.

## Pull request shape

- One conceptual change per PR. Refactors and behaviour changes go in separate PRs even if the diff feels small.
- Conventional Commits style commit subject (`feat:`, `fix:`, `docs:`, etc.).
- New behaviour comes with tests. Bug fixes come with a failing-before, passing-after test.
- Public API additions need XML doc comments. Breaking changes need a CHANGELOG entry.
- No `Co-Authored-By` trailers. The author of the PR is the author of the work.

## Coding style

- The repo enforces analyzer warnings as errors and `latest-recommended` analysis level. Treat warnings as bugs.
- Match the surrounding code style. If the existing code does X, do X.
- Names are spelled out. No `mgr`, `svc`, `ctx`. The exceptions are well-known abbreviations (`Id`, `Db`, `Url`, `Sql`).
- Comments explain why, not what. The code already says what.
- The correctness bar is absolute: keyset paging must never skip or duplicate a row across pages given a stable total order. Any change to the predicate builder or the cursor codec must keep the "visit every row exactly once" property, and the framework-free core must stay reflection-free and AOT-clean (no `Expression.Compile`, no reflection-based serialization).

## Tests

- xUnit.
- Test names are sentences with underscores: `Paging_descending_visits_every_row_once_in_order_across_ties`.
- Keyset correctness is tested against a real relational store (SQLite), not mocked — the SQL translation and the no-skip/no-duplicate property must be genuinely exercised, including ties on non-final sort keys.
- Coverage is a side effect of writing tests for behaviour, not a target in itself.

## Reporting bugs

Open an issue with:

- A minimal reproduction (the entity, the `OrderBy`, and the page calls, ideally less than 50 lines)
- The actual behaviour vs the expected behaviour
- The runtime (`dotnet --info` output), the EF Core provider, and the package version

If the bug has security implications, do not open a public issue; follow [SECURITY.md](SECURITY.md).

## Security

Do not file public issues for vulnerabilities. Report them privately through GitHub as described in [SECURITY.md](SECURITY.md).

## Conduct

Be kind. We follow the [Code of Conduct](CODE_OF_CONDUCT.md). Disagreement is fine; rudeness is not.

## License

By submitting a pull request, you agree your contribution is licensed under the repo's [MIT License](LICENSE).
