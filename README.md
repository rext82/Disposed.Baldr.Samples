<p align="center">
  <img src="images/disposed-logo.svg" alt="Disposed Software" width="48%">
  <img src="images/baldr-logo.svg" alt="Baldr Message Bus" width="48%">
</p>

<h1 align="center">Disposed.Baldr.Samples</h1>

<p align="center">
  Practical samples and tests for <strong>Baldr</strong>, a message bus for .NET.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-11-7C4DFF?style=flat-square" alt=".NET 11">
  <img src="https://img.shields.io/badge/transport-in--memory-00BFD6?style=flat-square" alt="In-memory transport">
</p>

## What this repository shows

This repository contains an in-memory test harness and examples of the core Baldr message bus workflows:

- **Commands** — send a message to its registered handler.
- **Events** — publish a message to registered handlers.
- **Request/response** — make a request and receive a typed response.
- **Message context** — pass metadata such as a correlation ID.
- **Startup and cancellation** — wait for readiness and pass cancellation tokens.
- **Edge cases** — verify behavior when handlers are missing and requests time out.

The samples are implemented as tests, so each example can be run and checked with the .NET test runner.

## Get started

You'll need the .NET 11 SDK. From the repository root, run:

```shell
dotnet test src/Baldr.Tests/Baldr.Tests.csproj
```

The tests use an in-memory transport, so no external broker or database is needed.

## Example

Register a handler with the Baldr builder, then send a message through the bus:

```csharp
await using var harness = await BaldrTestHost.CreateInMemoryAsync(
    configureBaldr: bus => bus.AddSubscriber<MyCommandHandler>());

var messageBus = harness.GetMessageBus();
await messageBus.SendAsync(new MyCommand("example"));
```

The same harness can exercise event publishing and request/response:

```csharp
await messageBus.PublishAsync(new OrderCreated("order-123"));

var response = await messageBus.RequestAsync<PriceRequest, PriceResponse>(
    new PriceRequest("product-456"));
```

See [`BaldrInMemoryTests.cs`](src/Baldr.Tests/BaldrInMemoryTests.cs) for complete examples, including handler definitions and assertions.

## Project layout

```text
images/
  baldr-logo.svg
  disposed-logo.svg
src/
  Baldr.Tests/
    Fixtures/
      BaldrTestHost.cs
    BaldrInMemoryTests.cs
```

