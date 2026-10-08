using System.Linq.Expressions;
using FluentValidation;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Application.DailyDiaries.Commands.SaveDailyDiary;

// First line of defence with per-field messages; the Domain enforces the same rules again.
public class SaveDailyDiaryCommandValidator : AbstractValidator<SaveDailyDiaryCommand>
{
    public SaveDailyDiaryCommandValidator()
    {
        // Mood, Body and Mind are optional: null passes these rules.
        RuleFor(x => x.Mood).IsInEnum();
        RuleFor(x => x.Body).InclusiveBetween(Percentage.MinValue, Percentage.MaxValue);
        RuleFor(x => x.Mind).InclusiveBetween(Percentage.MinValue, Percentage.MaxValue);

        RuleFor(x => x.Highlight).MaximumLength(DiaryContent.HighlightMaxLength);

        RulesForList(x => x.Grateful);
        RulesForList(x => x.Learned);

        RuleFor(x => x.Tasks)
            .Must(tasks => tasks is null || tasks.Count <= DiaryContent.MaxTasks)
            .WithMessage($"A day can have at most {DiaryContent.MaxTasks} tasks.");

        RuleForEach(x => x.Tasks).NotNull().ChildRules(task =>
            task.RuleFor(t => t.Title)
                .NotEmpty()
                .MaximumLength(DiaryTask.TitleMaxLength));
    }

    private void RulesForList(Expression<Func<SaveDailyDiaryCommand, IEnumerable<string>?>> list)
    {
        RuleFor(list)
            .Must(items => items is null || items.Count() <= DiaryContent.MaxItemsPerList)
            .WithMessage($"A list can have at most {DiaryContent.MaxItemsPerList} entries.");

        RuleForEach(list)
            .NotEmpty()
            .MaximumLength(DiaryContent.ItemMaxLength);
    }
}
