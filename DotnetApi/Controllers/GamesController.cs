// Controllers/GamesController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ChessAPI.Models.DTOs;
using ChessAPI.Services.Interfaces;

namespace ChessAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GamesController : ControllerBase
{
    private readonly IGameService _gameService;

    public GamesController(IGameService gameService)
    {
        _gameService = gameService;
    }

    [HttpGet("{gameId}")]
    public async Task<IActionResult> GetGame(Guid gameId)
    {
        var game = await _gameService.GetGameByIdAsync(gameId);
        if (game == null)
            return NotFound(new { message = "Game not found" });
        
        return Ok(game);
    }
}