using FluentValidation;

namespace HannasHabits.Application.Habits.Queries.GetHabitsStatistics;

public class GetHabitsStatisticsQueryValidator : AbstractValidator<GetHabitsStatisticsQuery>
{
    /// <summary>
    /// The longest range (both ends included): a leap year. The answer is small, but the rule is evaluated day by day
    /// for every habit and every part of it, so an unbounded range would be unbounded work. A month is what the screen asks for.
    /// </summary>
    public const int MaxRangeDays = 366;

    public GetHabitsStatisticsQueryValidator()
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
