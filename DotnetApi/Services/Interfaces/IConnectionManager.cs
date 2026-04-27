// Services/Interfaces/IConnectionManager.cs
using ChessAPI.Models.Enums;

namespace ChessAPI.Services.Interfaces;

public interface IConnectionManager
{
    void AddConnection(Guid userId, string connectionId);
    void RemoveConnection(Guid userId, string connectionId);
    string? GetConnectionId(Guid userId);
    List<string> GetConnectionIds(Guid userId);
    Dictionary<Guid, List<string>> GetAllConnections();
    bool IsUserConnected(Guid userId);
    int GetConnectedUsersCount();
    void AddUserToGame(Guid userId, Guid gameId);
    void RemoveUserFromGame(Guid userId);
    Guid? GetUserGame(Guid userId);
    List<Guid> GetUsersInGame(Guid gameId);
    void UpdateUserStatus(Guid userId, UserStatus status);
    UserStatus GetUserStatus(Guid userId);
    void AddToGroup(string connectionId, string groupName);
    void RemoveFromGroup(string connectionId, string groupName);
    List<string> GetGroupMembers(string groupName);
    Dictionary<Guid, UserConnectionInfo> GetConnectedUsersInfo();
    void UpdateUsername(Guid userId, string username);
    void UpdateActivity(Guid userId);
    List<Guid> GetInactiveUsers(TimeSpan timeout);
    void CleanupInactiveConnections(TimeSpan timeout);
    Dictionary<Guid, int> GetGamesWithPlayerCount();
    bool IsUserInGame(Guid userId, Guid gameId);
    List<Guid> GetOnlineUsers();
    Dictionary<UserStatus, int> GetUserStatusStatistics();
}

public class UserConnectionInfo
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public List<string> ConnectionIds { get; set; } = new();
    public UserStatus Status { get; set; }
    public Guid? CurrentGameId { get; set; }
    public DateTime ConnectedAt { get; set; }
    public DateTime LastActivity { get; set; }
}