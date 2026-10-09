using FluentValidation;
using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Resolutions;
using HannasHabits.Application.Resolutions.Commands.AddResolution;
using HannasHabits.Application.Resolutions.Commands.DeleteResolution;
using HannasHabits.Application.Resolutions.Commands.UpdateResolution;
using HannasHabits.Application.Resolutions.Queries.GetResolutionsByYear;
using HannasHabits.Application.Tests.Fakes;
using HannasHabits.Domain.Entities;
using HannasHabits.Domain.Exceptions;

namespace HannasHabits.Application.Tests.Resolutions;

public class AddResolutionCommandHandlerTests
{
    private readonly FakeCurrentUser _user = new();
    private readonly FakeResolutionRepository _resolutions = new();
    private readonly FakeHabitRepository _habits = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private Task<ResolutionDto> Handle(AddResolutionCommand command)
        => new AddResolutionCommandHandler(_resolutions, _habits, _unitOfWork, _user).Handle(command, CancellationToken.None);

    [Fact]
    public async Task AddsAResolutionThatIsNotKeptYet_ToTheCurrentUsersYear()
    {
        var dto = await Handle(new AddResolutionCommand(2026, "  Read 12 books ", null));

        var stored = Assert.Single(_resolutions.Resolutions);
        Assert.Equal(_user.UserId, stored.UserId);
        Assert.Equal(2026, stored.Year);
        Assert.Equal("Read 12 books", stored.Title);
        Assert.False(stored.Kept);
        Assert.Equal(1, _unitOfWork.SaveCount);
        Assert.Equal(new ResolutionDto(stored.Id, "Read 12 books", false, null, null), dto);
    }

    [Fact]
    public async Task WithAHabit_TheAnswerCarriesTheHabitsTitle()
    {
        var habit = Make.NewHabit(_user.UserId, "Read every day");
        _habits.Habits.Add(habit);

        var dto = await Handle(new AddResolutionCommand(2026, "Read 12 books", habit.Id));

        Assert.Equal(habit.Id, dto.HabitId);
        Assert.Equal("Read every day", dto.HabitTitle);
        Assert.Equal(habit.Id, Assert.Single(_resolutions.Resolutions).HabitId);
    }

    [Fact]
    public async Task AnUnknownHabit_IsAValidationErrorOnHabitId_AndNothingIsStored()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => Handle(new AddResolutionCommand(2026, "Read", Guid.NewGuid())));

        Assert.Equal("habitId", Assert.Single(exception.Errors).PropertyName);
        Assert.Empty(_resolutions.Resolutions);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnotherUsersHabit_IsReportedExactlyLikeAnUnknownOne()
    {
        var foreign = Make.NewHabit(Guid.NewGuid(), "Not yours");
        _habits.Habits.Add(foreign);

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => Handle(new AddResolutionCommand(2026, "Read", foreign.Id)));

        Assert.Equal("habitId", Assert.Single(exception.Errors).PropertyName);
        Assert.Empty(_resolutions.Resolutions);
    }

    [Fact]
    public async Task TheCountOfTheCurrentUsersYearIsPassedToTheDomain_WhichRefusesTheFiftyFirst()
    {
        _resolutions.CountOverride = Resolution.MaxPerYear;

        await Assert.ThrowsAsync<DomainException>(() => Handle(new AddResolutionCommand(2027, "One too many", null)));

        Assert.Equal((_user.UserId, 2027), _resolutions.CountAskedFor);
        Assert.Empty(_resolutions.Resolutions);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task TheFiftiethResolution_IsStillAccepted()
    {
        _resolutions.CountOverride = Resolution.MaxPerYear - 1;

        await Handle(new AddResolutionCommand(2026, "Number fifty", null));

        Assert.Single(_resolutions.Resolutions);
    }

    [Fact]
    public async Task AYearOutsideTheRange_IsADomainException()
    {
        await Assert.ThrowsAsync<DomainException>(() => Handle(new AddResolutionCommand(1999, "Read", null)));
    }
}

public class UpdateResolutionCommandHandlerTests
{
    private readonly FakeCurrentUser _user = new();
    private readonly FakeResolutionRepository _resolutions = new();
    private readonly FakeHabitRepository _habits = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private Task Handle(UpdateResolutionCommand command)
        => new UpdateResolutionCommandHandler(_resolutions, _habits, _unitOfWork, _user).Handle(command, CancellationToken.None);

    private Resolution Existing(int year = 2026, Guid? habitId = null)
    {
        var resolution = Make.NewResolution(_user.UserId, year, "Read", habitId);
        _resolutions.Resolutions.Add(resolution);
        return resolution;
    }

    [Fact]
    public async Task ReplacesTitleKeptAndLink()
    {
        var habit = Make.NewHabit(_user.UserId);
        _habits.Habits.Add(habit);
        var resolution = Existing();

        await Handle(new UpdateResolutionCommand(2026, resolution.Id, "  Read more ", true, habit.Id));

        Assert.Equal("Read more", resolution.Title);
        Assert.True(resolution.Kept);
        Assert.Equal(habit.Id, resolution.HabitId);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task LeavingTheHabitOut_RemovesTheLink()
    {
        var habit = Make.NewHabit(_user.UserId);
        _habits.Habits.Add(habit);
        var resolution = Existing(habitId: habit.Id);

        await Handle(new UpdateResolutionCommand(2026, resolution.Id, "Read", false, null));

        Assert.Null(resolution.HabitId);
    }

    [Fact]
    public async Task KeptCanBeTakenBack()
    {
        var resolution = Existing();
        await Handle(new UpdateResolutionCommand(2026, resolution.Id, "Read", true, null));

        await Handle(new UpdateResolutionCommand(2026, resolution.Id, "Read", false, null));

        Assert.False(resolution.Kept);
    }

    [Fact]
    public async Task AnUnknownResolution_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => Handle(new UpdateResolutionCommand(2026, Guid.NewGuid(), "Read", true, null)));

        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task TheRightIdInTheWrongYear_IsNotFound()
    {
        var resolution = Existing(year: 2026);

        await Assert.ThrowsAsync<NotFoundException>(
            () => Handle(new UpdateResolutionCommand(2027, resolution.Id, "Hacked", true, null)));

        Assert.Equal("Read", resolution.Title);
        Assert.False(resolution.Kept);
    }

    [Fact]
    public async Task AnotherUsersResolution_IsNotFound_AndStaysUntouched()
    {
        var foreign = Make.NewResolution(Guid.NewGuid(), 2026, "Private");
        _resolutions.Resolutions.Add(foreign);

        await Assert.ThrowsAsync<NotFoundException>(
            () => Handle(new UpdateResolutionCommand(2026, foreign.Id, "Hacked", true, null)));

        Assert.Equal("Private", foreign.Title);
        Assert.False(foreign.Kept);
    }

    [Fact]
    public async Task LinkingAnotherUsersHabit_IsAValidationError_AndChangesNothing()
    {
        var foreignHabit = Make.NewHabit(Guid.NewGuid());
        _habits.Habits.Add(foreignHabit);
        var resolution = Existing();

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => Handle(new UpdateResolutionCommand(2026, resolution.Id, "Changed", true, foreignHabit.Id)));

        Assert.Equal("habitId", Assert.Single(exception.Errors).PropertyName);
        Assert.Equal("Read", resolution.Title);
        Assert.False(resolution.Kept);
        Assert.Null(resolution.HabitId);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ABlankTitle_IsADomainException_AndChangesNothing()
    {
        var resolution = Existing();

        await Assert.ThrowsAsync<DomainException>(
            () => Handle(new UpdateResolutionCommand(2026, resolution.Id, "  ", true, null)));

        Assert.Equal("Read", resolution.Title);
        Assert.False(resolution.Kept);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }
}

public class DeleteResolutionCommandHandlerTests
{
    private readonly FakeCurrentUser _user = new();
    private readonly FakeResolutionRepository _resolutions = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private Task Handle(int year, Guid id)
        => new DeleteResolutionCommandHandler(_resolutions, _unitOfWork, _user)
            .Handle(new DeleteResolutionCommand(year, id), CancellationToken.None);

    [Fact]
    public async Task RemovesTheResolution_AndSaves()
    {
        var resolution = Make.NewResolution(_user.UserId, 2026);
        _resolutions.Resolutions.Add(resolution);

        await Handle(2026, resolution.Id);

        Assert.Same(resolution, Assert.Single(_resolutions.Removed));
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnUnknownResolution_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Handle(2026, Guid.NewGuid()));
    }

    [Fact]
    public async Task TheWrongYear_IsNotFound_AndTheResolutionStays()
    {
        var resolution = Make.NewResolution(_user.UserId, 2026);
        _resolutions.Resolutions.Add(resolution);

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(2027, resolution.Id));

        Assert.Same(resolution, Assert.Single(_resolutions.Resolutions));
    }

    [Fact]
    public async Task AnotherUsersResolution_IsNotFound_AndStays()
    {
        var foreign = Make.NewResolution(Guid.NewGuid(), 2026);
        _resolutions.Resolutions.Add(foreign);

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(2026, foreign.Id));

        Assert.Same(foreign, Assert.Single(_resolutions.Resolutions));
        Assert.Equal(0, _unitOfWork.SaveCount);
    }
}

public class GetResolutionsByYearQueryHandlerTests
{
    [Fact]
    public async Task ReturnsWhatTheReadSideSays_ForTheCurrentUserAndYear()
    {
        var user = new FakeCurrentUser();
        var item = new ResolutionDto(Guid.NewGuid(), "Read", true, null, null);
        var queries = new FakeResolutionQueries { Result = [item] };

        var result = await new GetResolutionsByYearQueryHandler(queries, user)
            .Handle(new GetResolutionsByYearQuery(2026), CancellationToken.None);

        Assert.Equal([item], result);
        Assert.Equal((user.UserId, 2026), queries.AskedFor);
    }

    [Fact]
    public async Task AYearWithoutResolutions_IsAnEmptyList_NotAnError()
    {
        var result = await new GetResolutionsByYearQueryHandler(new FakeResolutionQueries(), new FakeCurrentUser())
            .Handle(new GetResolutionsByYearQuery(2031), CancellationToken.None);

        Assert.Empty(result);
    }
}
