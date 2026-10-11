using HannasHabits.Domain.Entities;
using HannasHabits.Domain.Exceptions;

namespace HannasHabits.Domain.Tests.Entities;

/// <summary>The note of a part on a day: what it may say and which days it may be on.</summary>
public class SubHabitNoteTests
{
    private static readonly Guid Part = Guid.NewGuid();
    private static readonly DateOnly Monday = new(2026, 10, 5);

    [Fact]
    public void ANewNote_HoldsThePartTheDayAndTheText()
    {
        var note = SubHabitNote.Create(Part, Monday, "3 x 10, felt strong");

        Assert.Equal(Part, note.SubHabitId);
        Assert.Equal(Monday, note.Date);
        Assert.Equal("3 x 10, felt strong", note.Text);
        Assert.NotEqual(Guid.Empty, note.Id);
    }

    [Fact]
    public void TheEnds_AreTrimmed_ButTheLineBreaksInside_Stay()
    {
        var note = SubHabitNote.Create(Part, Monday, "  squats\n\nlunges \r\n ");

        Assert.Equal("squats\n\nlunges", note.Text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n \t")]
    public void ANoteWithoutText_IsRefused(string? text)
    {
        Assert.Throws<DomainException>(() => SubHabitNote.Create(Part, Monday, text));
    }

    [Fact]
    public void TheText_MayBeAsLongAsTheLimit_NotLonger()
    {
        var atTheLimit = SubHabitNote.Create(Part, Monday, new string('a', SubHabitNote.TextMaxLength));

        Assert.Equal(SubHabitNote.TextMaxLength, atTheLimit.Text.Length);
        Assert.Throws<DomainException>(
            () => SubHabitNote.Create(Part, Monday, new string('a', SubHabitNote.TextMaxLength + 1)));
    }

    [Fact]
    public void TheLimit_CountsTheTrimmedText()
    {
        var padded = "  " + new string('a', SubHabitNote.TextMaxLength) + "  ";

        Assert.Equal(SubHabitNote.TextMaxLength, SubHabitNote.Create(Part, Monday, padded).Text.Length);
    }

    [Fact]
    public void ADayOutsideThePlausibleRange_IsRefused()
    {
        Assert.Throws<DomainException>(() => SubHabitNote.Create(Part, SubHabitNote.MinDate.AddDays(-1), "x"));
        Assert.Throws<DomainException>(() => SubHabitNote.Create(Part, SubHabitNote.MaxDate.AddDays(1), "x"));

        Assert.Equal(SubHabitNote.MinDate, SubHabitNote.Create(Part, SubHabitNote.MinDate, "x").Date);
        Assert.Equal(SubHabitNote.MaxDate, SubHabitNote.Create(Part, SubHabitNote.MaxDate, "x").Date);
    }

    [Fact]
    public void Rewrite_ReplacesTheText_AndKeepsPartAndDay()
    {
        var note = SubHabitNote.Create(Part, Monday, "first");

        note.Rewrite(" second ");

        Assert.Equal("second", note.Text);
        Assert.Equal(Part, note.SubHabitId);
        Assert.Equal(Monday, note.Date);
    }

    [Fact]
    public void ARejectedRewrite_LeavesTheNoteAsItWas()
    {
        var note = SubHabitNote.Create(Part, Monday, "first");

        Assert.Throws<DomainException>(() => note.Rewrite("   "));
        Assert.Throws<DomainException>(() => note.Rewrite(new string('a', SubHabitNote.TextMaxLength + 1)));

        Assert.Equal("first", note.Text);
    }
}
