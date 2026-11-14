using Injaz.Application.Common.Exceptions;
using Injaz.Application.Features.Boards;
using Injaz.Application.Features.Cards;
using Injaz.Application.Features.Checklist;
using Injaz.Application.Features.Comments;
using Injaz.Application.Tests.Infrastructure;
using Injaz.Domain.Workspaces;

namespace Injaz.Application.Tests.Features.Cards;

public sealed class ChecklistAndCommentTests(DatabaseFixture database) : IAsyncDisposable
{
    private readonly TestApp _app = database.CreateApp();

    [Fact]
    public async Task Checklist_items_keep_their_order_and_feed_the_card_badges()
    {
        var fixture = await _app.CreateBoardAsync("To do");
        var card = (await _app.AddCardsAsync(fixture.Lists[0].Id, "Launch"))[0];

        var a = await _app.SendAsync(new AddChecklistItem(card.Id, "Write copy"));
        var b = await _app.SendAsync(new AddChecklistItem(card.Id, "Design banner"));
        var c = await _app.SendAsync(new AddChecklistItem(card.Id, "Publish"));
        await _app.SendAsync(new MoveChecklistItem(c.Id, AfterId: null, BeforeId: a.Id));
        await _app.SendAsync(new UpdateChecklistItem(a.Id, default, true));
        await _app.SendAsync(new DeleteChecklistItem(b.Id));
        await _app.SendAsync(new AddComment(card.Id, "Looks good"));

        var detail = await _app.QueryAsync(new GetCard(card.Id));
        Assert.Equal(["Publish", "Write copy"], detail.Checklist.Select(i => i.Text));
        Assert.Equal([false, true], detail.Checklist.Select(i => i.IsDone));
        Assert.Equal(1, detail.CommentCount);

        var tile = (await _app.QueryAsync(new GetBoard(fixture.Board.Id))).Lists[0].Cards.Single();
        Assert.Equal((1, 2, 1), (tile.ChecklistDone, tile.ChecklistTotal, tile.CommentCount));
    }

    [Fact]
    public async Task Viewers_cannot_touch_the_checklist_or_comment()
    {
        var fixture = await _app.CreateBoardAsync("To do");
        var card = (await _app.AddCardsAsync(fixture.Lists[0].Id, "x"))[0];
        var item = await _app.SendAsync(new AddChecklistItem(card.Id, "step"));

        await _app.AddMemberAsync(fixture.Owner, fixture.Workspace.Id, WorkspaceRole.Viewer);

        await Assert.ThrowsAsync<ForbiddenException>(() => _app.SendAsync(new AddChecklistItem(card.Id, "nope")));
        await Assert.ThrowsAsync<ForbiddenException>(() => _app.SendAsync(new UpdateChecklistItem(item.Id, default, true)));
        await Assert.ThrowsAsync<ForbiddenException>(() => _app.SendAsync(new AddComment(card.Id, "nope")));
        Assert.Single((await _app.QueryAsync(new GetCard(card.Id))).Checklist);
    }

    [Fact]
    public async Task Comments_page_newest_first_without_gaps_or_duplicates()
    {
        var fixture = await _app.CreateBoardAsync("To do");
        var card = (await _app.AddCardsAsync(fixture.Lists[0].Id, "x"))[0];
        for (var i = 1; i <= 5; i++)
        {
            await _app.SendAsync(new AddComment(card.Id, $"comment {i}"));
            _app.Clock.Advance(TimeSpan.FromSeconds(1));
        }

        var seen = new List<string>();
        string? cursor = null;
        do
        {
            var page = await _app.QueryAsync(new ListComments(card.Id, cursor, Limit: 2));
            Assert.True(page.Items.Count <= 2);
            seen.AddRange(page.Items.Select(c => c.Body));
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        Assert.Equal(["comment 5", "comment 4", "comment 3", "comment 2", "comment 1"], seen);
    }

    [Fact]
    public async Task Only_the_author_edits_and_admins_may_delete_anyones_comment()
    {
        var fixture = await _app.CreateBoardAsync("To do");
        var card = (await _app.AddCardsAsync(fixture.Lists[0].Id, "x"))[0];
        var member = await _app.AddMemberAsync(fixture.Owner, fixture.Workspace.Id, WorkspaceRole.Member);
        var mine = await _app.SendAsync(new AddComment(card.Id, "  first draft  "));
        Assert.Equal("first draft", mine.Body);
        Assert.Equal(member.User.Id, mine.Author.UserId);

        var edited = await _app.SendAsync(new EditComment(mine.Id, "final"));
        Assert.Equal("final", edited.Body);
        Assert.NotNull(edited.EditedAt);

        _app.ActAs(fixture.Owner);
        var ownerComment = await _app.SendAsync(new AddComment(card.Id, "owner's"));
        var notAuthor = await Assert.ThrowsAsync<ForbiddenException>(() => _app.SendAsync(new EditComment(mine.Id, "hijack")));
        Assert.Equal("comment.notAuthor", notAuthor.Code);

        _app.ActAs(member);
        await Assert.ThrowsAsync<ForbiddenException>(() => _app.SendAsync(new DeleteComment(ownerComment.Id)));

        _app.ActAs(fixture.Owner);
        await _app.SendAsync(new DeleteComment(mine.Id)); // admin+ removes someone else's
        Assert.Equal(["owner's"], (await _app.QueryAsync(new ListComments(card.Id, null, null))).Items.Select(c => c.Body));
    }

    [Fact]
    public async Task Comment_body_and_paging_input_are_validated()
    {
        var fixture = await _app.CreateBoardAsync("To do");
        var card = (await _app.AddCardsAsync(fixture.Lists[0].Id, "x"))[0];

        var empty = await Assert.ThrowsAsync<ValidationException>(() => _app.SendAsync(new AddComment(card.Id, "  ")));
        var badCursor = await Assert.ThrowsAsync<ValidationException>(() => _app.QueryAsync(new ListComments(card.Id, "garbage!", null)));
        var badLimit = await Assert.ThrowsAsync<ValidationException>(() => _app.QueryAsync(new ListComments(card.Id, null, 500)));

        Assert.Equal("required", Assert.Single(empty.Errors["body"]).Code);
        Assert.Equal("invalidValue", Assert.Single(badCursor.Errors["cursor"]).Code);
        Assert.Equal("outOfRange", Assert.Single(badLimit.Errors["limit"]).Code);
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();
}
