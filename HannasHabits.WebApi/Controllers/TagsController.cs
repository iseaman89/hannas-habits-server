using HannasHabits.Application.Tags;
using HannasHabits.Application.Tags.Commands.CreateTag;
using HannasHabits.Application.Tags.Commands.DeleteTag;
using HannasHabits.Application.Tags.Commands.RenameTag;
using HannasHabits.Application.Tags.Queries.GetTags;
using HannasHabits.WebApi.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HannasHabits.WebApi.Controllers;

/// <summary>The user's tags. A tag belongs to the user, not to a habit; which days carry it is a habit's matter.</summary>
[Authorize]
[ApiController]
[Route("api/tags")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class TagsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TagsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// The user's tags with how much they are used. With <c>habitId</c> the days in that habit come first (for
    /// suggestions while typing), then the days overall, then the name; <c>habitUseCount</c> is 0 without it.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<TagDto>>> GetAll([FromQuery] Guid? habitId, CancellationToken cancellationToken)
    {
        var tags = await _mediator.Send(new GetTagsQuery(habitId), cancellationToken);
        return Ok(tags);
    }

    /// <summary>
    /// Creates a tag. Idempotent by name (ignoring case): if the user already has a tag of that name the answer is
    /// that tag with 200 instead of 201, so a client that types a name it has not seen yet never fails on it.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TagSummaryDto>> Create(CreateTagRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(request.ToCommand(), cancellationToken);

        // There is no single-tag route; the list is where the new tag shows up.
        return result.Created
            ? CreatedAtAction(nameof(GetAll), result.Tag)
            : Ok(result.Tag);
    }

    /// <summary>
    /// Renames a tag. A name another tag of the user already has (ignoring case) merges the two: the days of this tag
    /// go to the other one and this tag is gone - the answer says so (<c>merged</c>) and carries the surviving tag.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RenamedTagDto>> Rename(Guid id, RenameTagRequest request,
        CancellationToken cancellationToken)
    {
        var renamed = await _mediator.Send(request.ToCommand(id), cancellationToken);
        return Ok(renamed);
    }

    /// <summary>Deletes a tag and takes it off every day. The answer says on how many days it was.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DeletedTagDto>> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new DeleteTagCommand(id), cancellationToken);
        return Ok(deleted);
    }
}
