namespace Moongazing.OrionPage.EntityFrameworkCore.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Moongazing.OrionPage;
using Moongazing.OrionPage.EntityFrameworkCore;

using Xunit;

public sealed class KeysetPaginationTests : IAsyncLifetime
{
    private SqliteConnection connection = null!;
    private PageDbContext db = null!;

    public async Task InitializeAsync()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PageDbContext>().UseSqlite(connection).Options;
        db = new PageDbContext(options);
        await db.Database.EnsureCreatedAsync();

        // Seed 100 items. Deliberately create ties on CreatedUtc (10 items share each minute) so the
        // Id tie-breaker is exercised — an unstable sort would skip or repeat rows here.
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 1; i <= 100; i++)
        {
            db.Items.Add(new Item { Id = i, CreatedUtc = start.AddMinutes(i / 10), Name = $"item-{i}" });
        }
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await db.DisposeAsync();
        await connection.DisposeAsync();
    }

    // Walk every page forward and collect the ids in the order they were returned.
    private static async Task<List<int>> PageThroughAllAsync(Func<IOrderedQueryable<Item>> ordered, int pageSize)
    {
        var seen = new List<int>();
        string? cursor = null;
        var guard = 0;
        while (true)
        {
            var page = await ordered().ToKeysetPageAsync(cursor, pageSize);
            seen.AddRange(page.Items.Select(i => i.Id));
            if (!page.HasMore)
            {
                Assert.Null(page.NextCursor);
                break;
            }
            Assert.NotNull(page.NextCursor);
            cursor = page.NextCursor;
            Assert.True(++guard < 1000, "paging did not terminate");
        }
        return seen;
    }

    [Fact]
    public async Task Paging_descending_visits_every_row_once_in_order_across_ties()
    {
        var expected = await db.Items
            .OrderByDescending(i => i.CreatedUtc).ThenByDescending(i => i.Id)
            .Select(i => i.Id).ToListAsync();

        var seen = await PageThroughAllAsync(
            () => db.Items.OrderByDescending(i => i.CreatedUtc).ThenByDescending(i => i.Id),
            pageSize: 7);

        Assert.Equal(expected, seen);                 // same order, no skips
        Assert.Equal(100, seen.Distinct().Count());   // no duplicates
    }

    [Fact]
    public async Task Paging_ascending_visits_every_row_once_in_order()
    {
        var expected = await db.Items
            .OrderBy(i => i.CreatedUtc).ThenBy(i => i.Id)
            .Select(i => i.Id).ToListAsync();

        var seen = await PageThroughAllAsync(
            () => db.Items.OrderBy(i => i.CreatedUtc).ThenBy(i => i.Id),
            pageSize: 10);

        Assert.Equal(expected, seen);
        Assert.Equal(100, seen.Count);
    }

    [Fact]
    public async Task Last_page_reports_no_more_and_a_null_cursor()
    {
        var page = await db.Items.OrderBy(i => i.Id).ToKeysetPageAsync(cursor: null, pageSize: 100);
        Assert.False(page.HasMore);
        Assert.Null(page.NextCursor);
        Assert.Equal(100, page.Count);
    }

    [Fact]
    public async Task A_single_column_ordering_pages_correctly()
    {
        var seen = await PageThroughAllAsync(() => db.Items.OrderBy(i => i.Id), pageSize: 13);
        Assert.Equal(Enumerable.Range(1, 100), seen);
    }

    [Fact]
    public async Task Entity_paging_then_projecting_the_items_is_the_recommended_shape()
    {
        // Order and page the entity (simple member access, always EF-translatable), then map the
        // returned items to a DTO in memory.
        var page = await db.Items
            .OrderByDescending(i => i.CreatedUtc).ThenByDescending(i => i.Id)
            .ToKeysetPageAsync(cursor: null, pageSize: 5);

        var dtos = page.Items.Select(i => new ItemDto(i.Id, i.CreatedUtc)).ToList();
        Assert.Equal(5, dtos.Count);
        Assert.True(page.HasMore);
    }

    [Fact]
    public async Task An_anonymous_type_projection_ordered_by_its_members_pages_correctly()
    {
        // Projection before ordering works when EF can translate the ordering (anonymous-type member
        // access does; ordering through a positional-record constructor does not).
        var seen = await PageThroughAllAnonAsync(pageSize: 9);
        Assert.Equal(Enumerable.Range(1, 100).Reverse(), seen);
    }

    private async Task<List<int>> PageThroughAllAnonAsync(int pageSize)
    {
        var seen = new List<int>();
        string? cursor = null;
        while (true)
        {
            var page = await db.Items
                .Select(i => new { i.Id, i.CreatedUtc })
                .OrderByDescending(x => x.CreatedUtc).ThenByDescending(x => x.Id)
                .ToKeysetPageAsync(cursor, pageSize);
            seen.AddRange(page.Items.Select(x => x.Id));
            if (!page.HasMore)
            {
                break;
            }
            cursor = page.NextCursor;
        }
        return seen;
    }

    [Fact]
    public void An_unordered_query_fails_fast()
    {
        // The extension takes IOrderedQueryable<T> (compile-time guard); the parser is the runtime
        // safety net for an ordered query whose ordering operators were stripped.
        Assert.Throws<InvalidOperationException>(() => KeysetOrderingParser.Parse(((IQueryable<Item>)db.Items).Expression));
    }

    [Fact]
    public async Task A_tampered_cursor_is_rejected()
    {
        var page = await db.Items.OrderBy(i => i.Id).ToKeysetPageAsync(cursor: null, pageSize: 5);
        var tampered = page.NextCursor! + "garbage";
        await Assert.ThrowsAsync<InvalidCursorException>(
            () => db.Items.OrderBy(i => i.Id).ToKeysetPageAsync(tampered, pageSize: 5));
    }

    [Fact]
    public async Task A_continuation_query_uses_a_keyset_WHERE_and_no_OFFSET()
    {
        // The whole point of keyset: a deep page is a WHERE seek, never OFFSET. Assert on the SQL.
        var first = await db.Items.OrderByDescending(i => i.CreatedUtc).ThenByDescending(i => i.Id)
            .ToKeysetPageAsync(cursor: null, pageSize: 5);

        var sql = db.Items.OrderByDescending(i => i.CreatedUtc).ThenByDescending(i => i.Id)
            .Where(KeysetProbePredicate())
            .Take(6)
            .ToQueryString();

        Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OFFSET", sql, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(first.NextCursor);
    }

    // A representative continuation predicate, to render SQL for the assertion above.
    private static System.Linq.Expressions.Expression<Func<Item, bool>> KeysetProbePredicate()
    {
        var cutoff = new DateTime(2026, 1, 1, 0, 5, 0, DateTimeKind.Utc);
        return i => i.CreatedUtc < cutoff || (i.CreatedUtc == cutoff && i.Id < 50);
    }
}

public sealed record ItemDto(int Id, DateTime CreatedUtc);

public sealed class Item
{
    public int Id { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string Name { get; set; } = string.Empty;
}

public sealed class PageDbContext : DbContext
{
    public PageDbContext(DbContextOptions<PageDbContext> options) : base(options) { }

    public DbSet<Item> Items => Set<Item>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Item>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Name).IsRequired();
        });
    }
}
