using FluentValidation;

namespace HannasHabits.Application.HabitRecords.Commands.MarkCompleted;

public class MarkCompletedCommandValidator : AbstractValidator<MarkCompletedCommand>
{
    public MarkCompletedCommandValidator()
    {
        RuleFor(x => x.HabitId).NotEmpty();
    }
}