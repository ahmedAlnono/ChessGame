// Services/BackgroundServices/ConnectionCleanupBackgroundService.cs
using ChessAPI.Services.Interfaces;

namespace ChessAPI.Services.BackgroundServices;

public class ConnectionCleanupBackgroundService : BackgroundService
{
    private readonly IConnectionManager _connectionManager;
    private readonly ILogger<ConnectionCleanupBackgroundService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _inactivityTimeout = TimeSpan.FromMinutes(10);

    public ConnectionCleanupBackgroundService(
        IConnectionManager connectionManager,
        ILogger<ConnectionCleanupBackgroundService> logger)
    {
        _connectionManager = connectionManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Connection cleanup service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _connectionManager.CleanupInactiveConnections(_inactivityTimeout);
                
                var connectedUsers = _connectionManager.GetConnectedUsersCount();
                var statusStats = _connectionManager.GetUserStatusStatistics();
                
                _logger.LogInformation(
                    "Connection stats - Connected users: {ConnectedUsers}, Status distribution: {@StatusStats}",
                    connectedUsers,
                    statusStats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during connection cleanup");
            }

            await Task.Delay(_cleanupInterval, stoppingToken);
        }

        _logger.LogInformation("Connection cleanup service stopped");
    }
}