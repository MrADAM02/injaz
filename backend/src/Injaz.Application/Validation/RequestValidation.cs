using Injaz.Application.Common.Exceptions;

namespace Injaz.Application.Validation;

internal static class RequestValidation
{
    public static void ThrowIfInvalid<TRequest>(TRequest request, IEnumerable<IValidator<TRequest>> validators)
    {
        ValidationContext? context = null;

        foreach (var validator in validators)
        {
            context ??= new ValidationContext();
            validator.Validate(request, context);
        }

        if (context is { IsValid: false })
        {
            throw new ValidationException(context.Errors);
        }
    }
}
