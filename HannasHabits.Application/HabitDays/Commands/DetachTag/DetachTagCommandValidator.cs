using FluentValidation;
using HannasHabits.Domain.Entities;

namespace HannasHabits.Application.HabitDays.Commands.DetachTag;

public class DetachTagCommandValidator : AbstractValidator<DetachTagCommand>
{
    public DetachTagCommandValidator()
    {
        RuleFor(x => x.HabitId).NotEmpty();
        RuleFor(x => x.TagId).NotEmpty();
        RuleFor(x => x.Date).InclusiveBetween(HabitDayEntry.MinDate, HabitDayEntry.MaxDate);
    }
}
