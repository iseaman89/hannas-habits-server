using FluentValidation;
using HannasHabits.Domain.Entities;

namespace HannasHabits.Application.Resolutions.Queries.GetResolutionsByYear;

public class GetResolutionsByYearQueryValidator : AbstractValidator<GetResolutionsByYearQuery>
{
    public GetResolutionsByYearQueryValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(Resolution.MinYear, Resolution.MaxYear);
    }
}
