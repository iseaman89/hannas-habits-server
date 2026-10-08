using HannasHabits.Domain.Enums;
using HannasHabits.Domain.Exceptions;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Domain.Tests.ValueObjects;

public class DiaryContentTests
{
    private static DiaryContent Create(
        Mood? mood = null, int? body = null, int? mind = null, string? highlight = null,
        IEnumerable<string>? grateful = null, IEnumerable<string>? learned = null, IEnumerable<DiaryTask>? tasks = null)
        => DiaryContent.Create(
            mood,
            body is { } b ? Percentage.Create(b) : null,
            mind is { } m ? Percentage.Create(m) : null,
            highlight, grateful, learned, tasks);

    // ---- IsEmpty: "is there anything to keep?" ----

    [Fact]
    public void NothingGiven_IsEmpty_WithEmptyListsInsteadOfNull()
    {
        var content = Create();

        Assert.True(content.IsEmpty);
        Assert.Null(content.Mood);
        Assert.Null(content.Body);
        Assert.Null(content.Mind);
        Assert.Null(content.Highlight);
        Assert.Empty(content.Grateful);
        Assert.Empty(content.Learned);
        Assert.Empty(content.Tasks);
    }

    [Fact]
    public void EachPartAloneMakesTheContentNonEmpty()
    {
        Assert.False(Create(mood: Mood.Ok).IsEmpty);
        Assert.False(Create(highlight: "A good day").IsEmpty);
        Assert.False(Create(grateful: ["Coffee"]).IsEmpty);
        Assert.False(Create(learned: ["Some Rust"]).IsEmpty);
        Assert.False(Create(tasks: [DiaryTask.Create("Call mum", false)]).IsEmpty);
    }

    [Fact]
    public void ABodyOrMindOfZeroIsAnEntry()
    {
        // 0 is a value ("drained"), not "nothing written".
        Assert.False(Create(body: 0).IsEmpty);
        Assert.False(Create(mind: 0).IsEmpty);
    }

    // ---- mood ----

    [Theory]
    [InlineData(Mood.Terrible)]
    [InlineData(Mood.Bad)]
    [InlineData(Mood.Ok)]
    [InlineData(Mood.Good)]
    [InlineData(Mood.Excellent)]
    public void EveryMoodIsAccepted(Mood mood)
    {
        Assert.Equal(mood, Create(mood: mood).Mood);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void ANumberThatIsNoMoodIsRejected(int mood)
    {
        Assert.Throws<DomainException>(() => Create(mood: (Mood)mood));
    }

    // ---- highlight ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n\t")]
    public void ABlankHighlightMeansNoHighlight(string? highlight)
    {
        var content = Create(highlight: highlight);

        Assert.Null(content.Highlight);
        Assert.True(content.IsEmpty);
    }

    [Fact]
    public void TheHighlightIsTrimmed()
    {
        Assert.Equal("A good day", Create(highlight: "  A good day \n").Highlight);
    }

    [Fact]
    public void TheHighlightMayBeAsLongAsTheLimit_ButNotLonger()
    {
        var longest = new string('a', DiaryContent.HighlightMaxLength);

        Assert.Equal(longest, Create(highlight: longest).Highlight);
        Assert.Throws<DomainException>(() => Create(highlight: longest + "a"));
    }

    [Fact]
    public void TheHighlightLimitCountsTheTrimmedText()
    {
        var padded = "  " + new string('a', DiaryContent.HighlightMaxLength) + "  ";

        Assert.Equal(DiaryContent.HighlightMaxLength, Create(highlight: padded).Highlight!.Length);
    }

    // ---- grateful / learned ----

    public static TheoryData<string> ListNames => new() { "grateful", "learned" };

    private static DiaryContent WithList(string list, IEnumerable<string>? items)
        => list == "grateful" ? Create(grateful: items) : Create(learned: items);

    private static IReadOnlyList<string> ListOf(string list, DiaryContent content)
        => list == "grateful" ? content.Grateful : content.Learned;

    [Theory]
    [MemberData(nameof(ListNames))]
    public void ListEntriesAreTrimmed_AndKeepTheUsersOrder(string list)
    {
        var content = WithList(list, ["  zebra ", "apple", "Mango\t"]);

        Assert.Equal(["zebra", "apple", "Mango"], ListOf(list, content));
    }

    [Theory]
    [MemberData(nameof(ListNames))]
    public void ListEntriesAreNotDeduplicated(string list)
    {
        Assert.Equal(["tea", "tea"], ListOf(list, WithList(list, ["tea", "tea"])));
    }

    [Theory]
    [MemberData(nameof(ListNames))]
    public void ANullListMeansAnEmptyList(string list)
    {
        Assert.Empty(ListOf(list, WithList(list, null)));
    }

    [Theory]
    [MemberData(nameof(ListNames))]
    public void ABlankEntryIsRejected_NotSilentlyDropped(string list)
    {
        Assert.Throws<DomainException>(() => WithList(list, ["ok", "  "]));
        Assert.Throws<DomainException>(() => WithList(list, [""]));
        Assert.Throws<DomainException>(() => WithList(list, ["ok", null!]));
    }

    [Theory]
    [MemberData(nameof(ListNames))]
    public void AnEntryMayBeAsLongAsTheLimit_ButNotLonger(string list)
    {
        var longest = new string('a', DiaryContent.ItemMaxLength);

        Assert.Equal([longest], ListOf(list, WithList(list, [longest])));
        Assert.Throws<DomainException>(() => WithList(list, [longest + "a"]));
    }

    [Theory]
    [MemberData(nameof(ListNames))]
    public void AListMayHoldTheMaximumNumberOfEntries_ButNotMore(string list)
    {
        var full = Enumerable.Range(1, DiaryContent.MaxItemsPerList).Select(i => $"entry {i}").ToArray();

        Assert.Equal(full, ListOf(list, WithList(list, full)));

        var exception = Assert.Throws<DomainException>(() => WithList(list, full.Append("one too many")));
        Assert.Contains(list, exception.Message); // the message says which list is too long
    }

    [Fact]
    public void TheTwoListsAreIndependent()
    {
        var content = Create(grateful: ["a"], learned: ["b", "c"]);

        Assert.Equal(["a"], content.Grateful);
        Assert.Equal(["b", "c"], content.Learned);
    }

    // ---- tasks ----

    [Fact]
    public void TasksKeepTheUsersOrder()
    {
        var tasks = new[] { DiaryTask.Create("b", true), DiaryTask.Create("a", false), DiaryTask.Create("c", false) };

        Assert.Equal(tasks, Create(tasks: tasks).Tasks);
    }

    [Fact]
    public void ADayMayHoldTheMaximumNumberOfTasks_ButNotMore()
    {
        var full = Enumerable.Range(1, DiaryContent.MaxTasks).Select(i => DiaryTask.Create($"task {i}", false)).ToArray();

        Assert.Equal(DiaryContent.MaxTasks, Create(tasks: full).Tasks.Count);
        Assert.Throws<DomainException>(() => Create(tasks: full.Append(DiaryTask.Create("one too many", false))));
    }

    // ---- the content is a snapshot ----

    [Fact]
    public void ChangingTheSourceListAfterwardsDoesNotChangeTheContent()
    {
        var grateful = new List<string> { "coffee" };
        var content = Create(grateful: grateful);

        grateful.Add("sun");
        grateful[0] = "changed";

        Assert.Equal(["coffee"], content.Grateful);
    }
}
