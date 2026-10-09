using FluentValidation;

namespace HannasHabits.Application.HabitRecords.Queries.GetRecordsByHabit;

public class GetRecordsByHabitQueryValidator : AbstractValidator<GetRecordsByHabitQuery>
{
    public GetRecordsByHabitQueryValidator()
    {
        RuleFor(x => x.HabitId).NotEmpty();

        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From)
            .When(x => x.From.HasValue && x.To.HasValue)
            .WithMessage("'To' must not be before 'From'.");
    }
}
