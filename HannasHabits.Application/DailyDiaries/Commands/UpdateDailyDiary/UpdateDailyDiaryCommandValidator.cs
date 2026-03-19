using FluentValidation;

namespace HannasHabits.Application.DailyDiaries.Commands.UpdateDailyDiary;

public class UpdateDailyDiaryCommandValidator : AbstractValidator<UpdateDailyDiaryCommand>
{
    public UpdateDailyDiaryCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Text).NotEmpty().MaximumLength(2000);
    }
}