// Models/DTOs/ChatDTOs.cs
using System.ComponentModel.DataAnnotations;

namespace ChessAPI.Models.DTOs;

public class ChatMessageDto
{
    public Guid Id { get; set; }
    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public Guid? GameId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsGameChat { get; set; }
    public bool IsPrivate { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SendMessageRequest
{
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;
    
    public Guid? GameId { get; set; }
    
    public Guid? ReceiverId { get; set; }
}

public class ChatHistoryDto
{
    public List<ChatMessageDto> Messages { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}