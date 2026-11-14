using Injaz.Application.Messaging;
using Injaz.Application.Persistence;
using Injaz.Application.Security;
using Injaz.Application.Validation;
using Injaz.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Features.Auth;

public sealed record Login(string? Email, string? Password) : ICommand<AuthSession>, IPublicRequest
{
    public override string ToString() => $"{nameof(Login)} {{ Email = {Email} }}";
}

internal sealed class LoginValidator : IValidator<Login>
{
    // Deliberately no password-policy rules here: login shouldn't reveal the policy, only bound the input size.
    public void Validate(Login request, ValidationContext context)
    {
        context.RuleFor(nameof(request.Email), request.Email).NotEmpty().MaxLength(User.EmailMaxLength);
        context.RuleFor(nameof(request.Password), request.Password).NotEmpty().MaxLength(PasswordPolicy.MaxLength);
    }
}

internal sealed class LoginHandler(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    SessionIssuer sessions,
    TimeProvider clock) : ICommandHandler<Login, AuthSession>
{
    private static string? s_dummyHash;

    public async Task<AuthSession> HandleAsync(Login command, CancellationToken cancellationToken)
    {
        var email = User.NormalizeEmail(command.Email!);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null)
        {
            // Spend the same hashing time as a real check so response timing doesn't reveal which emails exist.
            s_dummyHash ??= passwordHasher.Hash(Guid.NewGuid().ToString());
            passwordHasher.Verify(s_dummyHash, command.Password!);
            throw AuthErrors.InvalidCredentials();
        }

        var verification = passwordHasher.Verify(user.PasswordHash, command.Password!);
        if (verification == PasswordVerification.Failed)
        {
            throw AuthErrors.InvalidCredentials();
        }

        if (verification == PasswordVerification.SuccessRehashNeeded)
        {
            user.ChangePasswordHash(passwordHasher.Hash(command.Password!), clock.GetUtcNow());
        }

        return sessions.Start(user);
    }
}
