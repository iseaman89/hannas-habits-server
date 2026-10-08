using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Application.HabitRecords.Commands.MarkCompleted;
using HannasHabits.Domain.Entities;
using MapsterMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HannasHabits.Application.HabitRecords.Queries.GetRecordsByHabit;

public class GetRecordsByHabitQueryHandler : IRequestHandler<GetRecordsByHabitQuery, List<HabitRecordDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUserContextService _userContextService;
    private readonly IMapper _mapper;

    public GetRecordsByHabitQueryHandler(IApplicationDbContext context, IUserContextService userContextService, IMapper mapper)
    {
        _context = context;
        _userContextService = userContextService;
        _mapper = mapper;
    }

    public async Task<List<HabitRecordDto>> Handle(GetRecordsByHabitQuery request, CancellationToken cancellationToken)
    {
        var userId = _userContextService.UserId;
        if (userId is null) throw new UnauthorizedAccessException();
        
        var habitExists = await _context.Habits
            .AnyAsync(h => h.Id == request.HabitId && h.UserId == userId.Value, cancellationToken);

        if (!habitExists)
            throw new NotFoundException(nameof(Habit), request.HabitId);

        var records = await _context.HabitRecords
            .AsNoTracking()
            .Where(r => r.HabitId == request.HabitId)
            .Where(r => request.From == null || r.Date >= request.From)
            .Where(r => request.To == null || r.Date <= request.To)
            .OrderBy(r => r.Date)
            .ToListAsync(cancellationToken);

        return _mapper.Map<List<HabitRecordDto>>(records);
    }
}