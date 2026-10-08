using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Domain.Entities;
using MapsterMapper;
using MediatR;

namespace HannasHabits.Application.Habits.Commands.CreateHabit;

public class CreateHabitCommandHandler : IRequestHandler<CreateHabitCommand, CreateHabitDto>
{
    private readonly IHabitRepository _habits;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ICurrentUser _currentUser;

    public CreateHabitCommandHandler(IHabitRepository habits, IUnitOfWork unitOfWork, IMapper mapper, ICurrentUser currentUser)
    {
        _habits = habits;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _currentUser = currentUser;
    }

    public async Task<CreateHabitDto> Handle(CreateHabitCommand request, CancellationToken cancellationToken)
    {
        var habit = Habit.Create(_currentUser.UserId, request.Title, request.Description);

        _habits.Add(habit);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<CreateHabitDto>(habit);
    }
}
