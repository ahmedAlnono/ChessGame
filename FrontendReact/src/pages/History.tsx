// src/pages/History.tsx
import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { SiteHeader } from "@/components/SiteHeader";
import { useAuth } from "@/contexts/AuthContext";
import { gameApi } from "@/services/api";
import { 
  History as HistoryIcon, 
  Trophy, 
  Clock, 
  Swords, 
  Loader2,
  ChevronLeft,
  ChevronRight,
  Circle
} from "lucide-react";

interface GameSummary {
  id: string;
  whitePlayerName: string;
  blackPlayerName: string;
  winnerName?: string;
  result: string;
  mode: number;
  createdAt: string;
  moveCount: number;
  terminationReason: string;
}

const History = () => {
  const navigate = useNavigate();
  const { user, isAuthenticated } = useAuth();
  const [games, setGames] = useState<GameSummary[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const pageSize = 20;

  useEffect(() => {
    if (!isAuthenticated) {
      navigate("/login");
      return;
    }

    loadGames();
  }, [isAuthenticated, page]);

  const loadGames = async () => {
    setIsLoading(true);
    try {
      const response = await gameApi.getUserGames(user?.Id || "", page, pageSize);
      const data = response.data;
      
      if (Array.isArray(data)) {
        setGames(data);
      } else if (data.items) {
        setGames(data.items);
        setTotalPages(Math.ceil(data.totalCount / pageSize));
      } else {
        setGames([]);
      }
    } catch (error) {
      console.error("Failed to load game history:", error);
      setGames([]);
    } finally {
      setIsLoading(false);
    }
  };

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleDateString("en-US", {
      year: "numeric",
      month: "short",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    });
  };

  const getResultDisplay = (game: GameSummary) => {
    const userWon = game.winnerName === user?.Username;
    const isDraw = game.result === "Draw" || game.result === "Stalemate" || 
                   game.result === "ThreefoldRepetition" || game.result === "FiftyMoveRule" || 
                   game.result === "InsufficientMaterial";

    if (isDraw) {
      return {
        text: "Draw",
        color: "text-yellow-500",
        bgColor: "bg-yellow-500/10",
        borderColor: "border-yellow-500/20",
      };
    }

    if (userWon) {
      return {
        text: "Win",
        color: "text-green-500",
        bgColor: "bg-green-500/10",
        borderColor: "border-green-500/20",
      };
    }

    return {
      text: "Loss",
      color: "text-red-500",
      bgColor: "bg-red-500/10",
      borderColor: "border-red-500/20",
    };
  };

  const getModeDisplay = (mode: number) => {
    const modes: Record<number, string> = {
      0: "Casual",
      1: "Ranked",
      2: "Tournament",
      3: "vs AI",
      4: "Challenge",
    };
    return modes[mode] || "Game";
  };

  if (!isAuthenticated) {
    return null;
  }

  return (
    <div className="min-h-screen flex flex-col">
      <SiteHeader />
      
      <main className="flex-1 container py-6 md:py-10">
        <div className="max-w-4xl mx-auto">
          {/* Header */}
          <div className="flex items-center justify-between mb-6">
            <div className="flex items-center gap-3">
              <HistoryIcon className="h-6 w-6 text-accent" />
              <h1 className="font-display text-2xl md:text-3xl font-bold">Game History</h1>
            </div>
            <div className="flex items-center gap-2">
              <Button
                variant="outline"
                size="sm"
                onClick={() => navigate("/")}
              >
                Back to Lobby
              </Button>
            </div>
          </div>

          {/* Stats Summary */}
          {games.length > 0 && (
            <div className="grid grid-cols-3 gap-4 mb-6">
              <Card className="p-4 text-center">
                <p className="text-2xl font-bold text-accent">
                  {games.filter(g => g.winnerName === user?.Username).length}
                </p>
                <p className="text-xs text-muted-foreground">Wins</p>
              </Card>
              <Card className="p-4 text-center">
                <p className="text-2xl font-bold">
                  {games.filter(g => g.result === "Draw" || g.result === "Stalemate").length}
                </p>
                <p className="text-xs text-muted-foreground">Draws</p>
              </Card>
              <Card className="p-4 text-center">
                <p className="text-2xl font-bold">
                  {games.filter(g => g.winnerName && g.winnerName !== user?.Username).length}
                </p>
                <p className="text-xs text-muted-foreground">Losses</p>
              </Card>
            </div>
          )}

          {/* Games List */}
          {isLoading ? (
            <div className="flex items-center justify-center py-12">
              <Loader2 className="h-8 w-8 animate-spin text-accent" />
            </div>
          ) : games.length === 0 ? (
            <Card className="p-12 text-center">
              <HistoryIcon className="h-12 w-12 mx-auto text-muted-foreground mb-4 opacity-50" />
              <h2 className="text-xl font-semibold mb-2">No Games Yet</h2>
              <p className="text-muted-foreground mb-4">
                Your completed games will appear here.
              </p>
              <Button onClick={() => navigate("/")}>
                Play a Game
              </Button>
            </Card>
          ) : (
            <div className="space-y-3">
              {games.map((game) => {
                const result = getResultDisplay(game);
                const isUserWhite = game.whitePlayerName === user?.Username;
                
                return (
                  <Card 
                    key={game.id} 
                    className="p-4 hover:bg-accent/5 transition-colors cursor-pointer"
                    onClick={() => navigate(`/play/${game.id}`)}
                  >
                    <div className="flex items-center justify-between">
                      <div className="flex-1 min-w-0">
                        {/* Players */}
                        <div className="flex items-center gap-2 mb-1">
                          <div className="flex items-center gap-1.5 min-w-0">
                            <Circle className={`h-2 w-2 shrink-0 ${isUserWhite ? 'fill-white border border-border' : 'fill-black border border-border'}`} />
                            <span className={`font-medium truncate ${isUserWhite ? '' : 'text-muted-foreground'}`}>
                              {game.whitePlayerName}
                            </span>
                          </div>
                          <span className="text-muted-foreground text-xs">vs</span>
                          <div className="flex items-center gap-1.5 min-w-0">
                            <Circle className={`h-2 w-2 shrink-0 ${!isUserWhite ? 'fill-white border border-border' : 'fill-black border border-border'}`} />
                            <span className={`font-medium truncate ${!isUserWhite ? '' : 'text-muted-foreground'}`}>
                              {game.blackPlayerName}
                            </span>
                          </div>
                        </div>
                        
                        {/* Meta info */}
                        <div className="flex items-center gap-3 text-xs text-muted-foreground flex-wrap">
                          <span className="flex items-center gap-1">
                            <Clock className="h-3 w-3" />
                            {formatDate(game.createdAt)}
                          </span>
                          <span className="flex items-center gap-1">
                            <Swords className="h-3 w-3" />
                            {getModeDisplay(game.mode)}
                          </span>
                          <span>{game.moveCount} moves</span>
                          {game.terminationReason && (
                            <span>• {game.terminationReason}</span>
                          )}
                        </div>
                      </div>

                      {/* Result badge */}
                      <div className={`ml-3 px-3 py-1.5 rounded-full border text-sm font-medium shrink-0 ${result.bgColor} ${result.borderColor} ${result.color}`}>
                        {result.text}
                      </div>
                    </div>
                  </Card>
                );
              })}
            </div>
          )}

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="flex items-center justify-center gap-2 mt-6">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage(p => Math.max(1, p - 1))}
                disabled={page === 1}
              >
                <ChevronLeft className="h-4 w-4" />
              </Button>
              <span className="text-sm text-muted-foreground">
                Page {page} of {totalPages}
              </span>
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage(p => Math.min(totalPages, p + 1))}
                disabled={page === totalPages}
              >
                <ChevronRight className="h-4 w-4" />
              </Button>
            </div>
          )}
        </div>
      </main>
    </div>
  );
};

export default History;