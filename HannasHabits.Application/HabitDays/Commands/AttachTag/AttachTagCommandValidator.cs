using FluentValidation;
using HannasHabits.Domain.Entities;

namespace HannasHabits.Application.HabitDays.Commands.AttachTag;

public class AttachTagCommandValidator : AbstractValidator<AttachTagCommand>
{
    public AttachTagCommandValidator()
    {
        RuleFor(x => x.HabitId).NotEmpty();
        RuleFor(x => x.TagId).NotEmpty();
        RuleFor(x => x.Date).InclusiveBetween(HabitDayEntry.MinDate, HabitDayEntry.MaxDate);
    }
}
