using HannasHabits.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HannasHabits.Application.HabitRecords.Commands.UnmarkCompleted;

public class UnmarkCompletedCommandHandler : IRequestHandler<UnmarkCompletedCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IUserContextService _userContextService;

    public UnmarkCompletedCommandHandler(IApplicationDbContext context, IUserContextService userContextService)
    {
        _context = context;
        _userContextService = userContextService;
    }

    public async Task<Unit> Handle(UnmarkCompletedCommand request, CancellationToken cancellationToken)
    {
        var userId = _userContextService.UserId;
        if (userId is null) throw new UnauthorizedAccessException();
        
        var habit = await _context.Habits
            .Include(h => h.Records)
            .Where(h => h.Id == request.HabitId && h.UserId == userId.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (habit is null)
            throw new Exception("Habit not found");

        var record = habit.Records.FirstOrDefault(r => r.Date == request.Date);

        if (record is null)
            throw new Exception("Record not found for this date");

        _context.HabitRecords.Remove(record);

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}