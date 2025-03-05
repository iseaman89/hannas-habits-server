namespace HannasHabits.Data.Shared.Statics;

public static class DaysOfWeekCounter
{
    public static int CountDaysOfWeekInMonth(DayOfWeek dayOfWeek)
    {
        var startOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month);
        return Enumerable.Range(1, daysInMonth).Count(x => startOfMonth.AddDays(x - 1).DayOfWeek == dayOfWeek);
    }

    public static int CountDaysOfWeekInYear(DayOfWeek dayOfWeek)
    {
        var startOfYear = new DateTime(DateTime.Today.Year, 1, 1);
        var daysInYear = DateTime.IsLeapYear(DateTime.Today.Year) ? 366 : 365;
        return Enumerable.Range(1, daysInYear).Count(x => startOfYear.AddDays(x - 1).DayOfWeek == dayOfWeek);
    }
}