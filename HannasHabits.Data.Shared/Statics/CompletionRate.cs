namespace HannasHabits.Data.Shared.Statics;

public static class CompletionRate
{
    public static int CalculateCompletionRate(int completed, int scheduled) =>
        scheduled == 0 ? 0 : Convert.ToInt32((double)completed / scheduled * 100);
}