// Services/Implementations/ConnectionManager.cs
using System.Collections.Concurrent;
using ChessAPI.Models.Enums;
using ChessAPI.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ChessAPI.Services.Implementations;

public class ConnectionManager : IConnectionManager
{
    private static readonly ConcurrentDictionary<Guid, List<string>> _userConnections = new();
    private static readonly ConcurrentDictionary<Guid, Guid> _userGames = new();
    private static readonly ConcurrentDictionary<Guid, List<Guid>> _gameUsers = new();
    private static readonly ConcurrentDictionary<Guid, UserStatus> _userStatuses = new();
    private static readonly ConcurrentDictionary<Guid, UserConnectionInfo> _userInfo = new();
    private static readonly ConcurrentDictionary<string, List<string>> _groups = new();
    
    private readonly IMemoryCache _cache;
    private readonly ILogger<ConnectionManager> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public ConnectionManager(
        IMemoryCache cache,
        ILogger<ConnectionManager> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _cache = cache;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public void AddConnection(Guid userId, string connectionId)
    {
        _userConnections.AddOrUpdate(
            userId,
            new List<string> { connectionId },
            (_, connections) =>
            {
                lock (connections)
                {
                    if (!connections.Contains(connectionId))
                    {
                        connections.Add(connectionId);
                    }
                    return connections;
                }
            });

        _userInfo.AddOrUpdate(
            userId,
            new UserConnectionInfo
            {
                UserId = userId,
                ConnectionIds = new List<string> { connectionId },
                ConnectedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow
            },
            (_, info) =>
            {
                lock (info.ConnectionIds)
                {
                    if (!info.ConnectionIds.Contains(connectionId))
                    {
                        info.ConnectionIds.Add(connectionId);
                    }
                    info.LastActivity = DateTime.UtcNow;
                    return info;
                }
            });

        _cache.Set($"connection_{userId}", connectionId, TimeSpan.FromHours(24));
        
        _logger.LogDebug("Added connection {ConnectionId} for user {UserId}", connectionId, userId);
    }

    public void RemoveConnection(Guid userId, string connectionId)
    {
        if (_userConnections.TryGetValue(userId, out var connections))
        {
            lock (connections)
            {
                connections.Remove(connectionId);
                if (connections.Count == 0)
                {
                    _userConnections.TryRemove(userId, out _);
                    _userStatuses.TryRemove(userId, out _);
                    _cache.Remove($"connection_{userId}");
                }
            }
        }

        if (_userInfo.TryGetValue(userId, out var info))
        {
            lock (info.ConnectionIds)
            {
                info.ConnectionIds.Remove(connectionId);
                if (info.ConnectionIds.Count == 0)
                {
                    _userInfo.TryRemove(userId, out _);
                }
            }
        }

        _logger.LogDebug("Removed connection {ConnectionId} for user {UserId}", connectionId, userId);
    }

    public string? GetConnectionId(Guid userId)
    {
        if (_userConnections.TryGetValue(userId, out var connections))
        {
            lock (connections)
            {
                return connections.FirstOrDefault();
            }
        }
        return null;
    }

    public List<string> GetConnectionIds(Guid userId)
    {
        if (_userConnections.TryGetValue(userId, out var connections))
        {
            lock (connections)
            {
                return connections.ToList();
            }
        }
        return new List<string>();
    }

    public Dictionary<Guid, List<string>> GetAllConnections()
    {
        var result = new Dictionary<Guid, List<string>>();
        foreach (var kvp in _userConnections)
        {
            lock (kvp.Value)
            {
                result[kvp.Key] = kvp.Value.ToList();
            }
        }
        return result;
    }

    public bool IsUserConnected(Guid userId)
    {
        return _userConnections.ContainsKey(userId) && 
               _userConnections[userId].Count > 0;
    }

    public int GetConnectedUsersCount()
    {
        return _userConnections.Count;
    }

    public void AddUserToGame(Guid userId, Guid gameId)
    {
        _userGames[userId] = gameId;
        
        _gameUsers.AddOrUpdate(
            gameId,
            new List<Guid> { userId },
            (_, users) =>
            {
                lock (users)
                {
                    if (!users.Contains(userId))
                    {
                        users.Add(userId);
                    }
                    return users;
                }
            });

        if (_userInfo.TryGetValue(userId, out var info))
        {
            info.CurrentGameId = gameId;
        }

        UpdateUserStatus(userId, UserStatus.InGame);
        
        _logger.LogDebug("User {UserId} added to game {GameId}", userId, gameId);
    }

    public void RemoveUserFromGame(Guid userId)
    {
        if (_userGames.TryRemove(userId, out var gameId))
        {
            if (_gameUsers.TryGetValue(gameId, out var users))
            {
                lock (users)
                {
                    users.Remove(userId);
                    if (users.Count == 0)
                    {
                        _gameUsers.TryRemove(gameId, out _);
                    }
                }
            }
        }

        if (_userInfo.TryGetValue(userId, out var info))
        {
            info.CurrentGameId = null;
        }

        UpdateUserStatus(userId, UserStatus.Online);
        
        _logger.LogDebug("User {UserId} removed from game", userId);
    }

    public Guid? GetUserGame(Guid userId)
    {
        return _userGames.TryGetValue(userId, out var gameId) ? gameId : null;
    }

    public List<Guid> GetUsersInGame(Guid gameId)
    {
        if (_gameUsers.TryGetValue(gameId, out var users))
        {
            lock (users)
            {
                return users.ToList();
            }
        }
        return new List<Guid>();
    }

    public void UpdateUserStatus(Guid userId, UserStatus status)
    {
        _userStatuses[userId] = status;
        
        if (_userInfo.TryGetValue(userId, out var info))
        {
            info.Status = status;
            info.LastActivity = DateTime.UtcNow;
        }

        _cache.Set($"user_status_{userId}", status, TimeSpan.FromHours(24));
        
        Task.Run(async () => await UpdateUserStatusInDatabase(userId, status));
        
        _logger.LogDebug("Updated status for user {UserId} to {Status}", userId, status);
    }

    public UserStatus GetUserStatus(Guid userId)
    {
        if (_userStatuses.TryGetValue(userId, out var status))
        {
            return status;
        }
        
        if (_cache.TryGetValue($"user_status_{userId}", out UserStatus cachedStatus))
        {
            return cachedStatus;
        }
        
        return UserStatus.Offline;
    }

    public void AddToGroup(string connectionId, string groupName)
    {
        _groups.AddOrUpdate(
            groupName,
            new List<string> { connectionId },
            (_, connections) =>
            {
                lock (connections)
                {
                    if (!connections.Contains(connectionId))
                    {
                        connections.Add(connectionId);
                    }
                    return connections;
                }
            });

        _logger.LogDebug("Added connection {ConnectionId} to group {GroupName}", connectionId, groupName);
    }

    public void RemoveFromGroup(string connectionId, string groupName)
    {
        if (_groups.TryGetValue(groupName, out var connections))
        {
            lock (connections)
            {
                connections.Remove(connectionId);
                if (connections.Count == 0)
                {
                    _groups.TryRemove(groupName, out _);
                }
            }
        }

        _logger.LogDebug("Removed connection {ConnectionId} from group {GroupName}", connectionId, groupName);
    }

    public List<string> GetGroupMembers(string groupName)
    {
        if (_groups.TryGetValue(groupName, out var connections))
        {
            lock (connections)
            {
                return connections.ToList();
            }
        }
        return new List<string>();
    }

    public Dictionary<Guid, UserConnectionInfo> GetConnectedUsersInfo()
    {
        var result = new Dictionary<Guid, UserConnectionInfo>();
        
        foreach (var userId in _userConnections.Keys)
        {
            if (_userInfo.TryGetValue(userId, out var info))
            {
                result[userId] = new UserConnectionInfo
                {
                    UserId = info.UserId,
                    Username = info.Username,
                    ConnectionIds = info.ConnectionIds.ToList(),
                    Status = GetUserStatus(userId),
                    CurrentGameId = info.CurrentGameId,
                    ConnectedAt = info.ConnectedAt,
                    LastActivity = info.LastActivity
                };
            }
        }
        
        return result;
    }

    private async Task UpdateUserStatusInDatabase(Guid userId, UserStatus status)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();
            
            var user = await context.Users.FindAsync(userId);
            if (user != null)
            {
                user.Status = status;
                user.LastActiveAt = DateTime.UtcNow;
                await context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update user status in database for user {UserId}", userId);
        }
    }

    public void UpdateUsername(Guid userId, string username)
    {
        if (_userInfo.TryGetValue(userId, out var info))
        {
            info.Username = username;
        }
        else
        {
            _userInfo[userId] = new UserConnectionInfo
            {
                UserId = userId,
                Username = username,
                ConnectionIds = new List<string>(),
                Status = UserStatus.Offline,
                ConnectedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow
            };
        }
    }

    public void UpdateActivity(Guid userId)
    {
        if (_userInfo.TryGetValue(userId, out var info))
        {
            info.LastActivity = DateTime.UtcNow;
        }
    }

    public List<Guid> GetInactiveUsers(TimeSpan timeout)
    {
        var cutoff = DateTime.UtcNow - timeout;
        return _userInfo
            .Where(kvp => kvp.Value.LastActivity < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    public void CleanupInactiveConnections(TimeSpan timeout)
    {
        var inactiveUsers = GetInactiveUsers(timeout);
        
        foreach (var userId in inactiveUsers)
        {
            if (_userConnections.TryGetValue(userId, out var connections))
            {
                lock (connections)
                {
                    foreach (var connectionId in connections.ToList())
                    {
                        RemoveConnection(userId, connectionId);
                    }
                }
            }
            
            UpdateUserStatus(userId, UserStatus.Offline);
        }
        
        _logger.LogInformation("Cleaned up {Count} inactive connections", inactiveUsers.Count);
    }

    public Dictionary<Guid, int> GetGamesWithPlayerCount()
    {
        var result = new Dictionary<Guid, int>();
        
        foreach (var kvp in _gameUsers)
        {
            lock (kvp.Value)
            {
                result[kvp.Key] = kvp.Value.Count;
            }
        }
        
        return result;
    }

    public bool IsUserInGame(Guid userId, Guid gameId)
    {
        return _userGames.TryGetValue(userId, out var currentGameId) && 
               currentGameId == gameId;
    }

    public List<Guid> GetOnlineUsers()
    {
        return _userConnections.Keys
            .Where(IsUserConnected)
            .ToList();
    }

    public Dictionary<UserStatus, int> GetUserStatusStatistics()
    {
        var stats = new Dictionary<UserStatus, int>();
        
        foreach (UserStatus status in Enum.GetValues(typeof(UserStatus)))
        {
            stats[status] = 0;
        }
        
        foreach (var userId in _userConnections.Keys)
        {
            var status = GetUserStatus(userId);
            stats[status]++;
        }
        
        return stats;
    }
    
}