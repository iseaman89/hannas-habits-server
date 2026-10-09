using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Domain.Entities;
using HannasHabits.Domain.ValueObjects;
using MapsterMapper;
using MediatR;

namespace HannasHabits.Application.Habits.Commands.CreateHabit;

public class CreateHabitCommandHandler : IRequestHandler<CreateHabitCommand, CreateHabitDto>
{
    private readonly IHabitRepository _habits;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public CreateHabitCommandHandler(
        IHabitRepository habits, IUnitOfWork unitOfWork, IMapper mapper, ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _habits = habits;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<CreateHabitDto> Handle(CreateHabitCommand request, CancellationToken cancellationToken)
    {
        var schedule = request.Schedule is null ? null : HabitSchedule.Create(request.Schedule);

        // The client's date is authoritative (its local "today"); the server's UTC date is only the fallback.
        var startDate = request.StartDate ?? DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

        var habit = Habit.Create(
            _currentUser.UserId, HabitTitle.Create(request.Title), startDate, request.Description, schedule);

        _habits.Add(habit);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<CreateHabitDto>(habit);
    }
}
