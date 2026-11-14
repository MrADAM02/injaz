using Injaz.Application.Messaging;
using Injaz.Application.Persistence;
using Injaz.Application.Security;
using Injaz.Application.Validation;
using Injaz.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Features.Auth;

public sealed record Register(string? Email, string? Password, string? DisplayName, Locale? Locale)
    : ICommand<AuthSession>, IPublicRequest
{
    public override string ToString() => $"{nameof(Register)} {{ Email = {Email} }}";
}

internal sealed class RegisterValidator : IValidator<Register>
{
    public void Validate(Register request, ValidationContext context)
    {
        context.RuleFor(nameof(request.Email), request.Email).NotEmpty().MaxLength(User.EmailMaxLength).EmailAddress();
        context.RuleFor(nameof(request.Password), request.Password)
            .NotEmpty().MinLength(PasswordPolicy.MinLength).MaxLength(PasswordPolicy.MaxLength);
        context.RuleFor(nameof(request.DisplayName), request.DisplayName).NotEmpty().MaxLength(User.DisplayNameMaxLength);
        context.RuleFor(nameof(request.Locale), request.Locale).IsInEnum();
    }
}

internal sealed class RegisterHandler(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    SessionIssuer sessions,
    TimeProvider clock) : ICommandHandler<Register, AuthSession>
{
    public async Task<AuthSession> HandleAsync(Register command, CancellationToken cancellationToken)
    {
        var email = User.NormalizeEmail(command.Email!);

        // Fast path for a friendly error; the unique index still guards the race (→ conflict.duplicate).
        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw AuthErrors.EmailTaken();
        }

        var user = User.Register(
            email,
            command.DisplayName!,
            passwordHasher.Hash(command.Password!),
            command.Locale ?? Locale.Ar,
            clock.GetUtcNow());

        db.Users.Add(user);
        return sessions.Start(user);
    }
}
