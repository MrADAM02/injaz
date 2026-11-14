using Injaz.Application.Common;
using Injaz.Application.Common.Exceptions;
using Injaz.Application.Features.Cards;
using Injaz.Application.Features.Invitations;
using Injaz.Application.Features.Lists;
using Injaz.Application.Features.Members;
using Injaz.Application.Tests.Infrastructure;
using Injaz.Domain.Cards;
using Injaz.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Tests.Features.Cards;

public sealed class CardHandlerTests(DatabaseFixture database) : IAsyncDisposable
{
    private readonly TestApp _app = database.CreateApp();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static UpdateCard Patch(
        Guid cardId,
        Optional<string?> title = default,
        Optional<string?> description = default,
        Optional<DateTimeOffset?> dueAt = default,
        Optional<CardPriority?> priority = default,
        Optional<bool?> completed = default,
        Optional<bool?> archived = default) =>
        new(cardId, title, description, dueAt, priority, completed, archived);

    [Fact]
    public async Task Patch_changes_only_sent_fields_and_null_clears()
    {
        var fixture = await _app.CreateBoardAsync("To do");
        var card = (await _app.AddCardsAsync(fixture.Lists[0].Id, "Write spec"))[0];
        var riyadhDeadline = new DateTimeOffset(2026, 12, 1, 17, 0, 0, TimeSpan.FromHours(3));

        var scheduled = await _app.SendAsync(Patch(card.Id, description: "## Goals", dueAt: riyadhDeadline, priority: CardPriority.High));

        Assert.Equal("Write spec", scheduled.Title);
        Assert.Equal("## Goals", scheduled.Description);
        Assert.Equal(CardPriority.High, scheduled.Priority);
        Assert.Equal(riyadhDeadline, scheduled.DueAt);
        Assert.Equal(TimeSpan.Zero, scheduled.DueAt!.Value.Offset); // stored and returned in UTC

        var cleared = await _app.SendAsync(Patch(card.Id, dueAt: new Optional<DateTimeOffset?>(null)));
        Assert.Null(cleared.DueAt);
        Assert.Equal("## Goals", cleared.Description);
        Assert.Equivalent(cleared, await _app.QueryAsync(new GetCard(card.Id)), strict: true);
    }

    [Fact]
    public async Task Completing_is_idempotent_and_keeps_the_first_completion_time()
    {
        var fixture = await _app.CreateBoardAsync("To do");
        var card = (await _app.AddCardsAsync(fixture.Lists[0].Id, "Ship"))[0];

        var first = await _app.SendAsync(Patch(card.Id, completed: true));
        _app.Clock.Advance(TimeSpan.FromHours(1));
        var again = await _app.SendAsync(Patch(card.Id, completed: true));
        var reopened = await _app.SendAsync(Patch(card.Id, completed: false));

        Assert.NotNull(first.CompletedAt);
        Assert.Equal(first.CompletedAt, again.CompletedAt);
        Assert.Null(reopened.CompletedAt);
    }

    [Fact]
    public async Task Patch_input_is_validated()
    {
        var fixture = await _app.CreateBoardAsync("To do");
        var card = (await _app.AddCardsAsync(fixture.Lists[0].Id, "x"))[0];

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _app.SendAsync(
            Patch(card.Id, title: new Optional<string?>(null), priority: new Optional<CardPriority?>(null), archived: new Optional<bool?>(null))));

        Assert.Equal("required", Assert.Single(ex.Errors["title"]).Code);
        Assert.Equal("required", Assert.Single(ex.Errors["priority"]).Code);
        Assert.Equal("required", Assert.Single(ex.Errors["isArchived"]).Code);
    }

    [Fact]
    public async Task Viewers_read_members_edit_and_only_admins_delete()
    {
        var fixture = await _app.CreateBoardAsync("To do");
        var card = (await _app.AddCardsAsync(fixture.Lists[0].Id, "x"))[0];

        await _app.AddMemberAsync(fixture.Owner, fixture.Workspace.Id, WorkspaceRole.Viewer);
        await _app.QueryAsync(new GetCard(card.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => _app.SendAsync(Patch(card.Id, title: "hijack")));
        await Assert.ThrowsAsync<ForbiddenException>(() => _app.SendAsync(new MoveCard(card.Id, null, null, null)));

        await _app.AddMemberAsync(fixture.Owner, fixture.Workspace.Id, WorkspaceRole.Member);
        await _app.SendAsync(Patch(card.Id, title: "renamed"));
        await Assert.ThrowsAsync<ForbiddenException>(() => _app.SendAsync(new DeleteCard(card.Id)));
        await Assert.ThrowsAsync<ForbiddenException>(() => _app.SendAsync(new DeleteList(fixture.Lists[0].Id)));

        _app.ActAs(fixture.Owner);
        await _app.SendAsync(new DeleteCard(card.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => _app.QueryAsync(new GetCard(card.Id)));
    }

    [Fact]
    public async Task Only_workspace_members_can_be_assigned_and_assignment_is_idempotent()
    {
        var fixture = await _app.CreateBoardAsync("To do");
        var card = (await _app.AddCardsAsync(fixture.Lists[0].Id, "x"))[0];
        var stranger = await _app.RegisterAsync();
        _app.ActAs(fixture.Owner);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => _app.SendAsync(new AssignCard(card.Id, stranger.User.Id)));
        Assert.Equal("member.notFound", ex.Code);

        await _app.SendAsync(new AssignCard(card.Id, fixture.Owner.User.Id));
        await _app.SendAsync(new AssignCard(card.Id, fixture.Owner.User.Id));
        Assert.Equal([fixture.Owner.User.Id], (await _app.QueryAsync(new GetCard(card.Id))).AssigneeIds);

        await _app.SendAsync(new UnassignCard(card.Id, fixture.Owner.User.Id));
        await _app.SendAsync(new UnassignCard(card.Id, fixture.Owner.User.Id));
        Assert.Empty((await _app.QueryAsync(new GetCard(card.Id))).AssigneeIds);
    }

    [Fact]
    public async Task Removing_a_member_unassigns_them_from_that_workspaces_cards_only()
    {
        var fixture = await _app.CreateBoardAsync("To do");
        var card = (await _app.AddCardsAsync(fixture.Lists[0].Id, "x"))[0];
        var member = await _app.AddMemberAsync(fixture.Owner, fixture.Workspace.Id, WorkspaceRole.Member);

        // The same person also belongs to another workspace, with a card assigned there.
        var otherBoard = await _app.CreateBoardAsync("Elsewhere"); // new owner is now the caller
        var otherCard = (await _app.AddCardsAsync(otherBoard.Lists[0].Id, "y"))[0];
        var invitation = await _app.SendAsync(new CreateInvitation(otherBoard.Workspace.Id, member.User.Email, WorkspaceRole.Member));
        _app.ActAs(member);
        await _app.SendAsync(new AcceptInvitation(invitation.Token));
        _app.ActAs(otherBoard.Owner);
        await _app.SendAsync(new AssignCard(otherCard.Id, member.User.Id));

        _app.ActAs(fixture.Owner);
        await _app.SendAsync(new AssignCard(card.Id, member.User.Id));
        await _app.SendAsync(new RemoveMember(fixture.Workspace.Id, member.User.Id));

        Assert.Empty((await _app.QueryAsync(new GetCard(card.Id))).AssigneeIds);
        Assert.True(await _app.DbAsync(db => db.Cards.AnyAsync(c => c.Id == otherCard.Id && c.Assignees.Any(), Ct)));
    }

    [Fact]
    public async Task Deleting_a_list_deletes_its_cards()
    {
        var fixture = await _app.CreateBoardAsync("To do");
        await _app.AddCardsAsync(fixture.Lists[0].Id, "a", "b");

        await _app.SendAsync(new DeleteList(fixture.Lists[0].Id));

        Assert.False(await _app.DbAsync(db => db.Cards.AnyAsync(c => c.ListId == fixture.Lists[0].Id, Ct)));
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();
}
