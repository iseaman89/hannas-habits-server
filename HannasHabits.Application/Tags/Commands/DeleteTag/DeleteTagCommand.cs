using MediatR;

namespace HannasHabits.Application.Tags.Commands.DeleteTag;

/// <summary>Deletes a tag and takes it off every day it was on.</summary>
public record DeleteTagCommand(Guid TagId) : IRequest<DeletedTagDto>;

/// <param name="RemovedFromDays">On how many days (of all habits together) the tag was and no longer is.</param>
public record DeletedTagDto(int RemovedFromDays);
