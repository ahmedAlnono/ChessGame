// Models/Entities/Friend.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ChessAPI.Models.Enums;

namespace ChessAPI.Models.Entities;

[Table("Friends")]
public class Friend
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid UserId { get; set; }
    
    [Required]
    public Guid FriendId { get; set; }
    
    [Required]
    public FriendRequestStatus Status { get; set; } = FriendRequestStatus.Pending;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? AcceptedAt { get; set; }
    
    // Navigation properties
    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;
    
    [ForeignKey(nameof(FriendId))]
    public virtual User FriendUser { get; set; } = null!;
}