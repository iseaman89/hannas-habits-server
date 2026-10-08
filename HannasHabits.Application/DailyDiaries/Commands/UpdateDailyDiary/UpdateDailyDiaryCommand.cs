using MediatR;

namespace HannasHabits.Application.DailyDiaries.Commands.UpdateDailyDiary;

public class UpdateDailyDiaryCommand : IRequest<Unit>
{
    public Guid Id { get; set; }
    public string Text { get; set; } = default!;
}