using FluentValidation;

namespace HannasHabits.Application.Habits.Queries.GetHabitsOverview;

public class GetHabitsOverviewQueryValidator : AbstractValidator<GetHabitsOverviewQuery>
{
    public GetHabitsOverviewQueryValidator()
    {
        RuleFor(x => x.From).NotNull();
        RuleFor(x => x.To).NotNull();

        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From)
            .When(x => x.From.HasValue && x.To.HasValue)
            .WithMessage("'To' must not be before 'From'.");
    }
}
