using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Domain.Entities;
using MapsterMapper;
using MediatR;

namespace HannasHabits.Application.Habits.Commands.CreateHabit;

public class CreateHabitCommandHandler : IRequestHandler<CreateHabitCommand, CreateHabitDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly IUserContextService _userContextService;

    public CreateHabitCommandHandler(IApplicationDbContext context, IMapper mapper, IUserContextService userContextService)
    {
        _context = context;
        _mapper = mapper;
        _userContextService = userContextService;
    }
    
    public async Task<CreateHabitDto> Handle(CreateHabitCommand request, CancellationToken cancellationToken)
    {
        var userId = _userContextService.UserId;
        if (userId is null) throw new UnauthorizedAccessException();
        
        var habit = Habit.Create(userId.Value, request.Title, request.Description);
        
        _context.Habits.Add(habit);
        await _context.SaveChangesAsync(cancellationToken);
        
        return _mapper.Map<CreateHabitDto>(habit);
    }
}