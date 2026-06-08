using System.Diagnostics.CodeAnalysis;
using Cycling.Rider.Tracking.Api.Extensions;
using Cycling.Rider.Tracking.Api.Filters;
using Cycling.Rider.Tracking.Api.Infrastructure;
using Cycling.Rider.Tracking.Application.Abstractions.Messaging;
using Cycling.Rider.Tracking.Application.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cycling.Rider.Tracking.Api.Controllers;

[ApiController]
[Authorize]
[Route("files")]
[SuppressMessage("SonarLint", "S6960", Justification = "Read and write operations on the same resource belong in one controller.")]
public class FilesController(
    ICommandHandler<SaveFileCommand, SaveFileResult> saveHandler,
    IQueryHandler<ListFilesQuery, ListFilesResult> listHandler) : ControllerBase
{
    public sealed record SaveFitFileRequest(string FileName, byte[] FileContent, DateTimeOffset rideDate);

    [HttpPost("fit", Name = nameof(SaveFitFile))]
    [Idempotent]
    [ProducesResponseType<SaveFileResult>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IResult> SaveFitFile(
        IFormFile fit,
        DateTimeOffset rideDate,
        CancellationToken cancellationToken)
    {
        var result = await saveHandler.HandleAsync(new SaveFileCommand(fit.OpenReadStream(), fit.ContentType, rideDate), cancellationToken);

        return result.Match(value => Results.CreatedAtRoute(nameof(GetFile), new { result.Value.Id }, result.Value), CustomResults.Problem);
    }

    [HttpGet(Name = nameof(ListFiles))]
    [ProducesResponseType<ListFilesResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IResult> ListFiles(
        [FromQuery] string? cursor,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTimeOffset? createdAtFrom = null,
        [FromQuery] DateTimeOffset? createdAtTo = null,
        CancellationToken cancellationToken = default)
    {
        var result = await listHandler.HandleAsync(
            new ListFilesQuery(cursor, pageSize, createdAtFrom, createdAtTo),
            cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [HttpGet("{id:guid}", Name = nameof(GetFile))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<IResult> GetFile(Guid id)
    {
        return Task.FromResult(Results.Ok());
    }
}
