using MediatR;

namespace HannasHabits.Application.DailyDiaries.Commands.CreateDailyDiary;

public class CreateDailyDiaryCommand : IRequest<CreateDailyDiaryDto>
{
    public DateOnly Date { get; set; }
    public string Text { get; set; } = default!;
}