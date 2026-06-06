using Cycling.Rider.Tracking.Application.Abstractions.Data;
using Cycling.Rider.Tracking.Application.Abstractions.Messaging;
using Cycling.Rider.Tracking.Application.Files;
using Microsoft.AspNetCore.Mvc;

namespace Cycling.Rider.Tracking.Api.Controllers;

[ApiController]
[Route("files")]
public class FilesController(ICommandHandler<SaveFileCommand, FileLocation> handler) : ControllerBase
{
    public sealed record SaveFitFileRequest(string FileName, byte[] FileContent, DateTimeOffset rideDate);

    [HttpPost("fit", Name = nameof(SaveFitFile))]
    public async Task<ActionResult> SaveFitFile(IFormFile fit, DateTimeOffset rideDate)
    {
        var result = await handler.HandleAsync(new SaveFileCommand(fit.OpenReadStream(), fit.ContentType), CancellationToken.None);

        return CreatedAtAction(nameof(GetFile), new { Id = result.Value.rideId }, result);
    }

    [HttpGet("{id:guid}", Name = nameof(GetFile))]
    public Task<ActionResult> GetFile(Guid id)
    {
        return Task.FromResult<ActionResult>(Ok());
    }
}
