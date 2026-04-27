using System.ComponentModel.DataAnnotations;
using ChessAPI.Models.Enums;

namespace ChessAPI.Models.DTOs;

public class FriendDto
{
    public Guid Id { get; set; }
    public UserDto User { get; set; } = null!;
    public FriendRequestStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
}

public class FriendRequestDto
{
    [Required]
    public Guid FriendId { get; set; }
}

public class FriendRequestResponseDto
{
    [Required]
    public Guid RequestId { get; set; }
    
    [Required]
    public bool Accept { get; set; }
}