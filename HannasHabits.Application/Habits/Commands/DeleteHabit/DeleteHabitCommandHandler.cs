using HannasHabits.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HannasHabits.Application.Habits.Commands.DeleteHabit;

public class DeleteHabitCommandHandler : IRequestHandler<DeleteHabitCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IUserContextService _userContextService;

    public DeleteHabitCommandHandler(IApplicationDbContext context, IUserContextService userContextService)
    {
        _context = context;
        _userContextService = userContextService;
    }

    public async Task<Unit> Handle(DeleteHabitCommand request, CancellationToken cancellationToken)
    {
        var userId = _userContextService.UserId;
        if (userId is null) throw new UnauthorizedAccessException();
        
        var habit = await _context.Habits
            .Where(h => h.Id == request.Id && h.UserId == userId.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (habit is null)
            throw new Exception("Habit not found");

        _context.Habits.Remove(habit);
        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}