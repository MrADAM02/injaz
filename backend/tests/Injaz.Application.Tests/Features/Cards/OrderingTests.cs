using Injaz.Application.Common.Exceptions;
using Injaz.Application.Features.Boards;
using Injaz.Application.Features.Cards;
using Injaz.Application.Features.Lists;
using Injaz.Application.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Tests.Features.Cards;

/// <summary>Drag-and-drop ordering of lists and cards, including the rare rebalance fallback.</summary>
public sealed class OrderingTests(DatabaseFixture database) : IAsyncDisposable
{
    private readonly TestApp _app = database.CreateApp();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Moving_a_list_changes_only_that_lists_rank()
    {
        var fixture = await _app.CreateBoardAsync("A", "B", "C");
        var (a, b, c) = (fixture.Lists[0], fixture.Lists[1], fixture.Lists[2]);

        var moved = await _app.SendAsync(new MoveList(c.Id, AfterId: null, BeforeId: a.Id));

        var board = await _app.QueryAsync(new GetBoard(fixture.Board.Id));
        Assert.Equal(["C", "A", "B"], board.Lists.Select(l => l.Title));
        Assert.Equal(a.Rank, board.Lists[1].Rank);
        Assert.Equal(b.Rank, board.Lists[2].Rank);
        Assert.Equal(moved.Rank, board.Lists[0].Rank);
    }

    [Fact]
    public async Task Cards_can_be_created_at_the_top_or_bottom()
    {
        var fixture = await _app.CreateBoardAsync("To do");
        var list = fixture.Lists[0].Id;
        await _app.AddCardsAsync(list, "middle");

        await _app.SendAsync(new CreateCard(list, "bottom", CardPosition.Bottom));
        await _app.SendAsync(new CreateCard(list, "top", CardPosition.Top));

        Assert.Equal(["top", "middle", "bottom"], await _app.CardTitlesAsync(fixture.Board.Id, list));
    }

    [Fact]
    public async Task Cards_move_within_and_across_lists_using_any_combination_of_neighbours()
    {
        var fixture = await _app.CreateBoardAsync("To do", "Done");
        var (todo, done) = (fixture.Lists[0].Id, fixture.Lists[1].Id);
        var c = await _app.AddCardsAsync(todo, "1", "2", "3", "4");

        await _app.SendAsync(new MoveCard(c[3].Id, null, AfterId: c[0].Id, BeforeId: c[1].Id)); // both neighbours
        Assert.Equal(["1", "4", "2", "3"], await _app.CardTitlesAsync(fixture.Board.Id, todo));

        await _app.SendAsync(new MoveCard(c[0].Id, null, AfterId: c[2].Id, BeforeId: null)); // only "after" (→ end)
        Assert.Equal(["4", "2", "3", "1"], await _app.CardTitlesAsync(fixture.Board.Id, todo));

        await _app.SendAsync(new MoveCard(c[2].Id, done, null, null)); // empty target list
        await _app.SendAsync(new MoveCard(c[1].Id, done, AfterId: null, BeforeId: c[2].Id)); // only "before" (→ top)
        Assert.Equal(["2", "3"], await _app.CardTitlesAsync(fixture.Board.Id, done));
        Assert.Equal(["4", "1"], await _app.CardTitlesAsync(fixture.Board.Id, todo));
    }

    [Fact]
    public async Task A_neighbour_that_moved_away_makes_the_drop_stale()
    {
        var fixture = await _app.CreateBoardAsync("To do", "Done");
        var c = await _app.AddCardsAsync(fixture.Lists[0].Id, "1", "2", "3");
        await _app.SendAsync(new MoveCard(c[1].Id, fixture.Lists[1].Id, null, null)); // someone else moved "2" away

        var ex = await Assert.ThrowsAsync<ConflictException>(() => _app.SendAsync(new MoveCard(c[2].Id, null, c[0].Id, c[1].Id)));

        Assert.Equal("ordering.staleNeighbor", ex.Code);
    }

    [Fact]
    public async Task Tied_ranks_from_concurrent_drops_are_resolved_by_rebalancing_the_list()
    {
        var fixture = await _app.CreateBoardAsync("To do");
        var list = fixture.Lists[0].Id;
        var c = await _app.AddCardsAsync(list, "1", "2", "3");
        await TieAsync(c[0].Id, c[1].Id); // simulate two concurrent drops into the same slot

        await _app.SendAsync(new MoveCard(c[2].Id, null, AfterId: c[0].Id, BeforeId: c[1].Id));

        var ranks = await RanksAsync(list);
        Assert.Equal(ranks.Count, ranks.Distinct().Count());
        Assert.Equal(ranks.Order(StringComparer.Ordinal), ranks);
        Assert.Equal(3, (await _app.CardTitlesAsync(fixture.Board.Id, list)).Count);
        Assert.Equal("3", (await _app.CardTitlesAsync(fixture.Board.Id, list))[1]);
    }

    [Fact]
    public async Task Keys_that_grew_too_long_are_respread_short()
    {
        var fixture = await _app.CreateBoardAsync("To do");
        var list = fixture.Lists[0].Id;
        var c = await _app.AddCardsAsync(list, "1", "2", "3");
        await _app.DbAsync(db => db.Cards.Where(x => x.Id == c[0].Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Rank, new string('V', 63) + "1"), Ct));
        await _app.DbAsync(db => db.Cards.Where(x => x.Id == c[1].Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Rank, new string('V', 63) + "2"), Ct));

        await _app.SendAsync(new MoveCard(c[2].Id, null, c[0].Id, c[1].Id));

        Assert.Equal(["1", "3", "2"], await _app.CardTitlesAsync(fixture.Board.Id, list));
        Assert.All(await RanksAsync(list), r => Assert.True(r.Length <= 2, r));
    }

    [Fact]
    public async Task Concurrent_moves_of_the_same_card_cannot_both_win()
    {
        var fixture = await _app.CreateBoardAsync("To do", "Doing", "Done");
        var card = (await _app.AddCardsAsync(fixture.Lists[0].Id, "x"))[0];

        var results = await Task.WhenAll(
            Attempt(() => _app.SendAsync(new MoveCard(card.Id, fixture.Lists[1].Id, null, null))),
            Attempt(() => _app.SendAsync(new MoveCard(card.Id, fixture.Lists[2].Id, null, null))));

        Assert.All(results, r => Assert.True(r is null or ConflictException, r?.GetType().Name));
        var listId = await _app.DbAsync(db => db.Cards.Where(x => x.Id == card.Id).Select(x => x.ListId).SingleAsync(Ct));
        Assert.Contains(listId, new[] { fixture.Lists[1].Id, fixture.Lists[2].Id });
    }

    private Task<int> TieAsync(Guid first, Guid second) =>
        _app.DbAsync(async db =>
        {
            var rank = await db.Cards.Where(x => x.Id == first).Select(x => x.Rank).SingleAsync(Ct);
            return await db.Cards.Where(x => x.Id == second).ExecuteUpdateAsync(s => s.SetProperty(x => x.Rank, rank), Ct);
        });

    private Task<List<string>> RanksAsync(Guid listId) =>
        _app.DbAsync(db => db.Cards.Where(x => x.ListId == listId).OrderBy(x => x.Rank).ThenBy(x => x.Id).Select(x => x.Rank).ToListAsync(Ct));

    private static async Task<Exception?> Attempt(Func<Task> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (AppException ex)
        {
            return ex;
        }
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();
}
