using FluentValidation;

namespace HannasHabits.Application.Habits.Queries.GetHabitsOverview;

public class GetHabitsOverviewQueryValidator : AbstractValidator<GetHabitsOverviewQuery>
{
    /// <summary>
    /// The longest range (both ends included): a leap year. The answer lists every due day of the range for every habit,
    /// so an unbounded range would be an unbounded answer; a month (the grid) or a day is what the screens ask for.
    /// </summary>
    public const int MaxRangeDays = 366;

    public GetHabitsOverviewQueryValidator()
    {
        RuleFor(x => x.From).NotNull();
        RuleFor(x => x.To).NotNull();

        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From)
            .When(x => x.From.HasValue && x.To.HasValue)
            .WithMessage("'To' must not be before 'From'.");

        RuleFor(x => x.To)
            .Must((query, to) => to!.Value.DayNumber - query.From!.Value.DayNumber < MaxRangeDays)
            .When(x => x.From.HasValue && x.To.HasValue && x.To >= x.From)
            .WithMessage($"The range must not be longer than {MaxRangeDays} days.");
    }
}
