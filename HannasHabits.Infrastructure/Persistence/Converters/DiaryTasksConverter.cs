using System.Text.Json;
using HannasHabits.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HannasHabits.Infrastructure.Persistence.Converters;

/// <summary>
/// Stores the task list of a day as one JSON array (<c>jsonb</c>), e.g. <c>[{"title":"Call mum","done":true}]</c>.
/// The array keeps the user's order. Reading goes through <see cref="DiaryTask.Create"/>, so only valid tasks come out.
/// </summary>
public class DiaryTasksConverter : ValueConverter<IReadOnlyList<DiaryTask>, string>
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public DiaryTasksConverter() : base(tasks => Serialize(tasks), json => Deserialize(json))
    {
    }

    public static string Serialize(IReadOnlyList<DiaryTask> tasks)
        => JsonSerializer.Serialize(tasks.Select(task => new TaskJson(task.Title, task.Done)), Options);

    public static IReadOnlyList<DiaryTask> Deserialize(string json)
        => (JsonSerializer.Deserialize<List<TaskJson>>(json, Options) ?? [])
            .Select(task => DiaryTask.Create(task.Title, task.Done))
            .ToArray();

    // The stored shape, kept apart from the Domain type so the Domain needs no serialization attributes.
    private sealed record TaskJson(string Title, bool Done);
}
