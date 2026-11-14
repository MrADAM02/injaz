using Injaz.Application.Common;
using Injaz.Application.Common.Exceptions;
using Injaz.Application.Messaging;
using Injaz.Application.Persistence;
using Injaz.Application.Realtime;
using Injaz.Application.Security;
using Injaz.Application.Validation;
using Injaz.Domain.Boards;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Features.Boards;

// ---------- create (Member+) ----------

public sealed record CreateLabel(Guid BoardId, string? Name, string? Color) : ICommand<LabelDto>, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Board(BoardId);

    public Permission Permission => Permission.EditContent;
}

internal sealed class CreateLabelValidator : IValidator<CreateLabel>
{
    public void Validate(CreateLabel request, ValidationContext context)
    {
        context.RuleFor(nameof(request.Name), request.Name).MaxLength(Label.NameMaxLength);
        context.RuleFor(nameof(request.Color), request.Color).NotEmpty().Must(Palette.IsValid, ValidationCodes.InvalidValue);
    }
}

internal sealed class CreateLabelHandler(IAppDbContext db, IBoardEvents events) : ICommandHandler<CreateLabel, LabelDto>
{
    public Task<LabelDto> HandleAsync(CreateLabel command, CancellationToken cancellationToken)
    {
        var label = Label.Create(command.BoardId, command.Name, command.Color!);
        db.Labels.Add(label);
        events.LabelChanged(label.BoardId, label.Id);
        return Task.FromResult(LabelDto.From(label));
    }
}

// ---------- update (Member+) ----------

public sealed record UpdateLabel(Guid LabelId, Optional<string?> Name, Optional<string?> Color) : ICommand<LabelDto>, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Label(LabelId);

    public Permission Permission => Permission.EditContent;
}

internal sealed class UpdateLabelValidator : IValidator<UpdateLabel>
{
    public void Validate(UpdateLabel request, ValidationContext context)
    {
        if (request.Name.HasValue)
        {
            context.RuleFor(nameof(request.Name), request.Name.Value).MaxLength(Label.NameMaxLength);
        }

        if (request.Color.HasValue)
        {
            context.RuleFor(nameof(request.Color), request.Color.Value).Must(Palette.IsValid, ValidationCodes.InvalidValue);
        }
    }
}

internal sealed class UpdateLabelHandler(IAppDbContext db, IBoardEvents events) : ICommandHandler<UpdateLabel, LabelDto>
{
    public async Task<LabelDto> HandleAsync(UpdateLabel command, CancellationToken cancellationToken)
    {
        var label = await db.Labels.SingleOrDefaultAsync(l => l.Id == command.LabelId, cancellationToken)
            ?? throw new NotFoundException("label");
        events.LabelChanged(label.BoardId, label.Id);

        if (command.Name.HasValue)
        {
            label.Rename(command.Name.Value);
        }

        if (command.Color.HasValue)
        {
            label.Recolor(command.Color.Value!);
        }

        return LabelDto.From(label);
    }
}

// ---------- delete (Member+): removes it from every card on the board ----------

public sealed record DeleteLabel(Guid LabelId) : ICommand, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Label(LabelId);

    public Permission Permission => Permission.EditContent;
}

internal sealed class DeleteLabelHandler(IAppDbContext db, IBoardEvents events) : ICommandHandler<DeleteLabel, Unit>
{
    public async Task<Unit> HandleAsync(DeleteLabel command, CancellationToken cancellationToken)
    {
        var label = await db.Labels.SingleOrDefaultAsync(l => l.Id == command.LabelId, cancellationToken)
            ?? throw new NotFoundException("label");

        db.Labels.Remove(label); // card_labels rows go with it (FK cascade); clients strip it from their tiles
        events.LabelDeleted(label.BoardId, label.Id);
        return Unit.Value;
    }
}
