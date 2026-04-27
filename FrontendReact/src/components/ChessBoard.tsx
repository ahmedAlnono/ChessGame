import { useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import { Chessboard } from "react-chessboard";
import type { Square } from "chess.js";
import { useChessStore } from "@/store/chessStore";
import { useChessAnimations } from "@/hooks/useChessAnimations";
import { useChessSounds } from "@/hooks/useChessSounds";
import { AnimatedPiece, CapturedPieceFX } from "@/components/AnimatedPiece";
import { Maximize2, Minimize2 } from "lucide-react";
import { Button } from "@/components/ui/button";

interface Props {
  orientation?: "white" | "black";
  interactive?: boolean;
}

export const ChessBoard = ({ orientation = "white", interactive = true }: Props) => {
  const fen = useChessStore((s) => s.fen);
  const chess = useChessStore((s) => s.chess);
  const lastMove = useChessStore((s) => s.lastMove);
  const status = useChessStore((s) => s.status);
  const makeMove = useChessStore((s) => s.makeMove);
  const mode = useChessStore((s) => s.mode);

  const [selected, setSelected] = useState<Square | null>(null);
  const wrapperRef = useRef<HTMLDivElement>(null);
  const boardRef = useRef<HTMLDivElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const [boardSize, setBoardSize] = useState(500);
  const [isFullscreen, setIsFullscreen] = useState(false);

  const playSound = useChessSounds(true);
  const anim = useChessAnimations({ durationMs: 220, onSound: playSound });

  // Handle fullscreen toggle
  const toggleFullscreen = () => {
    if (!containerRef.current) return;
    
    if (!isFullscreen) {
      if (containerRef.current.requestFullscreen) {
        containerRef.current.requestFullscreen();
      }
    } else {
      if (document.exitFullscreen) {
        document.exitFullscreen();
      }
    }
  };

  // Listen for fullscreen change events
  useEffect(() => {
    const handleFullscreenChange = () => {
      setIsFullscreen(!!document.fullscreenElement);
    };
    
    document.addEventListener('fullscreenchange', handleFullscreenChange);
    document.addEventListener('webkitfullscreenchange', handleFullscreenChange);
    
    return () => {
      document.removeEventListener('fullscreenchange', handleFullscreenChange);
      document.removeEventListener('webkitfullscreenchange', handleFullscreenChange);
    };
  }, []);

  // Calculate responsive board size based on container and screen
  useLayoutEffect(() => {
    if (!wrapperRef.current) return;
    
    const updateDimensions = () => {
      const wrapper = wrapperRef.current;
      if (!wrapper) return;
      
      const screenWidth = window.innerWidth;
      const screenHeight = window.innerHeight;
      const containerWidth = wrapper.clientWidth;
      const containerHeight = wrapper.clientHeight;
      
      let size;
      
      if (isFullscreen) {
        // Fullscreen mode: maximize board size
        const padding = 40; // Space for fullscreen button
        const maxByHeight = screenHeight - padding;
        const maxByWidth = screenWidth - padding;
        size = Math.min(maxByWidth, maxByHeight);
      } else if (screenWidth < 640) { 
        // Mobile: Use full available height, up to 90vh
        const maxByHeight = Math.min(containerHeight * 0.9, screenHeight * 0.7);
        const maxByWidth = containerWidth * 0.9;
        size = Math.min(maxByWidth, maxByHeight);
      } else if (screenWidth < 1024) { 
        // Tablet: Balance between width and height
        const maxByHeight = containerHeight * 0.85;
        const maxByWidth = containerWidth * 0.75;
        size = Math.min(maxByWidth, maxByHeight, 550);
      } else { 
        // Desktop: Bigger board
        const maxByHeight = containerHeight * 0.9;
        const maxByWidth = containerWidth * 0.7;
        size = Math.min(maxByWidth, maxByHeight, 650);
      }
      
      // Ensure minimum size
      const finalSize = Math.max(300, Math.floor(size));
      setBoardSize(finalSize);
    };
    
    updateDimensions();
    
    const ro = new ResizeObserver(updateDimensions);
    ro.observe(wrapperRef.current);
    
    window.addEventListener('resize', updateDimensions);
    
    return () => {
      ro.disconnect();
      window.removeEventListener('resize', updateDimensions);
    };
  }, [isFullscreen]);

  // Trigger animations when a new move lands
  const lastTsRef = useRef<number | null>(null);
  useEffect(() => {
    if (!lastMove || lastMove.ts === lastTsRef.current) return;
    lastTsRef.current = lastMove.ts;
    anim.play({
      from: lastMove.from,
      to: lastMove.to,
      piece: lastMove.piece,
      capturedPiece: lastMove.capturedPiece,
      isCheck: lastMove.isCheck,
      isCheckmate: lastMove.isCheckmate,
      isCastle: lastMove.isCastle,
      rookFrom: lastMove.rookFrom as Square,
      rookTo: lastMove.rookTo as Square,
    });
  }, [lastMove, anim]);

  // Find checkmated king square for FX
  const checkmateKingSquare = useMemo<Square | null>(() => {
    if (status !== "finished" || !chess.isCheckmate()) return null;
    const turn = chess.turn();
    const board = chess.board();
    for (let r = 0; r < 8; r++) for (let c = 0; c < 8; c++) {
      const p = board[r][c];
      if (p && p.type === "k" && p.color === turn) {
        return `${"abcdefgh"[c]}${8 - r}` as Square;
      }
    }
    return null;
  }, [status, chess]);

  const squareStyles = useMemo(() => {
    const styles: Record<string, React.CSSProperties> = {};
    if (lastMove) {
      styles[lastMove.from] = { background: "hsl(var(--highlight-move) / 0.45)" };
      styles[lastMove.to] = { background: "hsl(var(--highlight-move) / 0.55)" };
    }
    if (chess.inCheck() && !chess.isCheckmate()) {
      const turn = chess.turn();
      const board = chess.board();
      for (let r = 0; r < 8; r++) for (let c = 0; c < 8; c++) {
        const piece = board[r][c];
        if (piece && piece.type === "k" && piece.color === turn) {
          const sq = `${"abcdefgh"[c]}${8 - r}`;
          styles[sq] = {
            background: "radial-gradient(circle, hsl(var(--highlight-check) / 0.7) 0%, transparent 70%)",
          };
        }
      }
    }
    if (selected) {
      styles[selected] = { ...(styles[selected] || {}), background: "hsl(var(--highlight-legal) / 0.5)" };
      const moves = chess.moves({ square: selected, verbose: true });
      moves.forEach((m) => {
        styles[m.to] = {
          ...(styles[m.to] || {}),
          background:
            "radial-gradient(circle, hsl(var(--highlight-legal) / 0.55) 22%, transparent 24%)",
        };
      });
    }
    return styles;
  }, [lastMove, selected, chess]);

  const canMove = (square: Square) => {
    if (!interactive || status !== "playing") return false;
    const piece = chess.get(square);
    if (!piece) return false;
    if (mode === "ai" && piece.color === "b") return false;
    return piece.color === chess.turn();
  };

  const onSquareClick = (square: string) => {
    const sq = square as Square;
    if (selected) {
      const ok = makeMove(selected, sq);
      setSelected(null);
      if (!ok && canMove(sq)) setSelected(sq);
      return;
    }
    if (canMove(sq)) setSelected(sq);
  };

  const onPieceDrop = (from: string, to: string) => {
    setSelected(null);
    return makeMove(from as Square, to as Square);
  };

  const isDraggable = ({ piece }: { piece: string }) => {
    if (!interactive || status !== "playing") return false;
    const color = piece[0] === "w" ? "w" : "b";
    if (mode === "ai" && color === "b") return false;
    return color === chess.turn();
  };

  // Inject CSS to hide the static board piece on destination square(s) while animating
  const hideSquaresCss = useMemo(() => {
    if (anim.active.length === 0) return "";
    const selectors = anim.active.flatMap((a) => {
      const list = [`[data-square="${a.move.to}"] [data-piece]`];
      if (a.move.isCastle && a.move.rookTo) {
        list.push(`[data-square="${a.move.rookTo}"] [data-piece]`);
      }
      return list;
    });
    return `${selectors.join(",")} { opacity: 0 !important; }`;
  }, [anim.active]);

  // Apply checkmate class to the king image
  const mateCss = useMemo(() => {
    if (!checkmateKingSquare) return "";
    return `[data-square="${checkmateKingSquare}"] [data-piece] { animation: king-checkmate 1.4s ease-in-out infinite; transform-origin: center; filter: drop-shadow(0 0 18px hsl(var(--highlight-check) / 0.9)); }`;
  }, [checkmateKingSquare]);

  return (
    <div ref={containerRef} className="relative w-full h-full">
      {/* Fullscreen toggle button */}
      <Button
        variant="ghost"
        size="icon"
        className="absolute top-2 right-2 z-20 bg-background/50 hover:bg-background/80 backdrop-blur-sm"
        onClick={toggleFullscreen}
      >
        {isFullscreen ? (
          <Minimize2 className="h-4 w-4" />
        ) : (
          <Maximize2 className="h-4 w-4" />
        )}
      </Button>

      <div 
        ref={wrapperRef} 
        className={`board-frame rounded-lg p-2 sm:p-3 relative w-full h-full flex items-center justify-center ${
          isFullscreen ? 'bg-background' : ''
        }`}
      >
        <style>{`
          ${hideSquaresCss}
          ${mateCss}
          @keyframes king-checkmate {
            0%, 100% { filter: drop-shadow(0 0 18px hsl(var(--highlight-check) / 0.9)); }
            50% { filter: drop-shadow(0 0 28px hsl(var(--highlight-check) / 1)); }
          }
        `}</style>

        <div 
          ref={boardRef}
          className="relative transition-all duration-200"
          style={{ width: boardSize, height: boardSize }}
        >
          <Chessboard
            position={fen}
            boardOrientation={orientation}
            onSquareClick={onSquareClick}
            onPieceDrop={onPieceDrop}
            isDraggablePiece={isDraggable}
            customSquareStyles={squareStyles}
            customLightSquareStyle={{ backgroundColor: "hsl(38 45% 85%)" }}
            customDarkSquareStyle={{ backgroundColor: "hsl(28 35% 38%)" }}
            customBoardStyle={{
              borderRadius: 4,
              boxShadow: "inset 0 0 0 1px hsl(24 30% 12%)",
            }}
            animationDuration={0}
          />

          {/* Animation overlay layer */}
          <div className="pointer-events-none absolute inset-0">
            {anim.active.map((a) => (
              <AnimatedPiece
                key={a.id}
                piece={a.move.piece}
                from={a.move.from}
                to={a.move.to}
                boardSize={boardSize}
                orientation={orientation}
                durationMs={anim.durationMs}
              />
            ))}
            {anim.active
              .filter((a) => a.move.isCastle && a.move.rookFrom && a.move.rookTo)
              .map((a) => (
                <AnimatedPiece
                  key={`${a.id}-rook`}
                  piece={`${a.move.piece[0]}R`}
                  from={a.move.rookFrom!}
                  to={a.move.rookTo!}
                  boardSize={boardSize}
                  orientation={orientation}
                  durationMs={anim.durationMs}
                />
              ))}
            {anim.captures.map((c) => (
              <CapturedPieceFX
                key={c.id}
                piece={c.piece}
                square={c.square}
                boardSize={boardSize}
                orientation={orientation}
              />
            ))}
          </div>

          {/* Checkmate dramatic overlay */}
          {checkmateKingSquare && (
            <div className="board-mate-overlay pointer-events-none absolute inset-0 rounded-md" />
          )}
        </div>
      </div>
    </div>
  );
};