using HannasHabits.Domain.Entities;
using HannasHabits.Domain.Exceptions;

namespace HannasHabits.Domain.Tests.Entities;

public class HabitDayEntryTests
{
    private static readonly Guid HabitId = Guid.NewGuid();
    private static readonly DateOnly Day = new(2026, 10, 8);

    private static HabitDayEntry NewEntry() => HabitDayEntry.Create(HabitId, Day);

    // ---- creating ----

    [Fact]
    public void Create_StartsEmpty_ForTheHabitAndTheDay()
    {
        var entry = NewEntry();

        Assert.Equal(HabitId, entry.HabitId);
        Assert.Equal(Day, entry.Date);
        Assert.Empty(entry.Tags);
        Assert.True(entry.IsEmpty);
    }

    [Theory]
    [InlineData(2000, 1, 1)]
    [InlineData(2100, 12, 31)]
    public void Create_AcceptsTheEdgesOfThePlausibleRange(int year, int month, int day)
    {
        Assert.NotNull(HabitDayEntry.Create(HabitId, new DateOnly(year, month, day)));
    }

    [Theory]
    [InlineData(1999, 12, 31)]
    [InlineData(2101, 1, 1)]
    [InlineData(1, 1, 1)]
    public void Create_RejectsADayOutsideTheRange(int year, int month, int day)
    {
        Assert.Throws<DomainException>(() => HabitDayEntry.Create(HabitId, new DateOnly(year, month, day)));
    }

    // ---- adding ----

    [Fact]
    public void AddTag_PutsTheTagOnTheDay()
    {
        var entry = NewEntry();
        var tagId = Guid.NewGuid();

        entry.AddTag(tagId);

        var dayTag = Assert.Single(entry.Tags);
        Assert.Equal(tagId, dayTag.TagId);
        Assert.Equal(entry.Id, dayTag.HabitDayEntryId);
        Assert.True(entry.HasTag(tagId));
        Assert.False(entry.IsEmpty);
    }

    [Fact]
    public void AddTag_RejectsTheSameTagTwice()
    {
        var entry = NewEntry();
        var tagId = Guid.NewGuid();
        entry.AddTag(tagId);

        Assert.Throws<DomainException>(() => entry.AddTag(tagId));

        Assert.Single(entry.Tags);
    }

    [Fact]
    public void AddTag_AcceptsTenTags_AndRejectsTheEleventh()
    {
        var entry = NewEntry();

        for (var i = 0; i < HabitDayEntry.MaxTags; i++)
            entry.AddTag(Guid.NewGuid());

        var exception = Assert.Throws<DomainException>(() => entry.AddTag(Guid.NewGuid()));

        Assert.Equal(HabitDayEntry.MaxTags, entry.Tags.Count);
        Assert.Contains(HabitDayEntry.MaxTags.ToString(), exception.Message);
    }

    [Fact]
    public void TheLimitIs10()
    {
        Assert.Equal(10, HabitDayEntry.MaxTags);
    }

    // ---- removing ----

    [Fact]
    public void RemoveTag_TakesTheTagOffTheDay_OthersStay()
    {
        var entry = NewEntry();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        entry.AddTag(first);
        entry.AddTag(second);

        entry.RemoveTag(first);

        Assert.False(entry.HasTag(first));
        Assert.True(entry.HasTag(second));
    }

    [Fact]
    public void RemoveTag_OfTheLastTag_LeavesTheEntryEmpty()
    {
        var entry = NewEntry();
        var tagId = Guid.NewGuid();
        entry.AddTag(tagId);

        entry.RemoveTag(tagId);

        Assert.True(entry.IsEmpty);
    }

    [Fact]
    public void RemoveTag_RejectsATagTheDayDoesNotHave()
    {
        var entry = NewEntry();
        entry.AddTag(Guid.NewGuid());

        Assert.Throws<DomainException>(() => entry.RemoveTag(Guid.NewGuid()));

        Assert.Single(entry.Tags);
    }

    // ---- replacing (what a merge does to a day) ----

    [Fact]
    public void ReplaceTag_SwapsTheTag()
    {
        var entry = NewEntry();
        var old = Guid.NewGuid();
        var replacement = Guid.NewGuid();
        entry.AddTag(old);

        entry.ReplaceTag(old, replacement);

        Assert.False(entry.HasTag(old));
        Assert.Equal(replacement, Assert.Single(entry.Tags).TagId);
    }

    [Fact]
    public void ReplaceTag_WhenTheReplacementIsAlreadyThere_JustDropsTheOldTag()
    {
        var entry = NewEntry();
        var old = Guid.NewGuid();
        var replacement = Guid.NewGuid();
        entry.AddTag(old);
        entry.AddTag(replacement);

        entry.ReplaceTag(old, replacement);

        Assert.Equal(replacement, Assert.Single(entry.Tags).TagId);
    }

    [Fact]
    public void ReplaceTag_OnAFullDay_StillWorks()
    {
        var entry = NewEntry();
        var old = Guid.NewGuid();
        entry.AddTag(old);
        for (var i = 1; i < HabitDayEntry.MaxTags; i++)
            entry.AddTag(Guid.NewGuid());
        var replacement = Guid.NewGuid();

        entry.ReplaceTag(old, replacement);

        Assert.Equal(HabitDayEntry.MaxTags, entry.Tags.Count);
        Assert.True(entry.HasTag(replacement));
    }

    [Fact]
    public void ReplaceTag_WithItself_ChangesNothing()
    {
        var entry = NewEntry();
        var tagId = Guid.NewGuid();
        entry.AddTag(tagId);

        entry.ReplaceTag(tagId, tagId);

        Assert.Equal(tagId, Assert.Single(entry.Tags).TagId);
    }

    [Fact]
    public void ReplaceTag_RejectsATagTheDayDoesNotHave()
    {
        var entry = NewEntry();
        var other = Guid.NewGuid();
        entry.AddTag(other);

        Assert.Throws<DomainException>(() => entry.ReplaceTag(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(other, Assert.Single(entry.Tags).TagId);
    }

    // ---- shape ----

    [Fact]
    public void TheTagsAreExposedOnlyAsAReadOnlyView()
    {
        var property = typeof(HabitDayEntry).GetProperty(nameof(HabitDayEntry.Tags))!;

        Assert.DoesNotContain(property.PropertyType.GetInterfaces().Append(property.PropertyType),
            type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ICollection<>));
    }
}
