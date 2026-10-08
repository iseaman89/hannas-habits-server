using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace HannasHabits.Infrastructure.Persistence.Converters;

/// <summary>
/// Tells EF Core when a list property has changed: same items in the same order = unchanged. Without it EF would
/// compare the list instances (a new list with equal items would count as a change, and an edited one could be missed).
/// </summary>
public class ReadOnlyListComparer<T> : ValueComparer<IReadOnlyList<T>>
{
    public ReadOnlyListComparer() : base(
        (left, right) => ReferenceEquals(left, right) || (left != null && right != null && left.SequenceEqual(right)),
        items => items.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
        items => items.ToArray())
    {
    }
}
