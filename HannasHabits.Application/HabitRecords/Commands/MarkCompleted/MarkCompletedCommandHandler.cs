using HannasHabits.Application.Common.Interfaces;
using MapsterMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HannasHabits.Application.HabitRecords.Commands.MarkCompleted;

public class MarkCompletedCommandHandler : IRequestHandler<MarkCompletedCommand, HabitRecordDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUserContextService _userContextService;
    private readonly IMapper _mapper;

    public MarkCompletedCommandHandler(IApplicationDbContext context, IUserContextService userContextService, IMapper mapper)
    {
        _context = context;
        _userContextService = userContextService;
        _mapper = mapper;
    }

    public async Task<HabitRecordDto> Handle(MarkCompletedCommand request, CancellationToken cancellationToken)
    {
        var userId = _userContextService.UserId;
        if (userId is null) throw new UnauthorizedAccessException();
        
        var habit = await _context.Habits
            .Include(h => h.Records)
            .Where(h => h.Id == request.HabitId && h.UserId == userId.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (habit is null)
            throw new Exception("Habit not found");
        
        if (habit.Records.Any(r => r.Date == request.Date))
            throw new Exception("Record for this date already exists");

        var record = habit.MarkCompleted(request.Date);

        await _context.SaveChangesAsync(cancellationToken);

        return _mapper.Map<HabitRecordDto>(record);
    }
}