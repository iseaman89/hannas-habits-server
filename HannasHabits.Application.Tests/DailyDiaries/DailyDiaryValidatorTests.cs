using FluentValidation.TestHelper;
using HannasHabits.Application.DailyDiaries;
using HannasHabits.Application.DailyDiaries.Commands.SaveDailyDiary;
using HannasHabits.Application.DailyDiaries.Queries.GetDailyDiaryDays;
using HannasHabits.Domain.Enums;

namespace HannasHabits.Application.Tests.DailyDiaries;

public class SaveDailyDiaryCommandValidatorTests
{
    private readonly SaveDailyDiaryCommandValidator _validator = new();
    private static readonly DateOnly Day = new(2026, 10, 8);

    private static SaveDailyDiaryCommand Command(
        Mood? mood = null, int? body = null, int? mind = null, string? highlight = null,
        string[]? grateful = null, string[]? learned = null, DiaryTaskDto[]? tasks = null)
        => new(Day, mood, body, mind, highlight, grateful, learned, tasks);

    [Fact]
    public void AnEmptyDocument_IsValid_TheHandlerTurnsItIntoADelete()
    {
        _validator.TestValidate(Command()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AFullDocumentAtEveryLimit_IsValid()
    {
        var items = Enumerable.Range(0, 30).Select(_ => new string('a', 200)).ToArray();
        var tasks = Enumerable.Range(0, 50).Select(_ => new DiaryTaskDto(new string('t', 200), true)).ToArray();

        _validator.TestValidate(Command(Mood.Excellent, 100, 0, new string('h', 5000), items, items, tasks))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(0, false)]
    [InlineData(6, false)]
    [InlineData(-1, false)]
    public void TheMood_MustBeOneOfTheFive(int mood, bool valid)
    {
        var result = _validator.TestValidate(Command(mood: (Mood)mood));

        if (valid)
            result.ShouldNotHaveValidationErrorFor(x => x.Mood);
        else
            result.ShouldHaveValidationErrorFor(x => x.Mood);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(100, true)]
    [InlineData(-1, false)]
    [InlineData(101, false)]
    public void BodyAndMind_MustBeBetween0And100(int value, bool valid)
    {
        var result = _validator.TestValidate(Command(body: value, mind: value));

        if (valid)
        {
            result.ShouldNotHaveValidationErrorFor(x => x.Body);
            result.ShouldNotHaveValidationErrorFor(x => x.Mind);
        }
        else
        {
            result.ShouldHaveValidationErrorFor(x => x.Body);
            result.ShouldHaveValidationErrorFor(x => x.Mind);
        }
    }

    [Fact]
    public void TheHighlight_MayHave5000Characters_NotMore()
    {
        _validator.TestValidate(Command(highlight: new string('a', 5000))).ShouldNotHaveValidationErrorFor(x => x.Highlight);
        _validator.TestValidate(Command(highlight: new string('a', 5001))).ShouldHaveValidationErrorFor(x => x.Highlight);
    }

    // ---- the lists: the error says which entry is wrong ----

    [Fact]
    public void ABlankListEntry_IsRejected_AtItsPosition()
    {
        var result = _validator.TestValidate(Command(grateful: ["fine", "  "], learned: ["", "fine"]));

        result.ShouldHaveValidationErrorFor("Grateful[1]");
        result.ShouldNotHaveValidationErrorFor("Grateful[0]");
        result.ShouldHaveValidationErrorFor("Learned[0]");
    }

    [Fact]
    public void AListEntry_MayHave200Characters_NotMore()
    {
        var result = _validator.TestValidate(Command(grateful: [new string('a', 200), new string('a', 201)]));

        result.ShouldNotHaveValidationErrorFor("Grateful[0]");
        result.ShouldHaveValidationErrorFor("Grateful[1]");
    }

    [Fact]
    public void AList_MayHave30Entries_NotMore()
    {
        var thirty = Enumerable.Repeat("a", 30).ToArray();
        var thirtyOne = Enumerable.Repeat("a", 31).ToArray();

        var ok = _validator.TestValidate(Command(grateful: thirty, learned: thirty));
        ok.ShouldNotHaveValidationErrorFor(x => x.Grateful);
        ok.ShouldNotHaveValidationErrorFor(x => x.Learned);

        var tooMany = _validator.TestValidate(Command(grateful: thirtyOne, learned: thirtyOne));
        tooMany.ShouldHaveValidationErrorFor(x => x.Grateful);
        tooMany.ShouldHaveValidationErrorFor(x => x.Learned);
    }

    // ---- tasks ----

    [Fact]
    public void ATaskWithoutATitle_IsRejected_AtItsPosition()
    {
        var result = _validator.TestValidate(Command(tasks: [new DiaryTaskDto("ok", false), new DiaryTaskDto(" ", true)]));

        result.ShouldHaveValidationErrorFor("Tasks[1].Title");
        result.ShouldNotHaveValidationErrorFor("Tasks[0].Title");
    }

    [Fact]
    public void ATaskTitle_MayHave200Characters_NotMore()
    {
        _validator.TestValidate(Command(tasks: [new DiaryTaskDto(new string('a', 200), false)]))
            .ShouldNotHaveValidationErrorFor("Tasks[0].Title");
        _validator.TestValidate(Command(tasks: [new DiaryTaskDto(new string('a', 201), false)]))
            .ShouldHaveValidationErrorFor("Tasks[0].Title");
    }

    [Fact]
    public void ANullTask_IsRejected()
    {
        _validator.TestValidate(Command(tasks: [null!])).ShouldHaveValidationErrorFor("Tasks[0]");
    }

    [Fact]
    public void ADay_MayHave50Tasks_NotMore()
    {
        DiaryTaskDto[] Tasks(int count) => Enumerable.Range(0, count).Select(i => new DiaryTaskDto($"task {i}", false)).ToArray();

        _validator.TestValidate(Command(tasks: Tasks(50))).ShouldNotHaveValidationErrorFor(x => x.Tasks);
        _validator.TestValidate(Command(tasks: Tasks(51))).ShouldHaveValidationErrorFor(x => x.Tasks);
    }
}

public class GetDailyDiaryDaysQueryValidatorTests
{
    private readonly GetDailyDiaryDaysQueryValidator _validator = new();
    private static readonly DateOnly Day = new(2026, 10, 8);

    [Fact]
    public void BothEndsAreOptional()
    {
        _validator.TestValidate(new GetDailyDiaryDaysQuery(null, null)).ShouldNotHaveAnyValidationErrors();
        _validator.TestValidate(new GetDailyDiaryDaysQuery(Day, null)).ShouldNotHaveAnyValidationErrors();
        _validator.TestValidate(new GetDailyDiaryDaysQuery(null, Day)).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ARangeMayBeASingleDay_ButNotBackwards()
    {
        _validator.TestValidate(new GetDailyDiaryDaysQuery(Day, Day)).ShouldNotHaveAnyValidationErrors();
        _validator.TestValidate(new GetDailyDiaryDaysQuery(Day, Day.AddDays(-1))).ShouldHaveValidationErrorFor(x => x.To);
    }
}
