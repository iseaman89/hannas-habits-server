using FluentValidation;

namespace HannasHabits.Application.DailyDiaries.Queries.GetDailyDiaryDays;

public class GetDailyDiaryDaysQueryValidator : AbstractValidator<GetDailyDiaryDaysQuery>
{
    public GetDailyDiaryDaysQueryValidator()
    {
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From)
            .When(x => x.From.HasValue && x.To.HasValue)
            .WithMessage("'To' must not be before 'From'.");
    }
}
