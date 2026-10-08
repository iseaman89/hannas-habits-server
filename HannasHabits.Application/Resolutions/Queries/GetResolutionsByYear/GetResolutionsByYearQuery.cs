using MediatR;

namespace HannasHabits.Application.Resolutions.Queries.GetResolutionsByYear;

/// <summary>The user's resolutions of a year. A year without any is not an error: the list is simply empty.</summary>
public record GetResolutionsByYearQuery(int Year) : IRequest<List<ResolutionDto>>;
