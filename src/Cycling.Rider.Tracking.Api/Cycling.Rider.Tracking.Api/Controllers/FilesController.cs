using Cycling.Rider.Tracking.Api.Extensions;
using Cycling.Rider.Tracking.Api.Infrastructure;
using Cycling.Rider.Tracking.Application.Abstractions.Messaging;
using Cycling.Rider.Tracking.Application.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cycling.Rider.Tracking.Api.Controllers;

[ApiController]
[Authorize]
[Route("files")]
public class FilesController(ICommandHandler<SaveFileCommand, SaveFileResult> handler) : ControllerBase
{
    public sealed record SaveFitFileRequest(string FileName, byte[] FileContent, DateTimeOffset rideDate);

    [HttpPost("fit", Name = nameof(SaveFitFile))]
    public async Task<IResult> SaveFitFile(
        IFormFile fit,
        DateTimeOffset rideDate,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new SaveFileCommand(fit.OpenReadStream(), fit.ContentType), cancellationToken);

        return result.Match(value => Results.CreatedAtRoute(nameof(GetFile), new { result.Value.Id }, result.Value), CustomResults.Problem);
    }

    [HttpGet("{id:guid}", Name = nameof(GetFile))]
    public Task<IResult> GetFile(Guid id)
    {
        return Task.FromResult(Results.Ok());
    }
}
