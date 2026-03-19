using HannasHabits.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HannasHabits.Application.Habits.Commands.UpdateHabit;

public class UpdateHabitCommandHandler : IRequestHandler<UpdateHabitCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IUserContextService _userContextService;

    public UpdateHabitCommandHandler(IApplicationDbContext context, IUserContextService userContextService)
    {
        _context = context;
        _userContextService = userContextService;
    }

    public async Task<Unit> Handle(UpdateHabitCommand request, CancellationToken cancellationToken)
    {
        var userId = _userContextService.UserId;
        if (userId is null) throw new UnauthorizedAccessException();
        
        var habit = await _context.Habits
            .Where(h => h.Id == request.Id && h.UserId == userId.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (habit is null)
            throw new Exception("Habit not found");

        habit.Update(request.Title, request.Description);

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}