using Injaz.Application.Common;
using Injaz.Application.Common.Exceptions;
using Injaz.Application.Features.Activity;
using Injaz.Application.Features.Boards;
using Injaz.Application.Features.Cards;
using Injaz.Application.Features.Lists;
using Injaz.Application.Tests.Infrastructure;

namespace Injaz.Application.Tests.Features.Activity;

public sealed class ActivityTests(DatabaseFixture database) : IAsyncDisposable
{
    private readonly TestApp _app = database.CreateApp();

    [Fact]
    public async Task Board_feed_records_meaningful_changes_newest_first_with_titles_as_they_were()
    {
        var (owner, workspace) = await _app.CreateWorkspaceAsync();
        var board = await Step(() => _app.SendAsync(new CreateBoard(workspace.Id, "Roadmap", null, null)));
        var todo = await Step(() => _app.SendAsync(new CreateList(board.Id, "To do")));
        var done = await Step(() => _app.SendAsync(new CreateList(board.Id, "Done")));
        var cards = await Step(() => _app.AddCardsAsync(todo.Id, "Ship it", "Other"));
        await Step(() => _app.SendAsync(new MoveCard(cards[1].Id, null, null, cards[0].Id))); // reorder in same list: not logged
        await Step(() => _app.SendAsync(new MoveCard(cards[0].Id, done.Id, null, null)));
        await Step(() => _app.SendAsync(new UpdateCard(cards[0].Id, "Shipped", default, default, default, true, default)));
        await Step(() => _app.SendAsync(new UpdateList(done.Id, "Finished", default)));

        var feed = (await _app.QueryAsync(new GetBoardActivity(board.Id, null, null))).Items;

        Assert.Equal(
            ["list.renamed", "card.completed", "card.renamed", "card.moved", "card.created", "card.created", "list.created", "list.created", "board.created"],
            feed.Select(a => a.Type));
        Assert.All(feed, a => Assert.Equal(owner.User.Id, a.Actor.UserId));

        var moved = feed.Single(a => a.Type == "card.moved");
        Assert.Equal(cards[0].Id, moved.CardId);
        Assert.Equal(("To do", "Done"), (moved.Data.GetProperty("fromList").GetString(), moved.Data.GetProperty("toList").GetString()));
        Assert.Equal("Ship it", feed.Single(a => a.Type == "card.renamed").Data.GetProperty("from").GetString());
    }

    [Fact]
    public async Task Card_feed_contains_only_that_cards_entries()
    {
        var fixture = await _app.CreateBoardAsync("To do");
        var cards = await _app.AddCardsAsync(fixture.Lists[0].Id, "A", "B");
        _app.Clock.Advance(TimeSpan.FromSeconds(1));
        await _app.SendAsync(new AssignCard(cards[0].Id, fixture.Owner.User.Id));
        await _app.SendAsync(new AssignCard(cards[0].Id, fixture.Owner.User.Id)); // no-op: not logged twice

        var feed = (await _app.QueryAsync(new GetCardActivity(cards[0].Id, null, null))).Items;

        Assert.Equal(["card.assigned", "card.created"], feed.Select(a => a.Type));
        Assert.Equal(fixture.Owner.User.DisplayName, feed[0].Data.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Failed_commands_leave_no_activity()
    {
        var fixture = await _app.CreateBoardAsync("To do");
        var card = (await _app.AddCardsAsync(fixture.Lists[0].Id, "A"))[0];
        var before = (await _app.QueryAsync(new GetBoardActivity(fixture.Board.Id, null, null))).Items.Count;

        await Assert.ThrowsAsync<ConflictException>(() => _app.SendAsync(new MoveCard(card.Id, null, Guid.NewGuid(), null)));
        await Assert.ThrowsAsync<ValidationException>(() => _app.SendAsync(new UpdateCard(card.Id, "", default, default, default, default, default)));

        Assert.Equal(before, (await _app.QueryAsync(new GetBoardActivity(fixture.Board.Id, null, null))).Items.Count);
    }

    [Fact]
    public async Task Deleting_a_card_keeps_its_history_on_the_board()
    {
        var fixture = await _app.CreateBoardAsync("To do");
        var card = (await _app.AddCardsAsync(fixture.Lists[0].Id, "Doomed"))[0];
        _app.Clock.Advance(TimeSpan.FromSeconds(1));

        await _app.SendAsync(new DeleteCard(card.Id));

        var feed = (await _app.QueryAsync(new GetBoardActivity(fixture.Board.Id, null, null))).Items;
        Assert.Equal("card.deleted", feed[0].Type);
        Assert.Equal("Doomed", feed[0].Data.GetProperty("title").GetString());
        var created = feed.Single(a => a.Type == "card.created");
        Assert.Null(created.CardId); // reference cleared, entry kept
        Assert.Equal("Doomed", created.Data.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Activity_is_paginated()
    {
        var fixture = await _app.CreateBoardAsync("A", "B", "C", "D"); // board.created + 4× list.created

        var first = await _app.QueryAsync(new GetBoardActivity(fixture.Board.Id, null, Limit: 3));
        var second = await _app.QueryAsync(new GetBoardActivity(fixture.Board.Id, first.NextCursor, Limit: 3));

        Assert.Equal(3, first.Items.Count);
        Assert.NotNull(first.NextCursor);
        Assert.Equal(2, second.Items.Count);
        Assert.Null(second.NextCursor);
        Assert.Empty(first.Items.Select(a => a.Id).Intersect(second.Items.Select(a => a.Id)));
    }

    private async Task<T> Step<T>(Func<Task<T>> action)
    {
        var result = await action();
        _app.Clock.Advance(TimeSpan.FromSeconds(1)); // distinct timestamps → deterministic feed order
        return result;
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();
}
