namespace HannasHabits.Domain.Enums;

/// <summary>What a habit's rule is judged in: the single days it is due on, or - for a quota - whole weeks or months.</summary>
public enum CountingUnit
{
    /// <summary>A rule with due days: every due day is met or missed.</summary>
    Days = 0,

    /// <summary>A "N times a week" quota: every calendar week reaches its N or does not.</summary>
    Weeks = 1,

    /// <summary>A "N times a month" quota: every calendar month reaches its N or does not.</summary>
    Months = 2
}
