using Injaz.Application.Common;
using Injaz.Application.Common.Exceptions;
using Injaz.Application.Features.Boards;
using Injaz.Application.Features.Cards;
using Injaz.Application.Features.Checklist;
using Injaz.Application.Features.Comments;
using Injaz.Application.Features.Lists;
using Injaz.Application.Features.Members;
using Injaz.Application.Features.Workspaces;
using Injaz.Application.Realtime;
using Injaz.Application.Tests.Infrastructure;
using Injaz.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Tests.Features.Realtime;

/// <summary>What gets broadcast, independent of the transport.</summary>
public sealed class BoardEventTests(DatabaseFixture database) : IAsyncDisposable
{
    private readonly TestApp _app = database.CreateApp();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IReadOnlyList<BoardEvent> Events => _app.Realtime.Events;

    [Fact]
    public async Task A_multi_field_patch_publishes_one_upsert_with_the_committed_tile()
    {
        var fixture = await _app.CreateBoardAsync("To do");
        var card = (await _app.AddCardsAsync(fixture.Lists[0].Id, "Draft"))[0];
        _app.Realtime.Clear();

        await _app.SendAsync(new UpdateCard(card.Id, "Final", "## notes", default, Domain.Cards.CardPriority.High, true, default));

        var e = Assert.Single(Events);
        Assert.Equal((BoardEventTypes.CardUpserted, fixture.Board.Id, fixture.Owner.User.Id), (e.Type, e.BoardId, e.ActorId));
        var tile = Assert.IsType<CardSummaryDto>(e.Payload);
        Assert.Equal(("Final", true, Domain.Cards.CardPriority.High), (tile.Title, tile.HasDescription, tile.Priority));
        Assert.NotNull(tile.CompletedAt);
    }

    [Fact]
    public async Task Archiving_or_deleting_is_a_removal_and_failures_publish_nothing()
    {
        var fixture = await _app.CreateBoardAsync("To do");
        var cards = await _app.AddCardsAsync(fixture.Lists[0].Id, "A", "B");
        _app.Realtime.Clear();

        await Assert.ThrowsAsync<ConflictException>(() => _app.SendAsync(new MoveCard(cards[0].Id, null, Guid.NewGuid(), null)));
        Assert.Empty(Events);

        await _app.SendAsync(new UpdateCard(cards[0].Id, default, default, default, default, default, true));
        await _app.SendAsync(new DeleteCard(cards[1].Id));

        Assert.Equal(
            [(BoardEventTypes.CardRemoved, cards[0].Id), (BoardEventTypes.CardRemoved, cards[1].Id)],
            Events.Select(e => (e.Type, IdOf(e.Payload))));
    }

    [Fact]
    public async Task Checklist_and_comment_changes_refresh_the_card_tile()
    {
        var fixture = await _app.CreateBoardAsync("To do");
        var card = (await _app.AddCardsAsync(fixture.Lists[0].Id, "A"))[0];
        var item = await _app.SendAsync(new AddChecklistItem(card.Id, "step"));
        _app.Realtime.Clear();

        await _app.SendAsync(new UpdateChecklistItem(item.Id, default, true));
        await _app.SendAsync(new AddComment(card.Id, "hi"));

        var tiles = Events.Select(e => Assert.IsType<CardSummaryDto>(e.Payload)).ToList();
        Assert.Equal((1, 1, 0), (tiles[0].ChecklistDone, tiles[0].ChecklistTotal, tiles[0].CommentCount));
        Assert.Equal(1, tiles[1].CommentCount);
    }

    [Fact]
    public async Task Lists_are_upserted_and_removed()
    {
        var fixture = await _app.CreateBoardAsync("To do");
        _app.Realtime.Clear();

        var doing = await _app.SendAsync(new CreateList(fixture.Board.Id, "Doing"));
        await _app.SendAsync(new UpdateList(fixture.Lists[0].Id, default, true));
        await _app.SendAsync(new DeleteList(doing.Id));

        Assert.Equal(
            [BoardEventTypes.ListUpserted, BoardEventTypes.ListRemoved, BoardEventTypes.ListRemoved],
            Events.Select(e => e.Type));
        Assert.Equal("Doing", Assert.IsType<ListDto>(Events[0].Payload).Title);
    }

    [Fact]
    public async Task A_rebalance_republishes_every_reranked_card_so_clients_keep_the_right_order()
    {
        var fixture = await _app.CreateBoardAsync("To do");
        var c = await _app.AddCardsAsync(fixture.Lists[0].Id, "1", "2", "3");
        var rank = await _app.DbAsync(db => db.Cards.Where(x => x.Id == c[0].Id).Select(x => x.Rank).SingleAsync(Ct));
        await _app.DbAsync(db => db.Cards.Where(x => x.Id == c[1].Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Rank, rank), Ct)); // tie
        _app.Realtime.Clear();

        await _app.SendAsync(new MoveCard(c[2].Id, null, c[0].Id, c[1].Id));

        var tiles = Events.Select(e => Assert.IsType<CardSummaryDto>(e.Payload)).ToDictionary(t => t.Id);
        Assert.Equal(3, tiles.Count);
        var order = tiles.Values.OrderBy(t => t.Rank, StringComparer.Ordinal).Select(t => t.Title);
        Assert.Equal(["1", "3", "2"], order);
    }

    [Fact]
    public async Task Removing_a_member_revokes_their_live_access_and_refreshes_their_cards()
    {
        var fixture = await _app.CreateBoardAsync("To do");
        var card = (await _app.AddCardsAsync(fixture.Lists[0].Id, "A"))[0];
        var member = await _app.AddMemberAsync(fixture.Owner, fixture.Workspace.Id, WorkspaceRole.Member);
        _app.ActAs(fixture.Owner);
        await _app.SendAsync(new AssignCard(card.Id, member.User.Id));
        _app.Realtime.Clear();

        await _app.SendAsync(new RemoveMember(fixture.Workspace.Id, member.User.Id));

        var revocation = Assert.Single(_app.Realtime.Revocations);
        Assert.Equal((member.User.Id, fixture.Workspace.Id), (revocation.UserId, revocation.WorkspaceId));
        Assert.Equal([fixture.Board.Id], revocation.BoardIds);
        Assert.Empty(Assert.IsType<CardSummaryDto>(Assert.Single(Events).Payload).AssigneeIds);
    }

    [Fact]
    public async Task Deleting_a_workspace_announces_each_of_its_boards()
    {
        var fixture = await _app.CreateBoardAsync("To do");
        var second = await _app.SendAsync(new CreateBoard(fixture.Workspace.Id, "Second", null, null));
        _app.Realtime.Clear();

        await _app.SendAsync(new DeleteWorkspace(fixture.Workspace.Id));

        Assert.All(Events, e => Assert.Equal(BoardEventTypes.BoardDeleted, e.Type));
        Assert.Equal(new[] { fixture.Board.Id, second.Id }.Order(), Events.Select(e => e.BoardId).Order());
    }

    private static Guid IdOf(object payload) => (Guid)payload.GetType().GetProperty("id")!.GetValue(payload)!;

    public ValueTask DisposeAsync() => _app.DisposeAsync();
}
