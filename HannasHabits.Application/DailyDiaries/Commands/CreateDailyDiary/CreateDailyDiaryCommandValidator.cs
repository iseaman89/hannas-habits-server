using FluentValidation;

namespace HannasHabits.Application.DailyDiaries.Commands.CreateDailyDiary;

public class CreateDailyDiaryCommandValidator : AbstractValidator<CreateDailyDiaryCommand>
{
    public CreateDailyDiaryCommandValidator()
    {
        RuleFor(x => x.Text)
            .NotEmpty()
            .MaximumLength(2000);
    }
}