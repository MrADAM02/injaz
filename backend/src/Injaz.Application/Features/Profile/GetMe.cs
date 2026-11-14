using Injaz.Application.Common.Exceptions;
using Injaz.Application.Messaging;
using Injaz.Application.Persistence;
using Injaz.Application.Security;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Features.Profile;

public sealed record GetMe : IQuery<UserDto>, IAuthenticatedRequest;

internal sealed class GetMeHandler(IAppDbContext db, ICurrentUser currentUser) : IQueryHandler<GetMe, UserDto>
{
    public async Task<UserDto> HandleAsync(GetMe query, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException("user");

        return UserDto.From(user);
    }
}
