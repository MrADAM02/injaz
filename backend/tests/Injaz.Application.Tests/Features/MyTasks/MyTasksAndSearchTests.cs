using Injaz.Application.Common;
using Injaz.Application.Common.Exceptions;
using Injaz.Application.Features.Boards;
using Injaz.Application.Features.Cards;
using Injaz.Application.Features.Lists;
using Injaz.Application.Features.MyTasks;
using Injaz.Application.Tests.Infrastructure;
using Injaz.Domain.Cards;
using Injaz.Domain.Workspaces;

namespace Injaz.Application.Tests.Features.MyTasks;

public sealed class MyTasksAndSearchTests(DatabaseFixture database) : IAsyncDisposable
{
    private const string Riyadh = "Asia/Riyadh"; // UTC+3, no DST

    // 22:30 UTC = 01:30 the next day in Riyadh, so "today" differs between UTC and Riyadh. Tomorrow, because the
    // test clock starts at the real time and can't go back.
    private static readonly DateTimeOffset Now = new(DateTime.UtcNow.Date.AddDays(1).AddHours(22).AddMinutes(30), TimeSpan.Zero);

    private readonly TestApp _app = database.CreateApp();

    private UpdateCard Due(Guid cardId, DateTimeOffset? dueAt) => new(cardId, default, default, new Optional<DateTimeOffset?>(dueAt), default, default, default);

    private static GetMyTasks Tasks(DueBucket? due = null, string? tz = null, bool includeCompleted = false, CardPriority? priority = null, string? cursor = null, int? limit = null) =>
        new(due, priority, null, includeCompleted, tz, cursor, limit);

    [Fact]
    public async Task My_tasks_lists_only_my_open_assigned_cards_soonest_first()
    {
        var fixture = await _app.CreateBoardAsync("To do", "Old");
        _app.Clock.SetUtcNow(Now);
        var me = fixture.Owner.User.Id;
        var c = await _app.AddCardsAsync(fixture.Lists[0].Id, "later", "soon", "no date", "not mine", "done", "archived");
        foreach (var card in new[] { c[0], c[1], c[2], c[4], c[5] })
        {
            await _app.SendAsync(new AssignCard(card.Id, me));
        }

        await _app.SendAsync(Due(c[0].Id, Now.AddDays(3)));
        await _app.SendAsync(Due(c[1].Id, Now.AddHours(2)));
        await _app.SendAsync(new UpdateCard(c[4].Id, default, default, default, default, true, default));
        await _app.SendAsync(new UpdateCard(c[5].Id, default, default, default, default, default, true));

        // A card in an archived list doesn't count either.
        var hidden = (await _app.AddCardsAsync(fixture.Lists[1].Id, "in archived list"))[0];
        await _app.SendAsync(new AssignCard(hidden.Id, me));
        await _app.SendAsync(new UpdateList(fixture.Lists[1].Id, default, true));

        var tasks = await _app.QueryAsync(Tasks());
        Assert.Equal(["soon", "later", "no date"], tasks.Items.Select(t => t.Card.Title));
        Assert.All(tasks.Items, t => Assert.Equal(("Roadmap", "To do"), (t.BoardTitle, t.ListTitle)));

        var withCompleted = await _app.QueryAsync(Tasks(includeCompleted: true));
        Assert.Contains("done", withCompleted.Items.Select(t => t.Card.Title));
    }

    [Fact]
    public async Task Due_buckets_follow_the_callers_time_zone()
    {
        var fixture = await _app.CreateBoardAsync("To do");
        _app.Clock.SetUtcNow(Now);
        var c = await _app.AddCardsAsync(fixture.Lists[0].Id, "A", "B", "C", "D");
        foreach (var card in c)
        {
            await _app.SendAsync(new AssignCard(card.Id, fixture.Owner.User.Id));
        }

        await _app.SendAsync(Due(c[0].Id, Now.AddHours(13.5))); // 12:00 UTC the next day: today in Riyadh
        await _app.SendAsync(Due(c[1].Id, Now.AddHours(-2.5))); // 20:00 UTC: yesterday in Riyadh, overdue
        await _app.SendAsync(Due(c[2].Id, Now.AddDays(4).AddHours(13.5))); // this week
        // D has no due date

        async Task<IEnumerable<string>> Titles(DueBucket due, string? tz) => (await _app.QueryAsync(Tasks(due, tz))).Items.Select(t => t.Card.Title);

        Assert.Equal(["A"], await Titles(DueBucket.Today, Riyadh));
        Assert.Equal(["B"], await Titles(DueBucket.Today, tz: null)); // UTC's "today" is still the day before
        Assert.Equal(["B"], await Titles(DueBucket.Overdue, Riyadh));
        Assert.Equal(["A", "C"], await Titles(DueBucket.Week, Riyadh));
        Assert.Equal(["D"], await Titles(DueBucket.None, Riyadh));
    }

    [Fact]
    public async Task My_tasks_paginate_across_dated_and_undated_cards()
    {
        var fixture = await _app.CreateBoardAsync("To do");
        var c = await _app.AddCardsAsync(fixture.Lists[0].Id, "1", "2", "3", "4", "5");
        foreach (var card in c)
        {
            await _app.SendAsync(new AssignCard(card.Id, fixture.Owner.User.Id));
        }

        await _app.SendAsync(Due(c[0].Id, Now.AddDays(1)));
        await _app.SendAsync(Due(c[1].Id, Now.AddDays(2)));

        var titles = new List<string>();
        string? cursor = null;
        do
        {
            var page = await _app.QueryAsync(Tasks(cursor: cursor, limit: 2));
            titles.AddRange(page.Items.Select(t => t.Card.Title));
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        Assert.Equal(5, titles.Count);
        Assert.Equal(["1", "2"], titles.Take(2));
        Assert.Equal(5, titles.Distinct().Count());
    }

    [Fact]
    public async Task Leaving_a_workspace_removes_its_cards_from_my_tasks()
    {
        var fixture = await _app.CreateBoardAsync("To do");
        var card = (await _app.AddCardsAsync(fixture.Lists[0].Id, "x"))[0];
        var member = await _app.AddMemberAsync(fixture.Owner, fixture.Workspace.Id, WorkspaceRole.Member);
        await _app.SendAsync(new AssignCard(card.Id, member.User.Id));
        Assert.Single((await _app.QueryAsync(Tasks())).Items);

        await _app.SendAsync(new Application.Features.Members.RemoveMember(fixture.Workspace.Id, member.User.Id));

        Assert.Empty((await _app.QueryAsync(Tasks())).Items);
    }

    [Fact]
    public async Task Unknown_time_zones_are_rejected()
    {
        await _app.RegisterAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _app.QueryAsync(Tasks(DueBucket.Today, "Mars/Olympus")));

        Assert.Equal("invalidValue", Assert.Single(ex.Errors["tz"]).Code);
    }

    [Fact]
    public async Task Board_search_matches_text_labels_assignees_and_archived_state()
    {
        var fixture = await _app.CreateBoardAsync("To do");
        var list = fixture.Lists[0].Id;
        var c = await _app.AddCardsAsync(list, "Write SPEC", "تصميم الشعار", "Unrelated", "Old spec");
        await _app.SendAsync(new UpdateCard(c[2].Id, default, "see the spec doc", default, default, default, default));
        await _app.SendAsync(new UpdateCard(c[3].Id, default, default, default, default, default, true));
        var bug = await _app.SendAsync(new CreateLabel(fixture.Board.Id, "Bug", "red"));
        await _app.SendAsync(new AddCardLabel(c[1].Id, bug.Id));
        await _app.SendAsync(new AssignCard(c[0].Id, fixture.Owner.User.Id));

        async Task<IEnumerable<string>> Search(string? q = null, Guid[]? labels = null, Guid[]? assignees = null, bool archived = false) =>
            (await _app.QueryAsync(new SearchCards(fixture.Board.Id, q, labels, assignees, null, null, archived, null, null)))
            .Items.Select(r => r.Card.Title).Order(StringComparer.Ordinal);

        Assert.Equal(["Unrelated", "Write SPEC"], await Search("spec"));         // title or description, any case
        Assert.Equal(["تصميم الشعار"], await Search("الشعار"));
        Assert.Equal(["تصميم الشعار"], await Search(labels: [bug.Id]));
        Assert.Equal(["Write SPEC"], await Search(assignees: [fixture.Owner.User.Id]));
        Assert.Equal(["Old spec"], await Search("spec", archived: true));
        Assert.Equal(4 - 1, (await Search()).Count());
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();
}
