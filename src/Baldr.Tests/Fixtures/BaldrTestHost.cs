using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Baldr.Tests.Fixtures;

internal class BaldrTestHost : IAsyncDisposable
{

    private readonly IHost? _host;

    private BaldrTestHost(IHost? host)
    {
        _host = host;
    }


    /// <summary>
    /// Creates and initializes a Baldr test harness with in-memory transport.
    /// </summary>
    /// <param name="configureServices">Optional service configuration callback.</param>
    /// <param name="configureBaldr">Optional Baldr builder configuration callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<BaldrTestHost> CreateInMemoryAsync(
        Action<IServiceCollection>? configureServices = null,
        Action<IBaldrRegistrationBuilder<IRealm>>? configureBaldr = null,
        CancellationToken cancellationToken = default)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddDebug());

        configureServices?.Invoke(services);

        services.AddBaldr(bus =>
        {
            //BaldrTestLicense.Apply(bus);
            bus.UseInMemory();
            configureBaldr?.Invoke(bus);
        });

        var serviceProvider = services.BuildServiceProvider();

        // Build and start a proper host to trigger hosted services
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, svc) =>
            {
                // Copy all services from our manually created provider
                foreach (var descriptor in services)
                {
                    svc.Add(descriptor);
                }
            })
            .Build();

        await host.StartAsync(cancellationToken);

        // Wait for in-memory startup
        var startupState = host.Services.GetRequiredService<IBaldrStartupState<IRealm>>();
        await startupState.WaitUntilReadyAsync(cancellationToken);

        return new BaldrTestHost(host);
    }


    /// <summary>
    /// Gets the service provider from the initialized host.
    /// </summary>
    public IServiceProvider Services => _host?.Services ?? throw new InvalidOperationException("Host not initialized");
    /// <summary>
    /// Gets the message bus for the realm.
    /// </summary>
    public IMessageBus<IRealm> GetMessageBus()
        => Services.GetRequiredService<IMessageBus<IRealm>>();

    /// <summary>
    /// Gets the realm control for lifecycle management.
    /// </summary>
    public IRealmControl<IRealm> GetRealmControl()
        => Services.GetRequiredService<IRealmControl<IRealm>>();

    /// <summary>
    /// Gets the startup state for readiness checking.
    /// </summary>
    public IBaldrStartupState<IRealm> GetStartupState()
        => Services.GetRequiredService<IBaldrStartupState<IRealm>>();

    /// <summary>
    /// Waits for all hosted services to complete startup (all startup signals received).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task WaitForStartupCompleteAsync(CancellationToken cancellationToken = default)
    {
        var startupState = GetStartupState();
        await startupState.WaitUntilReadyAsync(cancellationToken);
    }

    /// <summary>
    /// Cleans up all resources and drops the database (PostgreSQL only).
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        //if (_isPostgres && _databaseName != null)
        //{
        //    try
        //    {
        //        await DropDatabaseAsync(_adminConnectionString, _databaseName);
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine($"Failed to drop test database {_databaseName}: {ex.Message}");
        //    }
        //}
    }
}
