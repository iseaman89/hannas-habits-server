using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Habits.Commands.CreateHabit;
using HannasHabits.Application.Habits.Commands.DeleteHabit;
using HannasHabits.Application.Habits.Commands.UpdateHabit;
using HannasHabits.Application.Tests.Fakes;
using HannasHabits.Domain.Exceptions;
using HannasHabits.Domain.ValueObjects;
using Microsoft.Extensions.Time.Testing;

namespace HannasHabits.Application.Tests.Habits;

public class CreateHabitCommandHandlerTests
{
    private readonly FakeCurrentUser _user = new();
    private readonly FakeHabitRepository _habits = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTimeProvider _clock = TestClock.Create();

    private CreateHabitCommandHandler Handler() => new(_habits, _unitOfWork, TestMapper.Instance, _user, _clock);

    private Task<CreateHabitDto> Handle(CreateHabitCommand command) => Handler().Handle(command, CancellationToken.None);

    [Fact]
    public async Task CreatesTheHabitForTheCurrentUser_AndSavesOnce()
    {
        var dto = await Handle(new CreateHabitCommand { Title = "Read", Description = "20 pages" });

        var habit = Assert.Single(_habits.Habits);
        Assert.Equal(_user.UserId, habit.UserId);
        Assert.Equal("Read", habit.Title.Value);
        Assert.Equal("20 pages", habit.Description);
        Assert.Equal(1, _unitOfWork.SaveCount);
        Assert.Equal(habit.Id, dto.Id);
        Assert.Equal("Read", dto.Title);
    }

    [Fact]
    public async Task WithoutASchedule_TheHabitIsPlannedEveryDay()
    {
        var dto = await Handle(new CreateHabitCommand { Title = "Read" });

        Assert.Equal(HabitSchedule.Daily.Days, dto.Schedule);
        Assert.Equal(HabitSchedule.Daily, Assert.Single(_habits.Habits).Schedule);
    }

    [Fact]
    public async Task WithASchedule_TheDaysAreReturnedInWeekOrder()
    {
        var dto = await Handle(new CreateHabitCommand
        {
            Title = "Run", Schedule = [DayOfWeek.Friday, DayOfWeek.Monday, DayOfWeek.Friday]
        });

        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Friday], dto.Schedule);
    }

    [Fact]
    public async Task TheClientsStartDateIsUsed_WhateverTheServerClockSays()
    {
        var dto = await Handle(new CreateHabitCommand { Title = "Read", StartDate = new DateOnly(2026, 11, 2) });

        Assert.Equal(new DateOnly(2026, 11, 2), dto.StartDate);
        Assert.Equal(new DateOnly(2026, 11, 2), Assert.Single(_habits.Habits).StartDate);
    }

    [Fact]
    public async Task WithoutAStartDate_TheServersUtcDateIsTheFallback()
    {
        _clock.SetUtcNow(new DateTimeOffset(2026, 10, 8, 23, 30, 0, TimeSpan.Zero));
        Assert.Equal(new DateOnly(2026, 10, 8), (await Handle(new CreateHabitCommand { Title = "A" })).StartDate);

        _clock.SetUtcNow(new DateTimeOffset(2026, 10, 9, 0, 30, 0, TimeSpan.Zero));
        Assert.Equal(new DateOnly(2026, 10, 9), (await Handle(new CreateHabitCommand { Title = "B" })).StartDate);
    }

    [Fact]
    public async Task TitleAndDescriptionAreNormalized()
    {
        await Handle(new CreateHabitCommand { Title = "  Read  ", Description = "   " });

        var habit = Assert.Single(_habits.Habits);
        Assert.Equal("Read", habit.Title.Value);
        Assert.Null(habit.Description);
    }

    [Fact]
    public async Task ABrokenBusinessRule_IsADomainException_AndNothingIsSaved()
    {
        // The validator normally answers first; the Domain still guards every one of these on its own.
        await Assert.ThrowsAsync<DomainException>(() => Handle(new CreateHabitCommand { Title = "  " }));
        await Assert.ThrowsAsync<DomainException>(() => Handle(new CreateHabitCommand { Title = "Read", Schedule = [] }));
        await Assert.ThrowsAsync<DomainException>(() => Handle(
            new CreateHabitCommand { Title = "Read", StartDate = new DateOnly(1999, 12, 31) }));
        await Assert.ThrowsAsync<DomainException>(() => Handle(
            new CreateHabitCommand { Title = "Read", Description = new string('a', 501) }));

        Assert.Empty(_habits.Habits);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }
}

public class UpdateHabitCommandHandlerTests
{
    private readonly FakeCurrentUser _user = new();
    private readonly FakeHabitRepository _habits = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private Task Handle(UpdateHabitCommand command)
        => new UpdateHabitCommandHandler(_habits, _unitOfWork, _user).Handle(command, CancellationToken.None);

    [Fact]
    public async Task ReplacesTitleDescriptionAndSchedule_ButNotTheStartDate()
    {
        var habit = Make.NewHabit(_user.UserId, "Read", start: new DateOnly(2026, 3, 1), description: "old");
        _habits.Habits.Add(habit);

        await Handle(new UpdateHabitCommand
        {
            Id = habit.Id, Title = " Run ", Description = "new", Schedule = [DayOfWeek.Saturday, DayOfWeek.Sunday]
        });

        Assert.Equal("Run", habit.Title.Value);
        Assert.Equal("new", habit.Description);
        Assert.Equal([DayOfWeek.Sunday, DayOfWeek.Saturday], habit.Schedule.Days);
        Assert.Equal(new DateOnly(2026, 3, 1), habit.StartDate);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnUnknownHabit_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Handle(
            new UpdateHabitCommand { Id = Guid.NewGuid(), Title = "Run", Schedule = [DayOfWeek.Monday] }));

        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnotherUsersHabit_IsNotFound_AndStaysUntouched()
    {
        var foreign = Make.NewHabit(Guid.NewGuid(), "Read");
        _habits.Habits.Add(foreign);

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(
            new UpdateHabitCommand { Id = foreign.Id, Title = "Hacked", Schedule = [DayOfWeek.Monday] }));

        Assert.Equal("Read", foreign.Title.Value);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ARejectedUpdate_ChangesNothing_AndSavesNothing()
    {
        var habit = Make.NewHabit(_user.UserId, "Read", description: "old");
        _habits.Habits.Add(habit);

        await Assert.ThrowsAsync<DomainException>(() => Handle(new UpdateHabitCommand
        {
            Id = habit.Id, Title = "Run", Description = new string('a', 501), Schedule = [DayOfWeek.Monday]
        }));

        Assert.Equal("Read", habit.Title.Value);
        Assert.Equal("old", habit.Description);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }
}

public class DeleteHabitCommandHandlerTests
{
    private readonly FakeCurrentUser _user = new();
    private readonly FakeHabitRepository _habits = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private Task Handle(Guid id)
        => new DeleteHabitCommandHandler(_habits, _unitOfWork, _user).Handle(new DeleteHabitCommand(id), CancellationToken.None);

    [Fact]
    public async Task RemovesTheHabit_AndSaves()
    {
        var habit = Make.NewHabit(_user.UserId);
        _habits.Habits.Add(habit);

        await Handle(habit.Id);

        Assert.Same(habit, Assert.Single(_habits.Removed));
        Assert.Empty(_habits.Habits);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnUnknownHabit_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Handle(Guid.NewGuid()));

        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnotherUsersHabit_IsNotFound_AndStays()
    {
        var foreign = Make.NewHabit(Guid.NewGuid());
        _habits.Habits.Add(foreign);

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(foreign.Id));

        Assert.Same(foreign, Assert.Single(_habits.Habits));
        Assert.Empty(_habits.Removed);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }
}
