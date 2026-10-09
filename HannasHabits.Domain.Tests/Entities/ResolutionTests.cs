using HannasHabits.Domain.Entities;
using HannasHabits.Domain.Exceptions;

namespace HannasHabits.Domain.Tests.Entities;

public class ResolutionTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static Resolution New(string? title = "Read 12 books", int year = 2026, Guid? habitId = null, int inYear = 0)
        => Resolution.Create(UserId, year, title, habitId, inYear);

    // ---- Create ----

    [Fact]
    public void Create_StartsNotKept_WithTheGivenValues()
    {
        var habitId = Guid.NewGuid();

        var resolution = Resolution.Create(UserId, 2026, "Read 12 books", habitId, resolutionsInYear: 3);

        Assert.Equal(UserId, resolution.UserId);
        Assert.Equal(2026, resolution.Year);
        Assert.Equal("Read 12 books", resolution.Title);
        Assert.False(resolution.Kept);
        Assert.Equal(habitId, resolution.HabitId);
        Assert.NotEqual(Guid.Empty, resolution.Id);
    }

    [Fact]
    public void Create_WithoutAHabit_HasNoLink()
    {
        Assert.Null(New().HabitId);
    }

    [Fact]
    public void Create_TrimsTheTitle()
    {
        Assert.Equal("Read", New("  Read\t").Title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsAMissingOrBlankTitle(string? title)
    {
        Assert.Throws<DomainException>(() => New(title));
    }

    [Fact]
    public void Create_AcceptsTheMaximumTitleLength_AndRejectsOneMore()
    {
        var longest = new string('a', Resolution.TitleMaxLength);

        Assert.Equal(longest, New(longest).Title);
        Assert.Throws<DomainException>(() => New(longest + "a"));
    }

    [Theory]
    [InlineData(2000)]
    [InlineData(2026)]
    [InlineData(2100)]
    public void Create_AcceptsAYearInsideTheRange(int year)
    {
        Assert.Equal(year, New(year: year).Year);
    }

    [Theory]
    [InlineData(1999)]
    [InlineData(2101)]
    [InlineData(0)]
    [InlineData(-2026)]
    [InlineData(int.MaxValue)]
    public void Create_RejectsAYearOutsideTheRange(int year)
    {
        Assert.Throws<DomainException>(() => New(year: year));
    }

    [Fact]
    public void Create_AllowsTheFiftiethResolutionOfAYear_ButNotTheFiftyFirst()
    {
        // The caller passes how many exist already: 49 -> this one is number 50, 50 -> number 51.
        Assert.NotNull(New(inYear: Resolution.MaxPerYear - 1));
        Assert.Throws<DomainException>(() => New(inYear: Resolution.MaxPerYear));
        Assert.Throws<DomainException>(() => New(inYear: Resolution.MaxPerYear + 10));
    }

    [Fact]
    public void TheLimitsAreTheDocumentedOnes()
    {
        // The validators, the EF configuration and the check constraint are built from these.
        Assert.Equal(200, Resolution.TitleMaxLength);
        Assert.Equal(50, Resolution.MaxPerYear);
        Assert.Equal(2000, Resolution.MinYear);
        Assert.Equal(2100, Resolution.MaxYear);
    }

    // ---- Update ----

    [Fact]
    public void Update_ReplacesTitleKeptAndLink()
    {
        var resolution = New("Read", habitId: Guid.NewGuid());
        var newHabit = Guid.NewGuid();

        resolution.Update("  Read more ", kept: true, newHabit);

        Assert.Equal("Read more", resolution.Title);
        Assert.True(resolution.Kept);
        Assert.Equal(newHabit, resolution.HabitId);
    }

    [Fact]
    public void Update_WithoutAHabit_RemovesTheLink()
    {
        var resolution = New(habitId: Guid.NewGuid());

        resolution.Update(resolution.Title, kept: false, habitId: null);

        Assert.Null(resolution.HabitId);
    }

    [Fact]
    public void Update_CanTakeKeptBack()
    {
        var resolution = New();
        resolution.Update(resolution.Title, kept: true, habitId: null);

        resolution.Update(resolution.Title, kept: false, habitId: null);

        Assert.False(resolution.Kept);
    }

    [Fact]
    public void Update_KeepsOwnerYearAndIdentity()
    {
        var resolution = New(year: 2027);
        var id = resolution.Id;

        resolution.Update("Something else", kept: true, habitId: null);

        Assert.Equal(id, resolution.Id);
        Assert.Equal(UserId, resolution.UserId);
        Assert.Equal(2027, resolution.Year);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void ARejectedUpdate_ChangesNothing(string? badTitle)
    {
        var habitId = Guid.NewGuid();
        var resolution = New("Read", habitId: habitId);

        Assert.Throws<DomainException>(() => resolution.Update(badTitle, kept: true, habitId: null));

        Assert.Equal("Read", resolution.Title);
        Assert.False(resolution.Kept);
        Assert.Equal(habitId, resolution.HabitId);
    }
}
