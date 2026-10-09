using HannasHabits.Domain.Exceptions;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Domain.Tests.ValueObjects;

public class HabitTitleTests
{
    [Fact]
    public void Create_KeepsTheText()
    {
        Assert.Equal("Read 20 pages", HabitTitle.Create("Read 20 pages").Value);
    }

    [Fact]
    public void Create_TrimsSurroundingWhitespace()
    {
        Assert.Equal("Read", HabitTitle.Create("  \tRead \n").Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Create_RejectsAMissingOrBlankTitle(string? value)
    {
        Assert.Throws<DomainException>(() => HabitTitle.Create(value));
    }

    [Fact]
    public void Create_AcceptsTheMaximumLength()
    {
        var title = new string('a', HabitTitle.MaxLength);

        Assert.Equal(title, HabitTitle.Create(title).Value);
    }

    [Fact]
    public void Create_RejectsOneCharacterTooMany()
    {
        Assert.Throws<DomainException>(() => HabitTitle.Create(new string('a', HabitTitle.MaxLength + 1)));
    }

    [Fact]
    public void Create_CountsTheLengthAfterTrimming()
    {
        var padded = "  " + new string('a', HabitTitle.MaxLength) + "  ";

        Assert.Equal(HabitTitle.MaxLength, HabitTitle.Create(padded).Value.Length);
    }

    [Fact]
    public void TitlesWithTheSameTextAreEqual()
    {
        var first = HabitTitle.Create("Read");
        var second = HabitTitle.Create("  Read ");

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, HabitTitle.Create("Run"));
    }

    [Fact]
    public void EqualityIsCaseSensitive()
    {
        Assert.NotEqual(HabitTitle.Create("read"), HabitTitle.Create("Read"));
    }

    [Fact]
    public void ToString_IsTheText()
    {
        Assert.Equal("Read", HabitTitle.Create("Read").ToString());
    }
}
