using Injaz.Application.Common.Exceptions;
using Injaz.Application.Messaging;
using Injaz.Application.Persistence;
using Injaz.Application.Security;
using Injaz.Application.Validation;
using Injaz.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Features.Profile;

/// <summary>
/// Partial update (PATCH): a null field is left unchanged. To remove the avatar, send an empty <c>avatarUrl</c>.
/// </summary>
public sealed record UpdateProfile(string? DisplayName, string? AvatarUrl, Locale? Locale)
    : ICommand<UserDto>, IAuthenticatedRequest;

internal sealed class UpdateProfileValidator : IValidator<UpdateProfile>
{
    public void Validate(UpdateProfile request, ValidationContext context)
    {
        if (request.DisplayName is not null)
        {
            context.RuleFor(nameof(request.DisplayName), request.DisplayName).NotEmpty().MaxLength(User.DisplayNameMaxLength);
        }

        context.RuleFor(nameof(request.AvatarUrl), request.AvatarUrl).MaxLength(User.AvatarUrlMaxLength).HttpUrl();
        context.RuleFor(nameof(request.Locale), request.Locale).IsInEnum();
    }
}

internal sealed class UpdateProfileHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<UpdateProfile, UserDto>
{
    public async Task<UserDto> HandleAsync(UpdateProfile command, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException("user");

        var now = clock.GetUtcNow();

        if (command.DisplayName is not null)
        {
            user.Rename(command.DisplayName, now);
        }

        if (command.AvatarUrl is not null)
        {
            user.ChangeAvatar(command.AvatarUrl, now);
        }

        if (command.Locale is { } locale)
        {
            user.ChangeLocale(locale, now);
        }

        return UserDto.From(user);
    }
}
