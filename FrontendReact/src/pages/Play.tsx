// Play.tsx - Updated version
import { useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { ChessBoard } from "@/components/ChessBoard";
import { GamePanel } from "@/components/GamePanel";
import { MoveList } from "@/components/MoveList";
import { ChatBox } from "@/components/ChatBox";
import { SiteHeader } from "@/components/SiteHeader";
import { Button } from "@/components/ui/button";
import { useChessStore } from "@/store/chessStore";
import { Flag, RotateCcw, Home } from "lucide-react";

const Play = () => {
  const navigate = useNavigate();
  const status = useChessStore((s) => s.status);
  const chess = useChessStore((s) => s.chess);
  const reset = useChessStore((s) => s.reset);
  const resign = useChessStore((s) => s.resign);
  const mode = useChessStore((s) => s.mode);
  const whiteName = useChessStore((s) => s.whiteName);
  const blackName = useChessStore((s) => s.blackName);
  const startGame = useChessStore((s) => s.startGame);

  useEffect(() => {
    if (status === "idle") navigate("/");
  }, [status, navigate]);

  if (status === "idle") return null;

  const handleResign = () => {
    if (mode === "ai") return resign("w");
    resign(chess.turn());
  };

  const playAgain = () => {
    startGame({
      mode: mode || "local",
      white: whiteName,
      black: blackName,
    });
  };

// Add this CSS to your global styles or component
const fullscreenStyles = `
  .fullscreen-enabled {
    background: hsl(var(--background));
  }
  
  .fullscreen-enabled .board-frame {
    height: 100vh;
    width: 100vw;
    padding: 20px;
  }
`;

// In your Play component, add the styles
return (
  <>
    <style>{fullscreenStyles}</style>
    <div className="min-h-screen flex flex-col">
      <SiteHeader />
      <main className="flex-1 container py-3 md:py-6 grid lg:grid-cols-[1fr_360px] gap-3 md:gap-6 overflow-hidden">
        {/* Left column - Chess board area */}
        <div className="flex flex-col min-h-0">
          <div className="flex items-center justify-between mb-2 md:mb-4 shrink-0">
            <h1 className="font-display text-lg md:text-3xl font-bold">
              {whiteName}{" "}
              <span className="text-muted-foreground italic">vs</span>{" "}
              {blackName}
            </h1>
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
                onClick={playAgain}
                className="h-8 px-2 md:h-9 md:px-3"
              >
                <RotateCcw className="h-3.5 w-3.5 md:h-4 md:w-4 mr-1" />
                <span className="hidden sm:inline">New</span>
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
          
          {/* Board container - takes full available height */}
          <div className="flex-1 flex items-center justify-center min-h-0">
            <ChessBoard />
          </div>
        </div>
        
        {/* Right column - Game panel and move list */}
        <aside className="hidden lg:flex flex-col gap-4 min-h-0 overflow-hidden">
          <GamePanel />
          <div className="flex-1 min-h-0 flex flex-col gap-4 overflow-hidden">
            <MoveList />
            <ChatBox />
          </div>
        </aside>
      </main>
      
      {/* Mobile bottom sheet for game panel and moves */}
      <div className="lg:hidden border-t bg-background">
        <div className="container py-3">
          <GamePanel />
          <div className="mt-3 max-h-[40vh] overflow-y-auto">
            <MoveList />
          </div>
        </div>
      </div>
    </div>
  </>
);
};

export default Play;
