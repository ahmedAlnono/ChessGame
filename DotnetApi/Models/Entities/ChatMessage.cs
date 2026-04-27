// Models/Entities/ChatMessage.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChessAPI.Models.Entities;

[Table("ChatMessages")]
public class ChatMessage
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid SenderId { get; set; }
    
    public Guid? GameId { get; set; }
    
    public Guid? ReceiverId { get; set; } // For private messages
    
    [Required]
    [MaxLength(500)]
    public string Content { get; set; } = string.Empty;
    
    public bool IsGameChat { get; set; } = true;
    
    public bool IsPrivate { get; set; } = false;
    
    public bool IsRead { get; set; } = false;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    [ForeignKey(nameof(SenderId))]
    public virtual User Sender { get; set; } = null!;
    
    [ForeignKey(nameof(GameId))]
    public virtual Game? Game { get; set; }
    
    [ForeignKey(nameof(ReceiverId))]
    public virtual User? Receiver { get; set; }
}