// src/pages/OnlinePlay.tsx
import { useEffect } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { ChessBoard } from "@/components/ChessBoard";
import { GamePanel } from "@/components/GamePanel";
import { MoveList } from "@/components/MoveList";
import { ChatBox } from "@/components/ChatBox";
import { SiteHeader } from "@/components/SiteHeader";
import { Button } from "@/components/ui/button";
import { useChessStore } from "@/store/chessStore";
import { useOnlineGame } from "@/hooks/useOnlineGame";
import { useChess } from "@/contexts/ChessContext";
import { Flag, Home, Wifi, WifiOff, Loader2, Crown, Shield } from "lucide-react";

const OnlinePlay = () => {
  const { gameId } = useParams();
  const navigate = useNavigate();
  const { connection } = useChess();
  const status = useChessStore((s) => s.status);
  const whiteName = useChessStore((s) => s.whiteName);
  const blackName = useChessStore((s) => s.blackName);
  
  const { 
    playerColor,  // "white" or "black"
    opponent, 
    isMyTurn, 
    isConnected,
  } = useOnlineGame(gameId);
  // Redirect if no game ID
  console.log(playerColor);
  useEffect(() => {
    if (!gameId || gameId === "undefined") {
      navigate("/", { replace: true });
    }
  }, [gameId, navigate]);

  const handleResign = async () => {
    if (connection && gameId && gameId !== "undefined") {
      try {
        await connection.invoke("ResignGame", gameId);
      } catch (error) {
        console.error("Failed to resign:", error);
      }
    }
  };


  return (
    <div className="min-h-screen flex flex-col">
      <SiteHeader />
      <main className="flex-1 container py-3 md:py-6 grid lg:grid-cols-[1fr_360px] gap-3 md:gap-6 overflow-hidden">
        <div className="flex flex-col min-h-0">
          <div className="flex items-center justify-between mb-2 md:mb-4 shrink-0">
            <div className="flex items-center gap-2">
              <h1 className="font-display text-lg md:text-3xl font-bold">
                {whiteName}{" "}
                <span className="text-muted-foreground italic">vs</span>{" "}
                {blackName}
              </h1>
              {isConnected ? (
                <Wifi className="h-4 w-4 text-green-500" />
              ) : (
                <WifiOff className="h-4 w-4 text-red-500" />
              )}
            </div>
            
            <div className="flex gap-1 md:gap-2">
              <Button
                variant="outline"
                size="sm"
                onClick={handleResign}
                disabled={status !== "playing"}
                className="h-8 px-2 md:h-9 md:px-3"
              >
                <Flag className="h-3.5 w-3.5 md:h-4 md:w-4 mr-1" />
                <span className="hidden sm:inline">Resign</span>
              </Button>
              <Button
                variant="outline"
                size="sm"
                onClick={() => navigate("/")}
                className="h-8 px-2 md:h-9 md:px-3"
              >
                <Home className="h-3.5 w-3.5 md:h-4 md:w-4 mr-1" />
                <span className="hidden sm:inline">Home</span>
              </Button>
            </div>
          </div>

          {/* Player Info Bar */}
          <div className="flex items-center justify-between mb-3 px-4">
            {/* Your Color Indicator */}
            <div className={`flex items-center gap-2 px-4 py-2 rounded-lg border ${
              playerColor === "white" 
                ? "bg-white/10 border-white/20" 
                : "bg-black/10 border-white/20"
            }`}>
              <div className={`h-4 w-4 rounded-full ${
                playerColor === "white" 
                  ? "bg-white border-2 border-gray-300" 
                  : "bg-black border-2 border-gray-500"
              }`} />
              <span className="text-sm font-medium">
                You: {playerColor === "white" ? "White" : "Black"}
                {playerColor === "white" ? (
                  <span className="ml-1">⚪</span>
                ) : (
                  <span className="ml-1">⚫</span>
                )}
              </span>
            </div>

            {/* Turn Indicator */}
            <div className={`px-4 py-2 rounded-lg border text-sm font-medium ${
              isMyTurn 
                ? "bg-green-500/10 text-green-500 border-green-500/20" 
                : "bg-secondary text-muted-foreground border-border"
            }`}>
              {isMyTurn ? "Your turn" : "Opponent's turn"}
            </div>
          </div>

          {/* Board container */}
          <div className="flex-1 flex items-center justify-center min-h-0">
            <ChessBoard 
              orientation={playerColor || "white"} 
              interactive={isMyTurn && status === "playing"}
            />
          </div>
        </div>

        {/* Right column */}
        <aside className="hidden lg:flex flex-col gap-4 min-h-0 overflow-hidden">
          {/* Player Info Card */}
          <div className="bg-secondary/30 rounded-lg p-4 space-y-3">
            {/* You */}
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <div className={`h-3 w-3 rounded-full ${
                  playerColor === "white" ? "bg-white border border-gray-300" : "bg-black"
                }`} />
                <span className="text-sm font-medium">
                  {playerColor === "white" ? whiteName : blackName} (You)
                </span>
              </div>
              {playerColor === "white" ? (
                <Crown className="h-4 w-4 text-yellow-500" />
              ) : null}
            </div>
            
            {/* Opponent */}
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <div className={`h-3 w-3 rounded-full ${
                  playerColor === "white" ? "bg-black" : "bg-white border border-gray-300"
                }`} />
                <span className="text-sm text-muted-foreground">{opponent}</span>
              </div>
              {playerColor === "black" ? (
                <Crown className="h-4 w-4 text-yellow-500" />
              ) : null}
            </div>
          </div>
          
          <GamePanel />
          <div className="flex-1 min-h-0 flex flex-col gap-4 overflow-hidden">
            <MoveList />
            <ChatBox gameId={gameId} />
          </div>
        </aside>
      </main>

      {/* Mobile bottom sheet */}
      <div className="lg:hidden border-t bg-background">
        <div className="container py-3">
          <GamePanel />
          <div className="mt-3 max-h-[40vh] overflow-y-auto">
            <MoveList />
          </div>
        </div>
      </div>
    </div>
  );
};

export default OnlinePlay;