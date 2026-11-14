using Injaz.Application.Common;
using Injaz.Application.Common.Exceptions;
using Injaz.Application.Features.Boards;
using Injaz.Application.Features.Cards;
using Injaz.Application.Features.Lists;
using Injaz.Application.Tests.Infrastructure;
using Injaz.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Tests.Features.Boards;

public sealed class BoardHandlerTests(DatabaseFixture database) : IAsyncDisposable
{
    private readonly TestApp _app = database.CreateApp();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Members_create_boards_with_a_default_color_and_viewers_cannot()
    {
        var (owner, workspace) = await _app.CreateWorkspaceAsync();
        await _app.AddMemberAsync(owner, workspace.Id, WorkspaceRole.Member);

        var board = await _app.SendAsync(new CreateBoard(workspace.Id, "  Sprint 1 ", null, null));
        Assert.Equal(("Sprint 1", "blue", false), (board.Title, board.Color, board.IsArchived));

        await _app.AddMemberAsync(owner, workspace.Id, WorkspaceRole.Viewer);
        await Assert.ThrowsAsync<ForbiddenException>(() => _app.SendAsync(new CreateBoard(workspace.Id, "Nope", null, null)));
    }

    [Fact]
    public async Task Board_input_is_validated()
    {
        var (_, workspace) = await _app.CreateWorkspaceAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _app.SendAsync(new CreateBoard(workspace.Id, "", null, "turquoise")));

        Assert.Equal("required", Assert.Single(ex.Errors["title"]).Code);
        Assert.Equal("invalidValue", Assert.Single(ex.Errors["color"]).Code);
    }

    [Fact]
    public async Task Archived_boards_are_hidden_unless_requested()
    {
        var fixture = await _app.CreateBoardAsync();
        var other = await _app.SendAsync(new CreateBoard(fixture.Workspace.Id, "Archive me", null, "red"));

        await _app.SendAsync(new UpdateBoard(other.Id, default, default, default, new Optional<bool?>(true)));

        var open = await _app.QueryAsync(new ListBoards(fixture.Workspace.Id));
        var all = await _app.QueryAsync(new ListBoards(fixture.Workspace.Id, IncludeArchived: true));
        Assert.Equal([fixture.Board.Id], open.Select(b => b.Id));
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task Get_board_returns_open_lists_and_cards_in_order_with_labels_and_members()
    {
        var fixture = await _app.CreateBoardAsync("To do", "Doing", "Done");
        var (todo, doing, done) = (fixture.Lists[0], fixture.Lists[1], fixture.Lists[2]);
        var cards = await _app.AddCardsAsync(todo.Id, "A", "B", "C");
        await _app.SendAsync(new UpdateCard(cards[1].Id, default, default, default, default, default, new Optional<bool?>(true)));
        await _app.SendAsync(new UpdateList(done.Id, default, new Optional<bool?>(true)));
        var label = await _app.SendAsync(new CreateLabel(fixture.Board.Id, "Bug", "red"));

        var board = await _app.QueryAsync(new GetBoard(fixture.Board.Id));

        Assert.Equal(WorkspaceRole.Owner, board.MyRole);
        Assert.Equal([todo.Id, doing.Id], board.Lists.Select(l => l.Id));
        Assert.Equal(["A", "C"], board.Lists[0].Cards.Select(c => c.Title));
        Assert.Equal(label, Assert.Single(board.Labels));
        Assert.Equal(fixture.Owner.User.Id, Assert.Single(board.Members).UserId);
    }

    [Fact]
    public async Task Non_members_cannot_see_the_board_and_ids_from_other_boards_are_rejected()
    {
        var mine = await _app.CreateBoardAsync("Mine");
        var theirs = await _app.CreateBoardAsync("Theirs"); // a different owner, now the caller
        var theirLabel = await _app.SendAsync(new CreateLabel(theirs.Board.Id, null, "green"));

        _app.ActAs(mine.Owner);
        var hidden = await Assert.ThrowsAsync<NotFoundException>(() => _app.QueryAsync(new GetBoard(theirs.Board.Id)));
        Assert.Equal("board.notFound", hidden.Code);
        await Assert.ThrowsAsync<NotFoundException>(() => _app.SendAsync(new CreateCard(theirs.Lists[0].Id, "Sneaky", null)));

        var card = (await _app.AddCardsAsync(mine.Lists[0].Id, "Mine"))[0];
        var wrongLabel = await Assert.ThrowsAsync<NotFoundException>(() => _app.SendAsync(new AddCardLabel(card.Id, theirLabel.Id)));
        var wrongList = await Assert.ThrowsAsync<NotFoundException>(() => _app.SendAsync(new MoveCard(card.Id, theirs.Lists[0].Id, null, null)));
        Assert.Equal("label.notFound", wrongLabel.Code);
        Assert.Equal("list.notFound", wrongList.Code);
    }

    [Fact]
    public async Task Only_admins_delete_boards_and_deletion_cascades()
    {
        var fixture = await _app.CreateBoardAsync("To do");
        await _app.AddCardsAsync(fixture.Lists[0].Id, "A");
        await _app.SendAsync(new CreateLabel(fixture.Board.Id, "x", "red"));
        await _app.AddMemberAsync(fixture.Owner, fixture.Workspace.Id, WorkspaceRole.Member);

        await Assert.ThrowsAsync<ForbiddenException>(() => _app.SendAsync(new DeleteBoard(fixture.Board.Id)));

        await _app.AddMemberAsync(fixture.Owner, fixture.Workspace.Id, WorkspaceRole.Admin);
        await _app.SendAsync(new DeleteBoard(fixture.Board.Id));

        Assert.False(await _app.DbAsync(db => db.Cards.AnyAsync(c => c.BoardId == fixture.Board.Id, Ct)));
        Assert.False(await _app.DbAsync(db => db.BoardLists.AnyAsync(l => l.BoardId == fixture.Board.Id, Ct)));
        Assert.False(await _app.DbAsync(db => db.Labels.AnyAsync(l => l.BoardId == fixture.Board.Id, Ct)));
    }

    [Fact]
    public async Task Deleting_a_label_removes_it_from_cards()
    {
        var fixture = await _app.CreateBoardAsync("To do");
        var card = (await _app.AddCardsAsync(fixture.Lists[0].Id, "A"))[0];
        var label = await _app.SendAsync(new CreateLabel(fixture.Board.Id, "Bug", "red"));
        await _app.SendAsync(new AddCardLabel(card.Id, label.Id));
        await _app.SendAsync(new AddCardLabel(card.Id, label.Id)); // idempotent

        Assert.Equal([label.Id], (await _app.QueryAsync(new GetCard(card.Id))).LabelIds);

        await _app.SendAsync(new DeleteLabel(label.Id));
        Assert.Empty((await _app.QueryAsync(new GetCard(card.Id))).LabelIds);
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();
}
