using HannasHabits.Domain.Entities;
using HannasHabits.Domain.Enums;
using HannasHabits.Domain.Exceptions;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Domain.Tests.Entities;

public class DailyDiaryTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly Day = new(2026, 10, 8);

    private static DiaryContent Content(
        Mood? mood = null, int? body = null, int? mind = null, string? highlight = null,
        string[]? grateful = null, string[]? learned = null, DiaryTask[]? tasks = null)
        => DiaryContent.Create(
            mood,
            body is { } b ? Percentage.Create(b) : null,
            mind is { } m ? Percentage.Create(m) : null,
            highlight, grateful, learned, tasks);

    [Fact]
    public void Create_TakesEverythingFromTheContent()
    {
        var tasks = new[] { DiaryTask.Create("Call mum", true), DiaryTask.Create("Run", false) };
        var content = Content(Mood.Good, 70, 40, "A good day", ["coffee"], ["some Rust"], tasks);

        var diary = DailyDiary.Create(UserId, Day, content);

        Assert.Equal(UserId, diary.UserId);
        Assert.Equal(Day, diary.Date);
        Assert.Equal(Mood.Good, diary.Mood);
        Assert.Equal(Percentage.Create(70), diary.Body);
        Assert.Equal(Percentage.Create(40), diary.Mind);
        Assert.Equal("A good day", diary.Highlight);
        Assert.Equal(["coffee"], diary.Grateful);
        Assert.Equal(["some Rust"], diary.Learned);
        Assert.Equal(tasks, diary.Tasks);
        Assert.NotEqual(Guid.Empty, diary.Id);
    }

    [Fact]
    public void Create_AnEntryMayBeJustAMood()
    {
        var diary = DailyDiary.Create(UserId, Day, Content(mood: Mood.Bad));

        Assert.Equal(Mood.Bad, diary.Mood);
        Assert.Null(diary.Highlight);
        Assert.Empty(diary.Grateful);
        Assert.Empty(diary.Tasks);
    }

    [Fact]
    public void Create_RejectsEmptyContent_AnEntryIsNeverStoredEmpty()
    {
        Assert.Throws<DomainException>(() => DailyDiary.Create(UserId, Day, Content()));
    }

    [Fact]
    public void Replace_OverwritesEverything_AndClearsWhatTheNewContentLacks()
    {
        var diary = DailyDiary.Create(UserId, Day, Content(
            Mood.Good, 70, 40, "old highlight", ["a"], ["b"], [DiaryTask.Create("old task", false)]));

        diary.Replace(Content(highlight: "new highlight", grateful: ["x", "y"]));

        Assert.Null(diary.Mood);
        Assert.Null(diary.Body);
        Assert.Null(diary.Mind);
        Assert.Equal("new highlight", diary.Highlight);
        Assert.Equal(["x", "y"], diary.Grateful);
        Assert.Empty(diary.Learned);
        Assert.Empty(diary.Tasks);
    }

    [Fact]
    public void Replace_KeepsIdentityOwnerAndDate()
    {
        var diary = DailyDiary.Create(UserId, Day, Content(mood: Mood.Ok));
        var id = diary.Id;
        var createdAt = diary.CreatedAt;

        diary.Replace(Content(mood: Mood.Excellent));

        Assert.Equal(id, diary.Id);
        Assert.Equal(createdAt, diary.CreatedAt);
        Assert.Equal(UserId, diary.UserId);
        Assert.Equal(Day, diary.Date);
    }

    [Fact]
    public void Replace_RejectsEmptyContent_AndLeavesTheEntryAsItWas()
    {
        var diary = DailyDiary.Create(UserId, Day, Content(Mood.Good, highlight: "keep me"));

        Assert.Throws<DomainException>(() => diary.Replace(Content()));

        Assert.Equal(Mood.Good, diary.Mood);
        Assert.Equal("keep me", diary.Highlight);
    }
}
