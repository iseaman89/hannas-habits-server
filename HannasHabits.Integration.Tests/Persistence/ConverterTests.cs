using HannasHabits.Domain.Exceptions;
using HannasHabits.Domain.ValueObjects;
using HannasHabits.Infrastructure.Persistence.Converters;

namespace HannasHabits.Integration.Tests.Persistence;

// How the Domain's value objects look in the database - and that a damaged value fails loudly instead of loading as garbage.
public class ConverterTests
{
    private static readonly DayOfWeek[] AllDays = Enum.GetValues<DayOfWeek>();

    // ---- HabitSchedule <-> integer bit mask ----

    [Fact]
    public void Schedule_EveryMaskFrom1To127_RoundTrips()
    {
        for (var mask = HabitScheduleConverter.MinMask; mask <= HabitScheduleConverter.MaxMask; mask++)
        {
            var schedule = HabitScheduleConverter.FromMask(mask);

            Assert.Equal(mask, HabitScheduleConverter.ToMask(schedule));
            Assert.All(AllDays, day => Assert.Equal((mask & (1 << (int)day)) != 0, schedule.IncludesDay(day)));
        }
    }

    [Theory]
    [InlineData(127, new[] { 0, 1, 2, 3, 4, 5, 6 })]       // every day
    [InlineData(62, new[] { 1, 2, 3, 4, 5 })]              // Monday to Friday
    [InlineData(65, new[] { 0, 6 })]                       // Sunday + Saturday
    [InlineData(1, new[] { 0 })]                           // Sunday only: bit 0
    public void Schedule_TheStoredNumberIsOneBitPerDay_SundayIsBitZero(int mask, int[] days)
    {
        var schedule = HabitSchedule.Create(days.Select(d => (DayOfWeek)d));

        Assert.Equal(mask, HabitScheduleConverter.ToMask(schedule));
        Assert.Equal(schedule, HabitScheduleConverter.FromMask(mask));
    }

    [Theory]
    [InlineData(0)]     // no day at all
    [InlineData(128)]   // only a bit beyond Saturday
    [InlineData(129)]   // Sunday plus a bit that would be dropped silently
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void Schedule_ADamagedRow_FailsLoudly(int mask)
    {
        Assert.Throws<DomainException>(() => HabitScheduleConverter.FromMask(mask));
    }

    [Fact]
    public void Schedule_TheConverterAndTheCheckConstraintShareTheirLimits()
    {
        Assert.Equal(1, HabitScheduleConverter.MinMask);
        Assert.Equal(127, HabitScheduleConverter.MaxMask);
        Assert.Equal(HabitScheduleConverter.MaxMask, HabitScheduleConverter.ToMask(HabitSchedule.Daily));
    }

    // ---- tasks <-> JSON ----

    [Fact]
    public void Tasks_AreStoredAsAJsonArray_InTheUsersOrder()
    {
        var json = DiaryTasksConverter.Serialize([DiaryTask.Create("Call mum", true), DiaryTask.Create("Run", false)]);

        Assert.Equal("""[{"title":"Call mum","done":true},{"title":"Run","done":false}]""", json);
    }

    [Fact]
    public void Tasks_NoTasksIsAnEmptyArray()
    {
        Assert.Equal("[]", DiaryTasksConverter.Serialize([]));
        Assert.Empty(DiaryTasksConverter.Deserialize("[]"));
    }

    [Fact]
    public void Tasks_TrickyTextRoundTrips()
    {
        var titles = new[] { "Grüße 🎉", "say \"hi\"", "back\\slash", "</script>", "line1\nline2", "{\"json\": [1,2]}", "'; DROP TABLE x; --" };
        var tasks = titles.Select((title, i) => DiaryTask.Create(title, i % 2 == 0)).ToArray();

        var back = DiaryTasksConverter.Deserialize(DiaryTasksConverter.Serialize(tasks));

        Assert.Equal(tasks, back);
    }

    [Theory]
    [InlineData("""[{"title":"   ","done":false}]""")]                                  // a blank title never gets in
    [InlineData("""[{"title":"x","done":false},{"title":"","done":true}]""")]
    public void Tasks_ADamagedRowFailsLoudly_ReadingGoesThroughTheDomain(string json)
    {
        Assert.Throws<DomainException>(() => DiaryTasksConverter.Deserialize(json));
    }

    [Fact]
    public void Tasks_ANullJsonValueIsNoTasks()
    {
        Assert.Empty(DiaryTasksConverter.Deserialize("null"));
    }

    // ---- change detection for lists ----

    private readonly ReadOnlyListComparer<string> _comparer = new();

    [Fact]
    public void Lists_SameItemsInTheSameOrder_AreEqual_EvenAsDifferentInstances()
    {
        IReadOnlyList<string> first = ["a", "b"];
        IReadOnlyList<string> second = new List<string> { "a", "b" };

        Assert.True(_comparer.Equals(first, second));
        Assert.Equal(_comparer.GetHashCode(first), _comparer.GetHashCode(second));
    }

    [Fact]
    public void Lists_AChangedOrderOrContent_IsAChange()
    {
        IReadOnlyList<string> original = ["a", "b", "c"];

        Assert.False(_comparer.Equals(original, ["c", "b", "a"]));
        Assert.False(_comparer.Equals(original, ["a", "b"]));
        Assert.False(_comparer.Equals(original, ["a", "b", "d"]));
        Assert.False(_comparer.Equals(original, []));
    }

    [Fact]
    public void Lists_TheSnapshotEfKeepsIsACopy_LaterChangesToTheListDoNotLeakIntoIt()
    {
        var list = new List<string> { "a" };
        var snapshot = _comparer.Snapshot(list)!;

        list.Add("b");

        Assert.Equal(["a"], snapshot);
        Assert.False(_comparer.Equals(snapshot, list));
    }

    [Fact]
    public void Tasks_TheSameComparerCoversTheValueObject()
    {
        var comparer = new ReadOnlyListComparer<DiaryTask>();
        IReadOnlyList<DiaryTask> one = [DiaryTask.Create("a", false)];

        Assert.True(comparer.Equals(one, [DiaryTask.Create("a", false)]));
        Assert.False(comparer.Equals(one, [DiaryTask.Create("a", true)])); // ticking a task off is a change
    }

    // ---- the plain ones ----

    [Fact]
    public void ThePlainConverters_RoundTrip_AndStayValid()
    {
        var title = new HabitTitleConverter();
        var percentage = new PercentageConverter();
        var strings = new StringListConverter();

        Assert.Equal("Read", title.ConvertToProvider(HabitTitle.Create("Read")));
        Assert.Equal(HabitTitle.Create("Read"), title.ConvertFromProvider("Read"));
        Assert.Equal(42, percentage.ConvertToProvider(Percentage.Create(42)));
        Assert.Equal(Percentage.Create(42), percentage.ConvertFromProvider(42));
        Assert.Equal(new[] { "a", "b" }, (string[])strings.ConvertToProvider(new[] { "a", "b" })!);
        Assert.Equal(new[] { "a", "b" }, (IReadOnlyList<string>)strings.ConvertFromProvider(new[] { "a", "b" })!);

        // A damaged row (title blank, percentage out of range) throws when it is read.
        Assert.Throws<DomainException>(() => title.ConvertFromProvider("   "));
        Assert.Throws<DomainException>(() => percentage.ConvertFromProvider(101));
    }
}
