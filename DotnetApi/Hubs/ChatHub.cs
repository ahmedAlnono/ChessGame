// Hubs/ChatHub.cs
using System.Collections.Concurrent;
using ChessAPI.Data;
using ChessAPI.Models.DTOs;
using ChessAPI.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ChessAPI.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private static readonly ConcurrentDictionary<Guid, string> _userConnections = new();
    private static readonly ConcurrentDictionary<string, List<Guid>> _chatRooms = new();
    
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        ApplicationDbContext context,
        ILogger<ChatHub> logger)
    {
        _context = context;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            _userConnections[userId.Value] = Context.ConnectionId;
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
            
            var user = await _context.Users.FindAsync(userId.Value);
            if (user != null)
            {
                user.ConnectionId = Context.ConnectionId;
                await _context.SaveChangesAsync();
                
                await Clients.All.SendAsync("UserOnline", new
                {
                    UserId = userId,
                    Username = user.Username
                });
            }

            _logger.LogInformation("User {UserId} connected to ChatHub", userId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            _userConnections.TryRemove(userId.Value, out _);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");
            
            var user = await _context.Users.FindAsync(userId.Value);
            if (user != null)
            {
                user.ConnectionId = null;
                await _context.SaveChangesAsync();
                
                await Clients.All.SendAsync("UserOffline", new
                {
                    UserId = userId,
                    Username = user.Username
                });
            }

            foreach (var room in _chatRooms.Where(r => r.Value.Contains(userId.Value)))
            {
                room.Value.Remove(userId.Value);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, room.Key);
            }
            
            _logger.LogInformation("User {UserId} disconnected from ChatHub", userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinLobby()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "Lobby");
        
        var userId = GetUserId();
        if (userId.HasValue)
        {
            if (!_chatRooms.ContainsKey("Lobby"))
            {
                _chatRooms["Lobby"] = new List<Guid>();
            }
            
            if (!_chatRooms["Lobby"].Contains(userId.Value))
            {
                _chatRooms["Lobby"].Add(userId.Value);
            }
        }

        await Clients.Caller.SendAsync("JoinedLobby", new
        {
            OnlineUsers = _chatRooms.GetValueOrDefault("Lobby", new List<Guid>()).Count
        });
    }

    public async Task LeaveLobby()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Lobby");
        
        var userId = GetUserId();
        if (userId.HasValue && _chatRooms.ContainsKey("Lobby"))
        {
            _chatRooms["Lobby"].Remove(userId.Value);
        }
    }

    public async Task JoinGameChat(Guid gameId)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        var game = await _context.Games.FindAsync(gameId);
        if (game == null)
        {
            await Clients.Caller.SendAsync("Error", "Game not found");
            return;
        }

        if (userId != game.WhitePlayerId && userId != game.BlackPlayerId)
        {
            await Clients.Caller.SendAsync("Error", "You are not a participant in this game");
            return;
        }

        var groupName = $"game-chat-{gameId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        var messages = await _context.ChatMessages
            .Where(m => m.GameId == gameId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(50)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        await Clients.Caller.SendAsync("ChatHistory", new ChatHistoryDto
        {
            Messages = messages.Select(m => new ChatMessageDto
            {
                Id = m.Id,
                SenderId = m.SenderId,
                SenderName = m.Sender.Username,
                GameId = m.GameId,
                Content = m.Content,
                IsGameChat = m.IsGameChat,
                IsPrivate = m.IsPrivate,
                CreatedAt = m.CreatedAt
            }).ToList(),
            TotalCount = messages.Count,
            Page = 1,
            PageSize = 50
        });

        await Clients.Group(groupName).SendAsync("UserJoinedChat", new
        {
            UserId = userId,
            Username = await GetUsernameAsync(userId.Value),
            GameId = gameId
        });
    }

    public async Task LeaveGameChat(Guid gameId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"game-chat-{gameId}");
    }

    public async Task SendMessage(SendMessageRequest request)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        var user = await _context.Users.FindAsync(userId.Value);
        if (user == null) return;

        var message = new ChatMessage
        {
            SenderId = userId.Value,
            GameId = request.GameId,
            ReceiverId = request.ReceiverId,
            Content = request.Content,
            IsGameChat = request.GameId.HasValue,
            IsPrivate = request.ReceiverId.HasValue,
            CreatedAt = DateTime.UtcNow
        };

        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync();

        var messageDto = new ChatMessageDto
        {
            Id = message.Id,
            SenderId = message.SenderId,
            SenderName = user.Username,
            GameId = message.GameId,
            Content = message.Content,
            IsGameChat = message.IsGameChat,
            IsPrivate = message.IsPrivate,
            CreatedAt = message.CreatedAt
        };

        if (request.GameId.HasValue)
        {
            await Clients.Group($"game-chat-{request.GameId}").SendAsync("NewMessage", messageDto);
        }
        else if (request.ReceiverId.HasValue)
        {
            await Clients.Caller.SendAsync("NewMessage", messageDto);
            
            if (_userConnections.TryGetValue(request.ReceiverId.Value, out var receiverConnectionId))
            {
                await Clients.Client(receiverConnectionId).SendAsync("NewMessage", messageDto);
            }
        }
        else
        {
            await Clients.Group("Lobby").SendAsync("NewMessage", messageDto);
        }

        _logger.LogInformation("Message sent from {SenderId} to {Target}", 
            userId, request.GameId?.ToString() ?? request.ReceiverId?.ToString() ?? "Lobby");
    }

    public async Task SendPrivateMessage(Guid receiverId, string content)
    {
        await SendMessage(new SendMessageRequest
        {
            ReceiverId = receiverId,
            Content = content
        });
    }

    public async Task GetOnlineUsers()
    {
        var onlineUsers = new List<object>();
        
        foreach (var userId in _userConnections.Keys)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                onlineUsers.Add(new
                {
                    user.Id,
                    user.Username,
                    user.Rating,
                    user.Status,
                    user.AvatarUrl
                });
            }
        }

        await Clients.Caller.SendAsync("OnlineUsers", onlineUsers);
    }

    public async Task GetChatHistory(Guid? gameId = null, Guid? otherUserId = null, int page = 1, int pageSize = 50)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        IQueryable<ChatMessage> query = _context.ChatMessages;

        if (gameId.HasValue)
        {
            query = query.Where(m => m.GameId == gameId);
        }
        else if (otherUserId.HasValue)
        {
            query = query.Where(m => 
                (m.SenderId == userId && m.ReceiverId == otherUserId) ||
                (m.SenderId == otherUserId && m.ReceiverId == userId));
        }
        else
        {
            query = query.Where(m => !m.GameId.HasValue && !m.ReceiverId.HasValue);
        }

        var totalCount = await query.CountAsync();
        
        var messages = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ChatMessageDto
            {
                Id = m.Id,
                SenderId = m.SenderId,
                SenderName = m.Sender.Username,
                GameId = m.GameId,
                Content = m.Content,
                IsGameChat = m.IsGameChat,
                IsPrivate = m.IsPrivate,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync();

        await Clients.Caller.SendAsync("ChatHistory", new ChatHistoryDto
        {
            Messages = messages,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task MarkMessageAsRead(Guid messageId)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        var message = await _context.ChatMessages.FindAsync(messageId);
        if (message != null && message.ReceiverId == userId)
        {
            message.IsRead = true;
            await _context.SaveChangesAsync();
            
            if (_userConnections.TryGetValue(message.SenderId, out var senderConnectionId))
            {
                await Clients.Client(senderConnectionId).SendAsync("MessageRead", new
                {
                    MessageId = messageId,
                    ReadBy = userId
                });
            }
        }
    }

    public async Task TypingIndicator(Guid? gameId = null, Guid? receiverId = null, bool isTyping = true)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        var username = await GetUsernameAsync(userId.Value);

        var typingData = new
        {
            UserId = userId,
            Username = username,
            IsTyping = isTyping
        };

        if (gameId.HasValue)
        {
            await Clients.OthersInGroup($"game-chat-{gameId}").SendAsync("UserTyping", typingData);
        }
        else if (receiverId.HasValue)
        {
            if (_userConnections.TryGetValue(receiverId.Value, out var receiverConnectionId))
            {
                await Clients.Client(receiverConnectionId).SendAsync("UserTyping", typingData);
            }
        }
        else
        {
            await Clients.OthersInGroup("Lobby").SendAsync("UserTyping", typingData);
        }
    }

    private Guid? GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst("UserId");
        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }
        return null;
    }

    private async Task<string> GetUsernameAsync(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        return user?.Username ?? "Unknown";
    }
}