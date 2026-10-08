using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Domain.Entities;
using MapsterMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HannasHabits.Application.Habits.Queries.GetHabitById;

public class GetHabitByIdQueryHandler : IRequestHandler<GetHabitByIdQuery, HabitDetailsDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUserContextService _userContextService;
    private readonly IMapper _mapper;

    public GetHabitByIdQueryHandler(IApplicationDbContext context, IUserContextService userContextService, IMapper mapper)
    {
        _context = context;
        _userContextService = userContextService;
        _mapper = mapper;
    }
    
    public async Task<HabitDetailsDto> Handle(GetHabitByIdQuery request, CancellationToken cancellationToken)
    {
        var userId = _userContextService.UserId;
        if (userId is null) throw new UnauthorizedAccessException();
        
        var habit = await _context.Habits
            .Where(h => h.Id == request.Id && h.UserId == userId.Value)
            .FirstOrDefaultAsync(cancellationToken);
        
        if (habit is null) throw new NotFoundException(nameof(Habit), request.Id);
        
        return _mapper.Map<HabitDetailsDto>(habit);
    }
}