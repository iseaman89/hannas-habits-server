using HannasHabits.Application.Common.Interfaces;
using MapsterMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HannasHabits.Application.Habits.Queries.GetAllHabits;

public class GetAllHabitsQueryHandler 
    : IRequestHandler<GetAllHabitsQuery, List<HabitListItemDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUserContextService _userContextService;
    private readonly IMapper _mapper;

    public GetAllHabitsQueryHandler(IApplicationDbContext context, IUserContextService userContextService, IMapper mapper)
    {
        _context = context;
        _userContextService = userContextService;
        _mapper = mapper;
    }

    public async Task<List<HabitListItemDto>> Handle(GetAllHabitsQuery request, CancellationToken cancellationToken)
    {
        var userId = _userContextService.UserId;
        if (userId is null) throw new UnauthorizedAccessException();
        
        var habits = await _context.Habits
            .Where(h => h.UserId == userId.Value)
            .ToListAsync(cancellationToken);

        return _mapper.Map<List<HabitListItemDto>>(habits);
    }
}