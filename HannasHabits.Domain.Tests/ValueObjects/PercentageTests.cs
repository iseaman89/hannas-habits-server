using HannasHabits.Domain.Exceptions;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Domain.Tests.ValueObjects;

public class PercentageTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(99)]
    [InlineData(100)]
    public void Create_AcceptsTheWholeRange(int value)
    {
        Assert.Equal(value, Percentage.Create(value).Value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void Create_RejectsAValueOutsideTheRange(int value)
    {
        Assert.Throws<DomainException>(() => Percentage.Create(value));
    }

    [Fact]
    public void TheRangeConstantsAreZeroToOneHundred()
    {
        // The validators, the EF configuration and the check constraints are built from these.
        Assert.Equal(0, Percentage.MinValue);
        Assert.Equal(100, Percentage.MaxValue);
    }

    [Fact]
    public void PercentagesWithTheSameValueAreEqual()
    {
        Assert.Equal(Percentage.Create(42), Percentage.Create(42));
        Assert.NotEqual(Percentage.Create(42), Percentage.Create(43));
    }

    [Fact]
    public void ToString_AppendsThePercentSign()
    {
        Assert.Equal("42%", Percentage.Create(42).ToString());
    }
}
