using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AwesomeAssertions;
using Cycling.Rider.Tracking.Api.Controllers;
using Cycling.Rider.Tracking.Application.Files;
using Cycling.Rider.Tracking.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cycling.Rider.Tracking.Tests.Integration;

[Collection(nameof(IntegrationTestCollection))]
public sealed class IdempotencyEndpointTests(IntegrationTestWebAppFactory factory)
{
    private static readonly DateTimeOffset RideDate =
        new(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SaveFitFile_WithoutIdempotencyKey_ExecutesNormally()
    {
        // Arrange
        using var client = factory.CreateClient();
        await AuthenticateAsync(client);

        // Act
        var response = await PostFitFileAsync(client, RideDate, idempotencyKey: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<SaveFileResult>();
        result!.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task SaveFitFile_WithSameKeyAndSameBody_ReturnsSameIdOnRetry()
    {
        // Arrange
        using var client = factory.CreateClient();
        await AuthenticateAsync(client);
        var key = Guid.NewGuid().ToString();

        // Act
        var first = await PostFitFileAsync(client, RideDate, key);
        var second = await PostFitFileAsync(client, RideDate, key);

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);

        var firstResult = await first.Content.ReadFromJsonAsync<SaveFileResult>();
        var secondResult = await second.Content.ReadFromJsonAsync<SaveFileResult>();

        firstResult!.Id.Should().Be(secondResult!.Id);
    }

    [Fact]
    public async Task SaveFitFile_WithSameKeyAndDifferentBody_Returns422()
    {
        // Arrange
        using var client = factory.CreateClient();
        await AuthenticateAsync(client);
        var key = Guid.NewGuid().ToString();

        var differentRideDate = RideDate.AddDays(1);

        // Act
        var first = await PostFitFileAsync(client, RideDate, key);
        var second = await PostFitFileAsync(client, differentRideDate, key);

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task SaveFitFile_AfterKeyExpired_CreatesNewRide()
    {
        // Arrange
        using var client = factory.CreateClient();
        await AuthenticateAsync(client);
        var key = Guid.NewGuid().ToString();

        var first = await PostFitFileAsync(client, RideDate, key);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstResult = await first.Content.ReadFromJsonAsync<SaveFileResult>();

        // Expire the key directly in the database
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        var record = await db.IdempotencyKeys.FirstAsync(k => k.Key == key);
        record.ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1);
        await db.SaveChangesAsync();

        // Act
        var second = await PostFitFileAsync(client, RideDate, key);

        // Assert
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        var secondResult = await second.Content.ReadFromJsonAsync<SaveFileResult>();
        secondResult!.Id.Should().NotBe(firstResult!.Id);
    }

    [Fact]
    public async Task SaveFitFile_WithoutToken_Returns401_RegardlessOfIdempotencyKey()
    {
        // Arrange
        using var client = factory.CreateClient();
        var key = Guid.NewGuid().ToString();

        // Act
        var response = await PostFitFileAsync(client, RideDate, key);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task AuthenticateAsync(HttpClient client)
    {
        var request = new AuthController.TokenRequest(
            IntegrationTestWebAppFactory.TestUsername,
            IntegrationTestWebAppFactory.TestPassword);

        var response = await client.PostAsJsonAsync("/auth/token", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var token = await response.Content.ReadFromJsonAsync<AuthController.TokenResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token!.AccessToken);
    }

    private static async Task<HttpResponseMessage> PostFitFileAsync(
        HttpClient client,
        DateTimeOffset rideDate,
        string? idempotencyKey)
    {
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent([0x01, 0x02, 0x03]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "fit", "ride.fit");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/files/fit?rideDate={Uri.EscapeDataString(rideDate.ToString("O"))}")
        {
            Content = content
        };

        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return await client.SendAsync(request);
    }
}
