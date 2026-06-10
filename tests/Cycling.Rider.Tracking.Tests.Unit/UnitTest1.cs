using Cycling.Rider.Tracking.Infrastructure.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Cycling.Rider.Tracking.Tests.Unit;

public class PushFileServiceTests
{
    private readonly Mock<ILogger<PushFileService>> loggerMock;

    public PushFileServiceTests()
    {
        loggerMock = new Mock<ILogger<PushFileService>>();
    }

    [Fact]
    public async Task ExecuteAsync_CallsProcessor()
    {
        var processorOutboxMock = new Mock<IOutboxProcessor>();
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        processorOutboxMock
            .Setup(p => p.ProcessOutboxAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback(() => tcs.TrySetResult());

        var services = new ServiceCollection();
        services.AddScoped((_) => processorOutboxMock.Object);
        var provider = services.BuildServiceProvider();

        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        using var pushFileService = new PushFileService(scopeFactory, loggerMock.Object);

        await pushFileService.StartAsync(CancellationToken.None);

        await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));

        await pushFileService.StopAsync(CancellationToken.None);

        processorOutboxMock.Verify(p => p.ProcessOutboxAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsException_AndLogs()
    {
        var processorOutboxMock = new Mock<IOutboxProcessor>();
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        processorOutboxMock
            .Setup(p => p.ProcessOutboxAsync(It.IsAny<CancellationToken>()))
            .Callback(() => tcs.TrySetResult())
            .Throws<Exception>();

        var services = new ServiceCollection();
        services.AddScoped((_) => processorOutboxMock.Object);
        var provider = services.BuildServiceProvider();

        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        using var pushFileService = new PushFileService(scopeFactory, loggerMock.Object);

        await pushFileService.StartAsync(CancellationToken.None);

        await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));

        await pushFileService.StopAsync(CancellationToken.None);

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
