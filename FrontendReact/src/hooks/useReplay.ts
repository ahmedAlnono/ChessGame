import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Chess, type Square } from "chess.js";

export interface ReplayMove {
  moveNumber: number;          // 1-based ply index
  san: string;                 // PGN notation, e.g. "Nf3", "O-O"
  from: Square;
  to: Square;
  piece: string;               // "wP", "bN", ...
  capturedPiece?: string;
  promotion?: "q" | "r" | "b" | "n";
  isCastle?: boolean;
  rookFrom?: Square;
  rookTo?: Square;
  isCheck?: boolean;
  isCheckmate?: boolean;
  fenAfter: string;            // snapshot — avoids recomputing from scratch
}

export interface ReplayData {
  initialFEN: string;
  moves: ReplayMove[];
}

/**
 * Build replay data from a PGN string. Stores FEN snapshots so navigation is O(1).
 * Pure helper — no React. Safe to call inside useMemo.
 */
export const buildReplayFromPgn = (pgn: string): ReplayData => {
  const c = new Chess();
  if (pgn?.trim()) {
    try { c.loadPgn(pgn); } catch { /* fall through with empty */ }
  }
  const verbose = c.history({ verbose: true });

  const replay = new Chess();
  const initialFEN = replay.fen();
  const moves: ReplayMove[] = verbose.map((m, i) => {
    const result = replay.move({ from: m.from, to: m.to, promotion: m.promotion });
    const color = m.color;
    const pieceType = (m.promotion ?? m.piece).toUpperCase();
    const isCastle = m.flags.includes("k") || m.flags.includes("q");
    let rookFrom: Square | undefined;
    let rookTo: Square | undefined;
    if (isCastle) {
      const rank = color === "w" ? "1" : "8";
      if (m.flags.includes("k")) {
        rookFrom = `h${rank}` as Square;
        rookTo = `f${rank}` as Square;
      } else {
        rookFrom = `a${rank}` as Square;
        rookTo = `d${rank}` as Square;
      }
    }
    return {
      moveNumber: i + 1,
      san: result?.san ?? m.san,
      from: m.from as Square,
      to: m.to as Square,
      piece: `${color}${pieceType}`,
      capturedPiece: m.captured
        ? `${color === "w" ? "b" : "w"}${m.captured.toUpperCase()}`
        : undefined,
      promotion: m.promotion as ReplayMove["promotion"],
      isCastle,
      rookFrom,
      rookTo,
      isCheck: replay.inCheck() && !replay.isCheckmate(),
      isCheckmate: replay.isCheckmate(),
      fenAfter: replay.fen(),
    };
  });

  return { initialFEN, moves };
};

export interface UseReplayOptions {
  autoPlayMs?: number; // delay between auto-played moves
}

/**
 * Replay state machine. Completely isolated from live game store.
 * - currentPly = 0 → initial position (no moves played).
 * - currentPly = N → after Nth move applied.
 */
export const useReplay = (data: ReplayData | null, opts: UseReplayOptions = {}) => {
  const { autoPlayMs = 700 } = opts;
  const [currentPly, setCurrentPly] = useState(0);
  const [autoPlaying, setAutoPlaying] = useState(false);
  // When user scrubs fast (jumps >1 ply), we suppress animation for performance.
  const [shouldAnimate, setShouldAnimate] = useState(false);
  const [lastTransitionMove, setLastTransitionMove] = useState<ReplayMove | null>(null);
  const lastTickRef = useRef<number>(0);

  const total = data?.moves.length ?? 0;

  // Reset when new data loaded
  useEffect(() => {
    setCurrentPly(0);
    setAutoPlaying(false);
    setShouldAnimate(false);
    setLastTransitionMove(null);
  }, [data]);

  const goToMove = useCallback((index: number) => {
    if (!data) return;
    const target = Math.max(0, Math.min(total, index));
    setCurrentPly((prev) => {
      if (prev === target) return prev;
      const stepDelta = target - prev;
      // Only animate single-step forward transitions (not backward, not jumps).
      const animate = stepDelta === 1;
      // Throttle: if user is spamming clicks faster than animation, skip animation.
      const now = performance.now();
      const dt = now - lastTickRef.current;
      lastTickRef.current = now;
      const fastClick = dt < 180;
      setShouldAnimate(animate && !fastClick);
      setLastTransitionMove(animate ? data.moves[target - 1] : null);
      return target;
    });
  }, [data, total]);

  const next = useCallback(() => goToMove(currentPly + 1), [goToMove, currentPly]);
  const prev = useCallback(() => goToMove(currentPly - 1), [goToMove, currentPly]);
  const start = useCallback(() => goToMove(0), [goToMove]);
  const end = useCallback(() => goToMove(total), [goToMove, total]);
  const reset = useCallback(() => {
    setAutoPlaying(false);
    goToMove(0);
  }, [goToMove]);

  // Auto-play
  useEffect(() => {
    if (!autoPlaying) return;
    if (currentPly >= total) {
      setAutoPlaying(false);
      return;
    }
    const t = window.setTimeout(() => goToMove(currentPly + 1), autoPlayMs);
    return () => window.clearTimeout(t);
  }, [autoPlaying, currentPly, total, autoPlayMs, goToMove]);

  const toggleAutoPlay = useCallback(() => {
    if (!data) return;
    setAutoPlaying((p) => {
      // If at end, restart from beginning when starting auto-play.
      if (!p && currentPly >= total) goToMove(0);
      return !p;
    });
  }, [data, currentPly, total, goToMove]);

  // Current FEN derives from snapshot — O(1).
  const currentFen = useMemo(() => {
    if (!data) return new Chess().fen();
    if (currentPly === 0) return data.initialFEN;
    return data.moves[currentPly - 1].fenAfter;
  }, [data, currentPly]);

  const currentMove = currentPly > 0 && data ? data.moves[currentPly - 1] : null;

  return {
    currentPly,
    total,
    currentFen,
    currentMove,
    lastTransitionMove,
    shouldAnimate,
    autoPlaying,
    next,
    prev,
    start,
    end,
    reset,
    goToMove,
    toggleAutoPlay,
  };
};
