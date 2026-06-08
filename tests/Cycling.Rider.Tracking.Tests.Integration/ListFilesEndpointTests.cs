using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AwesomeAssertions;
using Cycling.Rider.Tracking.Api.Controllers;
using Cycling.Rider.Tracking.Application.Files;

namespace Cycling.Rider.Tracking.Tests.Integration;

[Collection(nameof(IntegrationTestCollection))]
public sealed class ListFilesEndpointTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task ListFiles_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/files");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListFiles_ReturnsEmptyResult_WhenNoFilesExist()
    {
        // Arrange
        using var client = factory.CreateClient();
        await AuthenticateAsync(client);

        // Act
        var response = await client.GetAsync("/files?pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ListFilesResult>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeNull();
        result.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task ListFiles_ReturnsBadRequest_WhenPageSizeOutOfRange()
    {
        // Arrange
        using var client = factory.CreateClient();
        await AuthenticateAsync(client);

        // Act
        var response = await client.GetAsync("/files?pageSize=200");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListFiles_ReturnsBadRequest_WhenCreatedAtRangeInvalid()
    {
        // Arrange
        using var client = factory.CreateClient();
        await AuthenticateAsync(client);

        var from = Uri.EscapeDataString("2026-06-08T10:00:00+00:00");
        var to = Uri.EscapeDataString("2026-06-07T10:00:00+00:00");

        // Act
        var response = await client.GetAsync($"/files?createdAtFrom={from}&createdAtTo={to}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListFiles_ReturnsPaginatedResults_WithNextCursor()
    {
        // Arrange
        var rideDate = new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client);

        // Seed 3 files
        for (var i = 0; i < 3; i++)
        {
            await SaveFitFileAsync(client, rideDate.AddDays(i));
        }

        // Act — request only 2 items
        var response = await client.GetAsync("/files?pageSize=2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ListFilesResult>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(2);
        result.NextCursor.Should().NotBeNullOrEmpty();

        // Follow the cursor
        var page2Response = await client.GetAsync($"/files?pageSize=2&cursor={Uri.EscapeDataString(result.NextCursor!)}");
        page2Response.StatusCode.Should().Be(HttpStatusCode.OK);

        var page2 = await page2Response.Content.ReadFromJsonAsync<ListFilesResult>();
        page2.Should().NotBeNull();
        page2!.Items.Should().HaveCountGreaterThan(0);
    }

    private static async Task AuthenticateAsync(HttpClient client)
    {
        var request = new AuthController.TokenRequest(
            IntegrationTestWebAppFactory.TestUsername,
            IntegrationTestWebAppFactory.TestPassword);

        var response = await client.PostAsJsonAsync("/auth/token", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var token = await response.Content.ReadFromJsonAsync<AuthController.TokenResponse>();
        token.Should().NotBeNull();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
    }

    private static async Task SaveFitFileAsync(HttpClient client, DateTimeOffset rideDate)
    {
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent([0x01, 0x02, 0x03]);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "fit", "ride.fit");

        var response = await client.PostAsync(
            $"/files/fit?rideDate={Uri.EscapeDataString(rideDate.ToString("O"))}",
            content);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
