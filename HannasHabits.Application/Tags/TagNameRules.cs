using FluentValidation;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Application.Tags;

internal static class TagNameRules
{
    /// <summary>
    /// First line of defence with a per-field message; the Domain enforces the same rules again. The length is the one
    /// the Domain keeps: after trimming ("  krank " is a fine name).
    /// </summary>
    public static IRuleBuilderOptions<T, string?> MustBeATagName<T>(this IRuleBuilder<T, string?> rule)
        => rule
            .NotEmpty()
            .Must(name => name is null || name.Trim().Length <= TagName.MaxLength)
            .WithMessage($"The name must not be longer than {TagName.MaxLength} characters.");
}
