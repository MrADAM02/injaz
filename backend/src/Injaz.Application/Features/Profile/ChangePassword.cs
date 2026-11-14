using Injaz.Application.Common.Exceptions;
using Injaz.Application.Features.Auth;
using Injaz.Application.Messaging;
using Injaz.Application.Persistence;
using Injaz.Application.Security;
using Injaz.Application.Validation;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Features.Profile;

/// <summary>
/// Changes the password and signs out every other session (all refresh tokens are revoked). The caller gets a
/// fresh session back so the current device stays signed in.
/// </summary>
public sealed record ChangePassword(string? CurrentPassword, string? NewPassword)
    : ICommand<AuthSession>, IAuthenticatedRequest
{
    public override string ToString() => nameof(ChangePassword);
}

internal sealed class ChangePasswordValidator : IValidator<ChangePassword>
{
    public void Validate(ChangePassword request, ValidationContext context)
    {
        context.RuleFor(nameof(request.CurrentPassword), request.CurrentPassword).NotEmpty().MaxLength(PasswordPolicy.MaxLength);
        context.RuleFor(nameof(request.NewPassword), request.NewPassword)
            .NotEmpty()
            .MinLength(PasswordPolicy.MinLength)
            .MaxLength(PasswordPolicy.MaxLength)
            .Must(p => p != request.CurrentPassword, "passwordUnchanged");
    }
}

internal sealed class ChangePasswordHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    IPasswordHasher passwordHasher,
    SessionIssuer sessions,
    TimeProvider clock) : ICommandHandler<ChangePassword, AuthSession>
{
    public async Task<AuthSession> HandleAsync(ChangePassword command, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException("user");

        if (passwordHasher.Verify(user.PasswordHash, command.CurrentPassword!) == PasswordVerification.Failed)
        {
            throw ValidationException.ForField(nameof(command.CurrentPassword), "invalidPassword");
        }

        var now = clock.GetUtcNow();
        user.ChangePasswordHash(passwordHasher.Hash(command.NewPassword!), now);

        var activeTokens = await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.Revoke(now);
        }

        return sessions.Start(user);
    }
}
