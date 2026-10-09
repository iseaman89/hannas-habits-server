using HannasHabits.Domain.Exceptions;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Domain.Tests.ValueObjects;

public class DiaryTaskTests
{
    [Fact]
    public void Create_KeepsTitleAndState()
    {
        var task = DiaryTask.Create("Call mum", done: true);

        Assert.Equal("Call mum", task.Title);
        Assert.True(task.Done);
    }

    [Fact]
    public void Create_TrimsTheTitle()
    {
        Assert.Equal("Call mum", DiaryTask.Create("  Call mum\t", false).Title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsAMissingOrBlankTitle(string? title)
    {
        Assert.Throws<DomainException>(() => DiaryTask.Create(title, false));
    }

    [Fact]
    public void Create_AcceptsTheMaximumLength_AndRejectsOneMore()
    {
        var longest = new string('a', DiaryTask.TitleMaxLength);

        Assert.Equal(longest, DiaryTask.Create(longest, false).Title);
        Assert.Throws<DomainException>(() => DiaryTask.Create(longest + "a", false));
    }

    [Fact]
    public void TasksWithTheSameTitleAndStateAreEqual()
    {
        Assert.Equal(DiaryTask.Create("Call mum", true), DiaryTask.Create(" Call mum ", true));
        Assert.NotEqual(DiaryTask.Create("Call mum", true), DiaryTask.Create("Call mum", false));
        Assert.NotEqual(DiaryTask.Create("Call mum", true), DiaryTask.Create("Call dad", true));
    }
}
