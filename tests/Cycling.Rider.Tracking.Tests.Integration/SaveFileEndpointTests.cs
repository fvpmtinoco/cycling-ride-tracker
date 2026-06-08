using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AwesomeAssertions;
using Cycling.Rider.Tracking.Application.Files;
using Cycling.Rider.Tracking.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cycling.Rider.Tracking.Tests.Integration;

[Collection(nameof(IntegrationTestCollection))]
public sealed class SaveFileEndpointTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task SaveFitFile_ReturnsCreatedAndPersistsFile()
    {
        // Arrange
        var fileBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var rideDate = new DateTimeOffset(2026, 6, 7, 8, 0, 0, TimeSpan.Zero);

        using var client = factory.CreateClient();
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "fit", "ride.fit");

        // Act
        var rideDateQuery = Uri.EscapeDataString(rideDate.ToString("O"));
        var response = await client.PostAsync($"/files/fit?rideDate={rideDateQuery}", content);

        // Assert
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, body);

        var result = await response.Content.ReadFromJsonAsync<SaveFileResult>();
        result.Should().NotBeNull();
        result!.Id.Should().NotBe(Guid.Empty);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        var ride = await dbContext.Rides.FindAsync(result.Id);
        ride.Should().NotBeNull();

        var savedFile = await dbContext.TransactionFiles
            .SingleOrDefaultAsync(file => file.FileId == result.Id);
        savedFile.Should().NotBeNull();
        savedFile!.FileContent.Should().Equal(fileBytes);
    }
}
