using System.Text.Json;
using Injaz.Application.Common;
using Injaz.Application.Messaging;
using Injaz.Application.Persistence;
using Injaz.Application.Security;
using Injaz.Application.Validation;
using Injaz.Domain.Activity;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Features.Activity;

public sealed record ActorDto(Guid UserId, string DisplayName, string? AvatarUrl);

/// <summary>One feed entry. <see cref="Data"/> is the event's JSON details (titles/names as they were at the time).</summary>
public sealed record ActivityDto(Guid Id, string Type, Guid BoardId, Guid? CardId, ActorDto Actor, JsonElement Data, DateTimeOffset CreatedAt);

/// <summary>A board's activity, newest first.</summary>
public sealed record GetBoardActivity(Guid BoardId, string? Cursor, int? Limit) : IQuery<PagedResult<ActivityDto>>, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Board(BoardId);

    public Permission Permission => Permission.View;
}

/// <summary>A card's activity, newest first.</summary>
public sealed record GetCardActivity(Guid CardId, string? Cursor, int? Limit) : IQuery<PagedResult<ActivityDto>>, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Card(CardId);

    public Permission Permission => Permission.View;
}

internal sealed class GetBoardActivityValidator : IValidator<GetBoardActivity>
{
    public void Validate(GetBoardActivity request, ValidationContext context) => PageRules.Limit(context, request.Limit);
}

internal sealed class GetCardActivityValidator : IValidator<GetCardActivity>
{
    public void Validate(GetCardActivity request, ValidationContext context) => PageRules.Limit(context, request.Limit);
}

internal sealed class GetBoardActivityHandler(IAppDbContext db) : IQueryHandler<GetBoardActivity, PagedResult<ActivityDto>>
{
    public Task<PagedResult<ActivityDto>> HandleAsync(GetBoardActivity query, CancellationToken cancellationToken) =>
        ActivityFeed.PageAsync(db, db.Activity.Where(a => a.BoardId == query.BoardId), query.Cursor, query.Limit, cancellationToken);
}

internal sealed class GetCardActivityHandler(IAppDbContext db) : IQueryHandler<GetCardActivity, PagedResult<ActivityDto>>
{
    public Task<PagedResult<ActivityDto>> HandleAsync(GetCardActivity query, CancellationToken cancellationToken) =>
        ActivityFeed.PageAsync(db, db.Activity.Where(a => a.CardId == query.CardId), query.Cursor, query.Limit, cancellationToken);
}

internal static class ActivityFeed
{
    public static async Task<PagedResult<ActivityDto>> PageAsync(
        IAppDbContext db, IQueryable<ActivityEntry> entries, string? cursor, int? limit, CancellationToken cancellationToken)
    {
        var take = limit ?? PageRules.DefaultLimit;

        if (cursor is not null)
        {
            var (at, id) = KeysetCursor.Decode(cursor);
            entries = entries.Where(a => a.CreatedAt < at || (a.CreatedAt == at && a.Id < id));
        }

        var rows = await entries
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Take(take + 1)
            .Join(db.Users, a => a.ActorId, u => u.Id, (a, u) => new { Entry = a, Actor = new ActorDto(u.Id, u.DisplayName, u.AvatarUrl) })
            .ToListAsync(cancellationToken);

        // The join may reorder rows; restore the feed order in memory (at most limit + 1 rows).
        rows = [.. rows.OrderByDescending(r => r.Entry.CreatedAt).ThenByDescending(r => r.Entry.Id)];

        return PagedResult<ActivityDto>.From(
            rows,
            take,
            r => new ActivityDto(r.Entry.Id, r.Entry.Type, r.Entry.BoardId, r.Entry.CardId, r.Actor, Parse(r.Entry.Data), r.Entry.CreatedAt),
            r => new KeysetCursor(r.Entry.CreatedAt, r.Entry.Id));
    }

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
