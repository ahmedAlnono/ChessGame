// Hubs/ChessHub.cs
using System.Collections.Concurrent;
using ChessAPI.Data;
using ChessAPI.Models.DTOs;
using ChessAPI.Models.Entities;
using ChessAPI.Models.Enums;
using ChessAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChessAPI.Hubs;

[Authorize]
public class ChessHub(
    ApplicationDbContext context,
    IGameService gameService,
    IChessEngine chessEngine,
    IMatchmakingService matchmakingService,
    ILogger<ChessHub> logger,
    IConnectionManager connectionManager,
    IServiceScopeFactory scopeFactory,
    IHubContext<ChessHub> hubContext) : Hub
{
    public class ChallengeInfo
    {
        public Guid ChallengeId { get; set; }
        public Guid FromUserId { get; set; }
        public string FromUsername { get; set; } = string.Empty;
        public int FromRating { get; set; }
        public Guid ToUserId { get; set; }
        public int TimeControl { get; set; }
        public int Increment { get; set; }
        public DateTime CreatedAt { get; set; }
    }
    private static readonly ConcurrentDictionary<Guid, string> _userConnections = new();
    private static readonly ConcurrentDictionary<Guid, Guid> _userGames = new();
    private static readonly ConcurrentDictionary<Guid, Timer> _gameTimers = new();

    private static readonly ConcurrentDictionary<Guid, OnlineUserInfo> _onlineUsers = new();
    private static readonly ConcurrentDictionary<Guid, ChallengeInfo> _activeChallenges = new();

    public class OnlineUserInfo
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public int Rating { get; set; }
        public UserStatus Status { get; set; }
        public string? AvatarUrl { get; set; }
    }


    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            connectionManager.AddConnection(userId.Value, Context.ConnectionId);
            connectionManager.UpdateUserStatus(userId.Value, UserStatus.Online);
            // _userConnections[userId.Value] = Context.ConnectionId;

            var user = await context.Users.FindAsync(userId.Value);
            if (user != null)
            {
                connectionManager.UpdateUsername(userId.Value, user.Username);
                user.Status = UserStatus.Online;
                user.LastActiveAt = DateTime.UtcNow;
                user.ConnectionId = Context.ConnectionId;
                await context.SaveChangesAsync();


                var onlineUser = new OnlineUserInfo
                {
                    UserId = user.Id,
                    Username = user.Username,
                    Rating = user.Rating,
                    Status = user.Status,
                    AvatarUrl = user.AvatarUrl
                };

                _onlineUsers[userId.Value] = onlineUser;

                await Clients.All.SendAsync("UserOnline", onlineUser);
                await Clients.Caller.SendAsync("OnlineUsers", _onlineUsers.Values.ToList());
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");

            logger.LogInformation("User {UserId} connected to ChessHub", userId);
        }

        await base.OnConnectedAsync();
    }

    public async Task GetOnlineUsers()
    {
        await Clients.Caller.SendAsync("OnlineUsers", _onlineUsers.Values.ToList());
    }


    public async Task SendChallenge(Guid opponentId, int timeControl = 600, int increment = 0)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            await Clients.Caller.SendAsync("Error", "You are not authenticated");
            return;
        }

        if (userId.Value == opponentId)
        {
            await Clients.Caller.SendAsync("Error", "You cannot challenge yourself");
            return;
        }

        var challenger = await context.Users.FindAsync(userId.Value);
        if (challenger == null)
        {
            await Clients.Caller.SendAsync("Error", "Challenger not found");
            return;
        }

        var opponent = await context.Users.FindAsync(opponentId);
        if (opponent == null)
        {
            await Clients.Caller.SendAsync("Error", "Opponent not found");
            return;
        }

        var opponentConnectionIds = connectionManager.GetConnectionIds(opponentId);
        if (opponentConnectionIds.Count == 0)
        {
            await Clients.Caller.SendAsync("Error", "Opponent is not online");
            return;
        }

        var opponentGame = connectionManager.GetUserGame(opponentId);
        if (opponentGame.HasValue)
        {
            await Clients.Caller.SendAsync("Error", "Opponent is already in a game");
            return;
        }

        var opponentStatus = connectionManager.GetUserStatus(opponentId);
        if (opponentStatus == UserStatus.InQueue)
        {
            await Clients.Caller.SendAsync("Error", "Opponent is currently in matchmaking");
            return;
        }

        var challengeId = Guid.NewGuid();

        // Create and store the challenge
        _activeChallenges[challengeId] = new ChallengeInfo
        {
            ChallengeId = challengeId,
            FromUserId = userId.Value,
            FromUsername = challenger.Username,
            FromRating = challenger.Rating,
            ToUserId = opponentId,
            TimeControl = timeControl,
            Increment = increment,
            CreatedAt = DateTime.UtcNow
        };

        // Create DTO for sending to clients
        var challengeDto = new
        {
            Id = challengeId,
            FromUserId = challenger.Id,
            FromUsername = challenger.Username,
            FromRating = challenger.Rating,
            ToUserId = opponentId,
            TimeControl = timeControl,
            Increment = increment,
            CreatedAt = DateTime.UtcNow
        };

        // Send to opponent
        var primaryConnectionId = opponentConnectionIds.First();
        await Clients.Client(primaryConnectionId).SendAsync("ChallengeReceived", challengeDto);

        // Confirm to sender
        await Clients.Caller.SendAsync("ChallengeSent", challengeDto);

        logger.LogInformation("Challenge sent from {Challenger} to {Opponent}",
            challenger.Username, opponent.Username);
    }


    // Add to ChessHub class
    public async Task AcceptChallenge(Guid challengeId)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            await Clients.Caller.SendAsync("Error", "You are not authenticated");
            return;
        }

        // Get the challenge from the dictionary first
        if (!_activeChallenges.TryGetValue(challengeId, out var challenge))
        {
            await Clients.Caller.SendAsync("Error", "Challenge not found or expired");
            return;
        }

        // Now challenge is defined and you can use it
        if (challenge.ToUserId != userId.Value)
        {
            await Clients.Caller.SendAsync("Error", "You are not the challenged player");
            return;
        }

        // Remove challenge from active list
        _activeChallenges.TryRemove(challengeId, out _);

        // Get usernames for logging
        var challengerName = challenge.FromUsername;
        var accepterName = await GetUsernameAsync(userId.Value);

        // Create the game with the challenger as white
        var game = await gameService.CreateGameAsync(challenge.FromUserId, new CreateGameRequest
        {
            Mode = GameMode.FriendChallenge,
            TimeControl = challenge.TimeControl,
            Increment = challenge.Increment,
            IsRated = true,
            OpponentId = challenge.ToUserId
        });

        if (game != null)
        {
            // Join the second player (accepter) to the game
            await gameService.JoinGameAsync(game.Id, challenge.ToUserId);

            var gameInfo = new
            {
                GameId = game.Id,
                ChallengeId = challengeId,
                WhitePlayer = challengerName,
                BlackPlayer = accepterName,
                TimeControl = challenge.TimeControl,
                Increment = challenge.Increment
            };

            // Notify the challenger (who sent the challenge)
            var challengerConnectionIds = connectionManager.GetConnectionIds(challenge.FromUserId);
            foreach (var connId in challengerConnectionIds)
            {
                await Clients.Client(connId).SendAsync("ChallengeAccepted", gameInfo);
            }

            // Notify the accepter (who accepted the challenge)
            var accepterConnectionIds = connectionManager.GetConnectionIds(userId.Value);
            foreach (var connId in accepterConnectionIds)
            {
                await Clients.Client(connId).SendAsync("ChallengeAccepted", gameInfo);
            }

            logger.LogInformation("Challenge {ChallengeId} accepted. Game {GameId} created between {Player1} and {Player2}",
                challengeId, game.Id, challengerName, accepterName);
        }
        else
        {
            await Clients.Caller.SendAsync("Error", "Failed to create game");
            logger.LogError("Failed to create game for challenge {ChallengeId}", challengeId);
        }
    }


    public async Task DeclineChallenge(Guid challengeId)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            await Clients.Caller.SendAsync("Error", "You are not authenticated");
            return;
        }

        // Get the challenge from the dictionary
        if (!_activeChallenges.TryGetValue(challengeId, out var challenge))
        {
            await Clients.Caller.SendAsync("Error", "Challenge not found or expired");
            return;
        }

        // Only the challenged player can decline
        if (challenge.ToUserId != userId.Value)
        {
            await Clients.Caller.SendAsync("Error", "You are not the challenged player");
            return;
        }

        // Remove from active challenges
        _activeChallenges.TryRemove(challengeId, out _);

        var declineInfo = new
        {
            ChallengeId = challengeId,
            DeclinedBy = userId.Value,
            DeclinedByUsername = await GetUsernameAsync(userId.Value)
        };

        // Notify the challenger
        var challengerConnectionIds = connectionManager.GetConnectionIds(challenge.FromUserId);
        foreach (var connId in challengerConnectionIds)
        {
            await Clients.Client(connId).SendAsync("ChallengeDeclined", declineInfo);
        }

        // Notify the decliner (current user)
        var declinerConnectionIds = connectionManager.GetConnectionIds(userId.Value);
        foreach (var connId in declinerConnectionIds)
        {
            await Clients.Client(connId).SendAsync("ChallengeDeclined", declineInfo);
        }

        logger.LogInformation("Challenge {ChallengeId} declined by {Username}",
            challengeId, await GetUsernameAsync(userId.Value));
    }


    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();

        if (userId.HasValue)
        {
            connectionManager.RemoveUserFromGame(userId.Value);
            connectionManager.RemoveConnection(userId.Value, Context.ConnectionId);
            // _userConnections.TryRemove(userId.Value, out _);
            var user = await context.Users.FindAsync(userId.Value);
            if (user != null)
            {
                user.Status = UserStatus.Offline;
                user.LastActiveAt = DateTime.UtcNow;
                user.ConnectionId = null;
                await context.SaveChangesAsync();
            }
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");

            await HandleDisconnectFromGame(userId.Value);
            await matchmakingService.LeaveQueueAsync(userId.Value);

            logger.LogInformation("User {UserId} disconnected from ChessHub", userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinGame(Guid gameId) 
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        var game = await context.Games
            .Include(g => g.WhitePlayer)
            .Include(g => g.BlackPlayer)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game == null)
        {
            await Clients.Caller.SendAsync("Error", "Game not found");
            return;
        }

        if (userId != game.WhitePlayerId && userId != game.BlackPlayerId)
        {
            await Clients.Caller.SendAsync("Error", "You are not a player in this game");
            return;
        }

        connectionManager.AddUserToGame(userId.Value, gameId);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"game-{gameId}");

        // Check if both players have joined - start the game
        var usersInGame = connectionManager.GetUsersInGame(gameId);
        if (usersInGame.Count >= 2 && game.Status == GameStatus.WaitingForOpponent)
        {
            game.Status = GameStatus.InProgress;
            game.StartedAt = DateTime.UtcNow;
            game.LastMoveAt = DateTime.UtcNow;
            await context.SaveChangesAsync();

            // Notify both players that game started
            await Clients.Group($"game-{gameId}").SendAsync("GameStarted", new
            {
                GameId = gameId,
                Status = "InProgress",
                CurrentTurn = "White" // White always starts
            });

            StartGameTimer(gameId);
        }

        // Send current game state
        var gameState = await gameService.GetGameStateAsync(gameId);
        await Clients.Caller.SendAsync("GameState", gameState);

        if (game.Status == GameStatus.InProgress)
        {
            StartGameTimer(gameId);
        }

        // Notify opponent
        var opponentId = userId == game.WhitePlayerId ? game.BlackPlayerId : game.WhitePlayerId;
        var opponentConnectionIds = connectionManager.GetConnectionIds(opponentId);
        foreach (var connId in opponentConnectionIds)
        {
            await Clients.Client(connId).SendAsync("OpponentJoined", new
            {
                UserId = userId,
                Username = await GetUsernameAsync(userId.Value)
            });
        }

        logger.LogInformation("User {UserId} joined game {GameId}. Status: {Status}, Players: {Count}",
            userId, gameId, game.Status, usersInGame.Count);
    }
    // Fix the timer to not spam GameState
    private async Task UpdateGameTime(Guid gameId)
    {
        using var scope = scopeFactory.CreateScope();
        var scopedContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var scopedGameService = scope.ServiceProvider.GetRequiredService<IGameService>();

        var game = await scopedContext.Games.FindAsync(gameId);
        if (game == null || game.Status != GameStatus.InProgress)
        {
            StopGameTimer(gameId);
            return;
        }

        var isWhiteTurn = game.CurrentFen.Contains(" w ");

        if (isWhiteTurn)
            game.WhiteTimeRemaining--;
        else
            game.BlackTimeRemaining--;

        await scopedContext.SaveChangesAsync();

        await hubContext.Clients.Group($"game-{gameId}").SendAsync("TimeUpdate", new TimeUpdateDto
        {
            GameId = gameId,
            WhiteTimeRemaining = game.WhiteTimeRemaining,
            BlackTimeRemaining = game.BlackTimeRemaining
        });

        var timeoutResult = await scopedGameService.CheckGameTimeoutAsync(gameId);
        if (timeoutResult.HasValue)
        {
            await hubContext.Clients.Group($"game-{gameId}").SendAsync("GameUpdate", new GameUpdateMessage
            {
                GameId = gameId,
                UpdateType = "GameEnded",
                Data = new
                {
                    Result = "Timeout",
                    Winner = timeoutResult
                }
            });

            StopGameTimer(gameId);
        }
    }
    public async Task LeaveGame(Guid gameId)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        connectionManager.RemoveUserFromGame(userId.Value);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"game-{gameId}");

        var game = await context.Games.FindAsync(gameId);
        if (game != null && game.Status == GameStatus.InProgress)
        {
            await gameService.LeaveGameAsync(gameId, userId.Value);
            await NotifyGameUpdate(gameId, "PlayerLeft", new { UserId = userId });
        }

        StopGameTimer(gameId);
    }



    public async Task MakeMove(MakeMoveRequest request)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        var game = await context.Games.FindAsync(request.GameId);
        if (game == null)
        {
            await Clients.Caller.SendAsync("Error", "Game not found");
            return;
        }

        if (game.Status != GameStatus.InProgress)
        {
            await Clients.Caller.SendAsync("Error", "Game is not in progress");
            return;
        }

        var isWhiteTurn = game.CurrentFen.Contains(" w ");
        if ((isWhiteTurn && userId != game.WhitePlayerId) ||
            (!isWhiteTurn && userId != game.BlackPlayerId))
        {
            await Clients.Caller.SendAsync("Error", "Not your turn");
            return;
        }

        var result = await gameService.MakeMoveAsync(request.GameId, userId.Value, request);

        if (result == null || !result.Success)
        {
            await Clients.Caller.SendAsync("Error", result?.ErrorMessage ?? "Failed to make move");
            return;
        }

        await NotifyGameUpdate(request.GameId, "MoveMade", result);

        if (result.GameResult.HasValue)
        {
            await NotifyGameUpdate(request.GameId, "GameEnded", new
            {
                Result = result.GameResult,
                Winner = game.WinnerId,
                Move = result.Move
            });

            StopGameTimer(request.GameId);
        }
        else
        {
            ResetGameTimer(request.GameId);
        }

        logger.LogInformation("Move made in game {GameId} by user {UserId}: {From}-{To}",
            request.GameId, userId, request.From, request.To);
    }

    public async Task ResignGame(Guid gameId)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        var success = await gameService.ResignGameAsync(gameId, userId.Value);

        if (success)
        {
            await NotifyGameUpdate(gameId, "GameEnded", new
            {
                Result = "Resigned",
                ResignedBy = userId
            });

            StopGameTimer(gameId);
        }
        else
        {
            await Clients.Caller.SendAsync("Error", "Failed to resign game");
        }
    }

    public async Task OfferDraw(Guid gameId)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        var game = await context.Games.FindAsync(gameId);
        if (game == null) return;

        var opponentId = userId == game.WhitePlayerId ? game.BlackPlayerId : game.WhitePlayerId;

        if (_userConnections.TryGetValue(opponentId, out var opponentConnectionId))
        {
            await Clients.Client(opponentConnectionId).SendAsync("DrawOffered", new
            {
                GameId = gameId,
                OfferedBy = userId,
                Username = await GetUsernameAsync(userId.Value)
            });
        }

        await Clients.Caller.SendAsync("DrawOfferSent");
    }

    public async Task RespondToDrawOffer(Guid gameId, bool accept)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        var success = await gameService.RespondToDrawOfferAsync(gameId, userId.Value, accept);

        if (success && accept)
        {
            await NotifyGameUpdate(gameId, "GameEnded", new
            {
                Result = "Draw",
                Reason = "Draw by agreement"
            });

            StopGameTimer(gameId);
        }
        else if (!accept)
        {
            var game = await context.Games.FindAsync(gameId);
            if (game != null)
            {
                var opponentId = userId == game.WhitePlayerId ? game.BlackPlayerId : game.WhitePlayerId;
                if (_userConnections.TryGetValue(opponentId, out var opponentConnectionId))
                {
                    await Clients.Client(opponentConnectionId).SendAsync("DrawOfferDeclined");
                }
            }
        }
    }

    public async Task GetGameState(Guid gameId)
    {
        var state = await gameService.GetGameStateAsync(gameId);
        await Clients.Caller.SendAsync("GameState", state);
    }

    public async Task GetLegalMoves(Guid gameId, string square)
    {
        var game = await context.Games.FindAsync(gameId);
        if (game == null) return;

        var moves = chessEngine.GetLegalMoves(game.CurrentFen, square);
        await Clients.Caller.SendAsync("LegalMoves", new { Square = square, Moves = moves });
    }

    public async Task JoinMatchmaking(JoinQueueRequest request)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        var status = await matchmakingService.JoinQueueAsync(userId.Value, request);
        await Clients.Caller.SendAsync("QueueStatus", status);

        _ = Task.Run(async () =>
        {
            while (status.IsInQueue)
            {
                await Task.Delay(2000);
                var match = await matchmakingService.CheckMatchAsync(userId.Value);

                if (match != null)
                {
                    await hubContext.Clients.Client(Context.ConnectionId).SendAsync("MatchFound", match);

                    using var scope = scopeFactory.CreateScope();
                    var scopedConnectionManager = scope.ServiceProvider.GetRequiredService<IConnectionManager>();
                    var connectionIds = scopedConnectionManager.GetConnectionIds(userId.Value);
                    var targetConnectionId = connectionIds.Contains(Context.ConnectionId)
                        ? Context.ConnectionId
                        : connectionIds.FirstOrDefault();

                    if (!string.IsNullOrWhiteSpace(targetConnectionId))
                    {
                        await JoinGameFromBackgroundAsync(match.GameId, userId.Value, targetConnectionId);
                    }

                    break;
                }
            }
        });
    }

    public async Task LeaveMatchmaking()
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        await matchmakingService.LeaveQueueAsync(userId.Value);
        await Clients.Caller.SendAsync("LeftQueue");
    }

    public async Task SendGameMessage(Guid gameId, string message)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        var username = await GetUsernameAsync(userId.Value);

        await Clients.Group($"game-{gameId}").SendAsync("GameMessage", new
        {
            UserId = userId,
            Username = username,
            Message = message,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task Ping()
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            connectionManager.UpdateActivity(userId.Value);

        }

        await Clients.Caller.SendAsync("Pong", DateTime.UtcNow);
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
        var user = await context.Users.FindAsync(userId);
        return user?.Username ?? "Unknown";
    }

    private async Task NotifyGameUpdate(Guid gameId, string updateType, object data)
    {
        await Clients.Group($"game-{gameId}").SendAsync("GameUpdate", new GameUpdateMessage
        {
            GameId = gameId,
            UpdateType = updateType,
            Data = data
        });
    }

    private void StartGameTimer(Guid gameId)
    {
        if (_gameTimers.ContainsKey(gameId)) return;

        var timer = new Timer(async _ =>
        {
            await UpdateGameTime(gameId);
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));

        _gameTimers[gameId] = timer;
    }

    private void StopGameTimer(Guid gameId)
    {
        if (_gameTimers.TryRemove(gameId, out var timer))
        {
            timer.Dispose();
        }
    }

    private void ResetGameTimer(Guid gameId)
    {
        StopGameTimer(gameId);
        StartGameTimer(gameId);
    }


    private async Task HandleDisconnectFromGame(Guid userId)
    {
        if (_userGames.TryGetValue(userId, out var gameId))
        {
            var game = await context.Games.FindAsync(gameId);
            if (game != null && game.Status == GameStatus.InProgress)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(30000);

                    if (!_userConnections.ContainsKey(userId))
                    {
                        using var scope = scopeFactory.CreateScope();
                        var scopedGameService = scope.ServiceProvider.GetRequiredService<IGameService>();

                        await scopedGameService.LeaveGameAsync(gameId, userId);
                        await hubContext.Clients.Group($"game-{gameId}").SendAsync("GameUpdate", new GameUpdateMessage
                        {
                            GameId = gameId,
                            UpdateType = "PlayerDisconnected",
                            Data = new { UserId = userId }
                        });
                    }
                });
            }
        }
    }

    private async Task JoinGameFromBackgroundAsync(Guid gameId, Guid userId, string connectionId)
    {
        using var scope = scopeFactory.CreateScope();
        var scopedContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var scopedGameService = scope.ServiceProvider.GetRequiredService<IGameService>();
        var scopedConnectionManager = scope.ServiceProvider.GetRequiredService<IConnectionManager>();

        var game = await scopedContext.Games
            .Include(g => g.WhitePlayer)
            .Include(g => g.BlackPlayer)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game == null) return;
        if (userId != game.WhitePlayerId && userId != game.BlackPlayerId) return;

        scopedConnectionManager.AddUserToGame(userId, gameId);
        await hubContext.Groups.AddToGroupAsync(connectionId, $"game-{gameId}");

        var usersInGame = scopedConnectionManager.GetUsersInGame(gameId);
        if (usersInGame.Count >= 2 && game.Status == GameStatus.WaitingForOpponent)
        {
            game.Status = GameStatus.InProgress;
            game.StartedAt = DateTime.UtcNow;
            game.LastMoveAt = DateTime.UtcNow;
            await scopedContext.SaveChangesAsync();

            await hubContext.Clients.Group($"game-{gameId}").SendAsync("GameStarted", new
            {
                GameId = gameId,
                Status = "InProgress",
                CurrentTurn = "White"
            });

            StartGameTimer(gameId);
        }

        var gameState = await scopedGameService.GetGameStateAsync(gameId);
        await hubContext.Clients.Client(connectionId).SendAsync("GameState", gameState);

        if (game.Status == GameStatus.InProgress)
        {
            StartGameTimer(gameId);
        }

        var opponentId = userId == game.WhitePlayerId ? game.BlackPlayerId : game.WhitePlayerId;
        var opponentConnectionIds = scopedConnectionManager.GetConnectionIds(opponentId);
        foreach (var connId in opponentConnectionIds)
        {
            await hubContext.Clients.Client(connId).SendAsync("OpponentJoined", new
            {
                UserId = userId,
                Username = await GetUsernameFromScopeAsync(scopedContext, userId)
            });
        }

        logger.LogInformation("User {UserId} joined game {GameId} from matchmaking. Status: {Status}, Players: {Count}",
            userId, gameId, game.Status, usersInGame.Count);
    }

    private static async Task<string> GetUsernameFromScopeAsync(ApplicationDbContext scopedContext, Guid userId)
    {
        var user = await scopedContext.Users.FindAsync(userId);
        return user?.Username ?? "Unknown";
    }
}
