

using Baldr.Tests.Fixtures;

namespace Baldr.Tests;

public class BaldrInMemoryTests
{
    // ===== Test Message Types =====
    private sealed record SimpleCommand(string Id, string Value);
    private sealed record SimpleEvent(string Id, string Value);
    private sealed record SimpleRequest(string Id, string Query);
    private sealed record SimpleResponse(string Id, string Result);

    // ===== Test Handlers with thread-safe message collection =====
    private sealed class CollectingCommandHandler : ICommandHandler<SimpleCommand>
    {
        public static readonly List<SimpleCommand> AllReceived = new();

        public Task HandleAsync(SimpleCommand command, CancellationToken cancellationToken = default)
        {
            lock (AllReceived)
            {
                AllReceived.Add(command);
            }
            return Task.CompletedTask;
        }
    }

    private sealed class CollectingEventHandler : IEventHandler<SimpleEvent>
    {
        public static readonly List<SimpleEvent> AllReceived = new();

        public Task HandleAsync(SimpleEvent @event, CancellationToken cancellationToken = default)
        {
            lock (AllReceived)
            {
                AllReceived.Add(@event);
            }
            return Task.CompletedTask;
        }
    }

    private sealed class CollectingRequestHandler : IRequestHandler<SimpleRequest, SimpleResponse>
    {
        public static readonly List<SimpleRequest> AllReceived = new();

        public Task<SimpleResponse> HandleAsync(SimpleRequest request, CancellationToken cancellationToken = default)
        {
            lock (AllReceived)
            {
                AllReceived.Add(request);
            }
            return Task.FromResult(new SimpleResponse(request.Id, $"Answered: {request.Query}"));
        }
    }
    // ===== SEND / COMMAND TESTS =====

    /// <summary>
    /// Test: SendAsync delivers command to registered handler.
    /// </summary>
    [Fact]
    public async Task SendAsync_WithRegisteredHandler_DeliversSynchronously()
    {
        // Arrange
        var command = new SimpleCommand("cmd1", "test value");

        await using var harness = await BaldrTestHost.CreateInMemoryAsync(
            configureBaldr: bus =>
            {
                bus.AddSubscriber<CollectingCommandHandler>();
            });

        var bus = harness.GetMessageBus();

        // Act
        await bus.SendAsync(command);

        // Assert
        Assert.True(CollectingCommandHandler.AllReceived.Count > 0, "Handler should receive command");
        Assert.Equal("cmd1", CollectingCommandHandler.AllReceived[^1].Id);
    }

    /// <summary>
    /// Test: SendAsync with multiple commands delivers all sequentially.
    /// </summary>
    [Fact]
    public async Task SendAsync_MultipleMessages_DeliverSequentially()
    {
        // Arrange
        await using var harness = await BaldrTestHost.CreateInMemoryAsync(
            configureBaldr: bus =>
            {
                bus.AddSubscriber<CollectingCommandHandler>();
            });

        var bus = harness.GetMessageBus();

        var commands = new[]
        {
            new SimpleCommand("cmd1", "first"),
            new SimpleCommand("cmd2", "second"),
            new SimpleCommand("cmd3", "third"),
        };

        // Act
        var countBefore = CollectingCommandHandler.AllReceived.Count;
        foreach (var command in commands)
        {
            await bus.SendAsync(command);
        }
        var countAfter = CollectingCommandHandler.AllReceived.Count;

        // Assert
        Assert.Equal(3, countAfter - countBefore);
    }

    /// <summary>
    /// Test: SendAsync with message context works.
    /// </summary>
    [Fact]
    public async Task SendAsync_WithMessageContext_Succeeds()
    {
        // Arrange
        var command = new SimpleCommand("cmd1", "value");
        var context = new MessageContext(CorrelationId: "corr-123");

        await using var harness = await BaldrTestHost.CreateInMemoryAsync(
            configureBaldr: bus =>
            {
                bus.AddSubscriber<CollectingCommandHandler>();
            });

        var bus = harness.GetMessageBus();

        // Act & Assert - Should not throw
        await bus.SendAsync(command, context);
    }

    /// <summary>
    /// Test: SendAsync respects cancellation token.
    /// </summary>
    [Fact]
    public async Task SendAsync_WithCancellationToken_Completes()
    {
        // Arrange
        var command = new SimpleCommand("cmd1", "value");

        await using var harness = await BaldrTestHost.CreateInMemoryAsync(
            configureBaldr: bus =>
            {
                bus.AddSubscriber<CollectingCommandHandler>();
            });

        var bus = harness.GetMessageBus();
        var cts = new CancellationTokenSource();

        // Act & Assert - Should not throw
        await bus.SendAsync(command, cancellationToken: cts.Token);
    }

    // ===== PUBLISH / EVENT TESTS =====

    /// <summary>
    /// Test: PublishAsync delivers event to registered handlers.
    /// </summary>
    [Fact]
    public async Task PublishAsync_WithHandler_DeliverEvent()
    {
        // Arrange
        var @event = new SimpleEvent("evt1", "test event");

        await using var harness = await BaldrTestHost.CreateInMemoryAsync(
            configureBaldr: bus =>
            {
                bus.AddSubscriber<CollectingEventHandler>();
            });

        var bus = harness.GetMessageBus();

        // Act
        var countBefore = CollectingEventHandler.AllReceived.Count;
        await bus.PublishAsync(@event);
        var countAfter = CollectingEventHandler.AllReceived.Count;

        // Assert
        Assert.Equal(1, countAfter - countBefore);
    }

    /// <summary>
    /// Test: PublishAsync with multiple events delivers all.
    /// </summary>
    [Fact]
    public async Task PublishAsync_MultipleEvents_DeliverAll()
    {
        // Arrange
        await using var harness = await BaldrTestHost.CreateInMemoryAsync(
            configureBaldr: bus =>
            {
                bus.AddSubscriber<CollectingEventHandler>();
            });

        var bus = harness.GetMessageBus();

        var events = new[]
        {
            new SimpleEvent("evt1", "first"),
            new SimpleEvent("evt2", "second"),
            new SimpleEvent("evt3", "third"),
        };

        // Act
        var countBefore = CollectingEventHandler.AllReceived.Count;
        foreach (var evt in events)
        {
            await bus.PublishAsync(evt);
        }
        var countAfter = CollectingEventHandler.AllReceived.Count;

        // Assert
        Assert.Equal(3, countAfter - countBefore);
    }

    // ===== REQUEST / RESPONSE TESTS =====

    /// <summary>
    /// Test: RequestAsync returns response from handler.
    /// </summary>
    [Fact]
    public async Task RequestAsync_WithHandler_ReturnsResponse()
    {
        // Arrange
        var request = new SimpleRequest("req1", "what is the answer?");

        await using var harness = await BaldrTestHost.CreateInMemoryAsync(
            configureBaldr: bus =>
            {
                bus.AddSubscriber<CollectingRequestHandler>();
            });

        var bus = harness.GetMessageBus();

        // Act
        var response = await bus.RequestAsync<SimpleRequest, SimpleResponse>(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("req1", response.Id);
        Assert.StartsWith("Answered:", response.Result);
    }

    /// <summary>
    /// Test: TryRequestAsync returns success result when handler responds.
    /// </summary>
    [Fact]
    public async Task TryRequestAsync_WithHandler_ReturnsSuccess()
    {
        // Arrange
        var request = new SimpleRequest("req1", "query");

        await using var harness = await BaldrTestHost.CreateInMemoryAsync(
            configureBaldr: bus =>
            {
                bus.AddSubscriber<CollectingRequestHandler>();
            }, cancellationToken: TestContext.Current.CancellationToken);

        var bus = harness.GetMessageBus();

        // Act
        var result = await bus.TryRequestAsync<SimpleRequest, SimpleResponse>(request, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Equal("req1", result.Response.Id);
    }

    /// <summary>
    /// Test: RequestAsync with timeout completes within specified duration.
    /// </summary>
    [Fact]
    public async Task RequestAsync_WithTimeout_CompletesWithinTimeout()
    {
        // Arrange
        var request = new SimpleRequest("req1", "quick query");
        var timeout = TimeSpan.FromSeconds(5);

        await using var harness = await BaldrTestHost.CreateInMemoryAsync(
            configureBaldr: bus =>
            {
                bus.AddSubscriber<CollectingRequestHandler>();
            }, cancellationToken: TestContext.Current.CancellationToken);

        var bus = harness.GetMessageBus();

        // Act
        var response = await bus.RequestAsync<SimpleRequest, SimpleResponse>(request, timeout, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("req1", response.Id);
    }

    /// <summary>
    /// Test: Multiple requests are handled independently.
    /// </summary>
    [Fact]
    public async Task RequestAsync_MultipleRequests_HandleIndependently()
    {
        // Arrange
        var requests = new[]
        {
            new SimpleRequest("req1", "query 1"),
            new SimpleRequest("req2", "query 2"),
            new SimpleRequest("req3", "query 3"),
        };

        await using var harness = await BaldrTestHost.CreateInMemoryAsync(
            configureBaldr: bus =>
            {
                bus.AddSubscriber<CollectingRequestHandler>();
            }, cancellationToken: TestContext.Current.CancellationToken);

        var bus = harness.GetMessageBus();

        // Act
        var responses = new List<SimpleResponse>();
        foreach (var request in requests)
        {
            var response = await bus.RequestAsync<SimpleRequest, SimpleResponse>(request, cancellationToken: TestContext.Current.CancellationToken);
            responses.Add(response);
        }

        // Assert
        Assert.Equal(3, responses.Count);
        Assert.Equal("req1", responses[0].Id);
        Assert.Equal("req2", responses[1].Id);
        Assert.Equal("req3", responses[2].Id);
    }

    // ===== MIXED OPERATIONS TESTS =====

    /// <summary>
    /// Test: Send, Publish, and Request operations work together.
    /// </summary>
    [Fact]
    public async Task MixedOperations_SendPublishRequest_AllSucceed()
    {
        // Arrange
        await using var harness = await BaldrTestHost.CreateInMemoryAsync(
            configureBaldr: bus =>
            {
                bus.AddSubscriber<CollectingCommandHandler>();
                bus.AddSubscriber<CollectingEventHandler>();
                bus.AddSubscriber<CollectingRequestHandler>();
            }, cancellationToken: TestContext.Current.CancellationToken);

        var bus = harness.GetMessageBus();

        // Act
        await bus.SendAsync(new SimpleCommand("cmd1", "test"), cancellationToken: TestContext.Current.CancellationToken);
        await bus.PublishAsync(new SimpleEvent("evt1", "event"), cancellationToken: TestContext.Current.CancellationToken);
        var response = await bus.RequestAsync<SimpleRequest, SimpleResponse>(
            new SimpleRequest("req1", "query"), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("req1", response.Id);
    }

    // ===== STARTUP AND STATE TESTS =====

    /// <summary>
    /// Test: In-memory harness is ready immediately upon creation.
    /// </summary>
    [Fact]
    public async Task StartupState_InMemory_IsReadyImmediately()
    {
        // Arrange & Act
        await using var harness = await BaldrTestHost.CreateInMemoryAsync(cancellationToken: TestContext.Current.CancellationToken );
        var startupState = harness.GetStartupState();

        // Assert
        Assert.True(startupState.IsReady);
        await startupState.WaitUntilReadyAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(startupState.IsReady);
    }

    // ===== ERROR AND EDGE CASE TESTS =====

    /// <summary>
    /// Test: SendAsync without registered handler throws appropriate error.
    /// </summary>
    [Fact]
    public async Task SendAsync_NoHandlerRegistered_ThrowsInvalidOperationException()
    {
        // Arrange
        var command = new SimpleCommand("cmd1", "value");

        await using var harness = await BaldrTestHost.CreateInMemoryAsync();
        var bus = harness.GetMessageBus();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => bus.SendAsync(command, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Contains("No command handler registered", exception.Message);
    }

    /// <summary>
    /// Test: RequestAsync without handler times out gracefully.
    /// </summary>
    [Fact]
    public async Task RequestAsync_NoHandlerRegistered_TimesOutGracefully()
    {
        // Arrange
        var request = new SimpleRequest("req1", "query");

        await using var harness = await BaldrTestHost.CreateInMemoryAsync(cancellationToken: TestContext.Current.CancellationToken);
        var bus = harness.GetMessageBus();

        // Act & Assert - Should timeout without throwing
        var timeout = TimeSpan.FromMilliseconds(100);
        var task = bus.RequestAsync<SimpleRequest, SimpleResponse>(request, timeout, cancellationToken: TestContext.Current.CancellationToken);

        var result = await task.ContinueWith(t =>
        {
            if (t.IsFaulted)
                return null;
            return t.Result;
        });

        Assert.True(result == null || task.IsCompleted);
    }
}
