using HannasHabits.Application.Tags.Commands.CreateTag;

namespace HannasHabits.WebApi.Models;

/// <param name="Name">At most 30 characters after trimming; compared ignoring case.</param>
public record CreateTagRequest(string Name)
{
    public CreateTagCommand ToCommand() => new(Name);
}
