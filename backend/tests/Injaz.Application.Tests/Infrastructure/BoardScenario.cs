using Injaz.Application.Features.Auth;
using Injaz.Application.Features.Boards;
using Injaz.Application.Features.Cards;
using Injaz.Application.Features.Lists;
using Injaz.Application.Features.Workspaces;

namespace Injaz.Application.Tests.Infrastructure;

public sealed record BoardFixture(AuthSession Owner, WorkspaceDto Workspace, BoardSummaryDto Board, IReadOnlyList<ListDto> Lists);

/// <summary>Builds boards, lists and cards through the real use cases.</summary>
public static class BoardScenario
{
    /// <summary>A fresh owner + workspace + board with the given lists. The owner is the current caller afterwards.</summary>
    public static async Task<BoardFixture> CreateBoardAsync(this TestApp app, params string[] lists)
    {
        ArgumentNullException.ThrowIfNull(app);
        var (owner, workspace) = await app.CreateWorkspaceAsync();
        var board = await app.SendAsync(new CreateBoard(workspace.Id, "Roadmap", null, null));

        var created = new List<ListDto>();
        foreach (var title in lists)
        {
            created.Add(await app.SendAsync(new CreateList(board.Id, title)));
        }

        return new BoardFixture(owner, workspace, board, created);
    }

    public static async Task<IReadOnlyList<CardSummaryDto>> AddCardsAsync(this TestApp app, Guid listId, params string[] titles)
    {
        ArgumentNullException.ThrowIfNull(app);
        var cards = new List<CardSummaryDto>();
        foreach (var title in titles)
        {
            cards.Add(await app.SendAsync(new CreateCard(listId, title, null)));
        }

        return cards;
    }

    /// <summary>Titles of the open cards in a list, in board order.</summary>
    public static async Task<IReadOnlyList<string>> CardTitlesAsync(this TestApp app, Guid boardId, Guid listId)
    {
        ArgumentNullException.ThrowIfNull(app);
        var board = await app.QueryAsync(new GetBoard(boardId));
        return [.. board.Lists.Single(l => l.Id == listId).Cards.Select(c => c.Title)];
    }
}
