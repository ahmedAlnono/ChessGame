import { memo, useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import { Chessboard } from "react-chessboard";
import { Chess, type Square } from "chess.js";
import { AnimatedPiece, CapturedPieceFX } from "@/components/AnimatedPiece";
import { useChessAnimations } from "@/hooks/useChessAnimations";
import { useChessSounds } from "@/hooks/useChessSounds";
import type { ReplayMove } from "@/hooks/useReplay";

interface Props {
  fen: string;
  orientation?: "white" | "black";
  /** The move that just transitioned us into the current position (null = no animation). */
  transitionMove: ReplayMove | null;
  shouldAnimate: boolean;
  soundEnabled?: boolean;
  /** Last-move highlight squares (independent of animation). */
  highlightMove?: ReplayMove | null;
}

const boardPadding = () => 16;
const innerSize = (w: number) => Math.max(0, w - boardPadding() * 2);

/**
 * Pure presentational replay board. Reuses the same animation system as the live board
 * but is fully isolated from chessStore — driven only by props.
 */
export const ReplayBoard = memo(({
  fen,
  orientation = "white",
  transitionMove,
  shouldAnimate,
  soundEnabled = true,
  highlightMove,
}: Props) => {
  const wrapperRef = useRef<HTMLDivElement>(null);
  const [boardSize, setBoardSize] = useState(480);

  const playSound = useChessSounds(soundEnabled);
  const anim = useChessAnimations({ durationMs: 220, onSound: playSound });

  useLayoutEffect(() => {
    if (!wrapperRef.current) return;
    const el = wrapperRef.current;
    const ro = new ResizeObserver(() => setBoardSize(el.clientWidth));
    ro.observe(el);
    setBoardSize(el.clientWidth);
    return () => ro.disconnect();
  }, []);

  // Trigger animation only when shouldAnimate is true.
  const lastFiredRef = useRef<ReplayMove | null>(null);
  useEffect(() => {
    if (!shouldAnimate || !transitionMove) return;
    if (lastFiredRef.current === transitionMove) return;
    lastFiredRef.current = transitionMove;
    anim.play({
      from: transitionMove.from,
      to: transitionMove.to,
      piece: transitionMove.piece,
      capturedPiece: transitionMove.capturedPiece,
      isCheck: transitionMove.isCheck,
      isCheckmate: transitionMove.isCheckmate,
      isCastle: transitionMove.isCastle,
      rookFrom: transitionMove.rookFrom,
      rookTo: transitionMove.rookTo,
    });
  }, [transitionMove, shouldAnimate, anim]);

  // Detect checkmated king from FEN for end-of-game FX.
  const checkmateKingSquare = useMemo<Square | null>(() => {
    const c = new Chess(fen);
    if (!c.isCheckmate()) return null;
    const turn = c.turn();
    const board = c.board();
    for (let r = 0; r < 8; r++) for (let col = 0; col < 8; col++) {
      const p = board[r][col];
      if (p && p.type === "k" && p.color === turn) {
        return `${"abcdefgh"[col]}${8 - r}` as Square;
      }
    }
    return null;
  }, [fen]);

  const squareStyles = useMemo(() => {
    const styles: Record<string, React.CSSProperties> = {};
    if (highlightMove) {
      styles[highlightMove.from] = { background: "hsl(var(--highlight-move) / 0.45)" };
      styles[highlightMove.to] = { background: "hsl(var(--highlight-move) / 0.55)" };
    }
    return styles;
  }, [highlightMove]);

  // Hide static destination piece during animation to prevent ghosting.
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

  const mateCss = useMemo(() => {
    if (!checkmateKingSquare) return "";
    return `[data-square="${checkmateKingSquare}"] [data-piece] { animation: king-checkmate 1.4s ease-in-out infinite; transform-origin: center; filter: drop-shadow(0 0 18px hsl(var(--highlight-check) / 0.9)); }`;
  }, [checkmateKingSquare]);

  return (
    <div ref={wrapperRef} className="board-frame rounded-lg p-3 sm:p-4 relative">
      {(hideSquaresCss || mateCss) && <style>{`${hideSquaresCss} ${mateCss}`}</style>}

      <Chessboard
        position={fen}
        boardOrientation={orientation}
        arePiecesDraggable={false}
        customSquareStyles={squareStyles}
        customLightSquareStyle={{ backgroundColor: "hsl(38 45% 85%)" }}
        customDarkSquareStyle={{ backgroundColor: "hsl(28 35% 38%)" }}
        customBoardStyle={{
          borderRadius: 4,
          boxShadow: "inset 0 0 0 1px hsl(24 30% 12%)",
        }}
        animationDuration={0}
      />

      <div
        className="pointer-events-none absolute"
        style={{ top: boardPadding(), left: boardPadding(), width: innerSize(boardSize), height: innerSize(boardSize) }}
      >
        {anim.active.map((a) => (
          <AnimatedPiece
            key={a.id}
            piece={a.move.piece}
            from={a.move.from}
            to={a.move.to}
            boardSize={innerSize(boardSize)}
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
              boardSize={innerSize(boardSize)}
              orientation={orientation}
              durationMs={anim.durationMs}
            />
          ))}
        {anim.captures.map((c) => (
          <CapturedPieceFX
            key={c.id}
            piece={c.piece}
            square={c.square}
            boardSize={innerSize(boardSize)}
            orientation={orientation}
          />
        ))}
      </div>

      {checkmateKingSquare && (
        <div
          className="board-mate-overlay pointer-events-none absolute rounded-md"
          style={{ top: boardPadding(), left: boardPadding(), width: innerSize(boardSize), height: innerSize(boardSize) }}
        />
      )}
    </div>
  );
});
ReplayBoard.displayName = "ReplayBoard";
