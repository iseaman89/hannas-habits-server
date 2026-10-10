using HannasHabits.Application.Tags.Commands.RenameTag;

namespace HannasHabits.WebApi.Models;

/// <param name="Name">At most 30 characters after trimming. A name another tag already has merges the two.</param>
public record RenameTagRequest(string Name)
{
    public RenameTagCommand ToCommand(Guid id) => new(id, Name);
}
