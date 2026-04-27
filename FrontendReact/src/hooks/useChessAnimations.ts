import { useEffect, useRef, useState, useCallback } from "react";
import type { Square } from "chess.js";

export interface AnimatedMove {
  from: Square;
  to: Square;
  piece: string; // e.g. "wP", "bN" (color + piece type uppercase)
  capturedPiece?: string;
  isCheck?: boolean;
  isCheckmate?: boolean;
  isCastle?: boolean;
  // For castling we also slide the rook
  rookFrom?: Square;
  rookTo?: Square;
}

export type AnimationEvent =
  | { type: "move"; move: AnimatedMove }
  | { type: "capture"; square: Square; piece: string }
  | { type: "checkmate"; kingSquare: Square };

export interface UseChessAnimationsOptions {
  durationMs?: number;
  onSound?: (kind: "move" | "capture" | "check" | "checkmate" | "castle") => void;
}

export interface ActiveAnimation {
  id: string;
  move: AnimatedMove;
  startedAt: number;
}

export interface ActiveCapture {
  id: string;
  square: Square;
  piece: string;
  startedAt: number;
}

/**
 * useChessAnimations — orchestrates piece slide + capture fade + checkmate FX.
 *
 * Usage:
 *   const anim = useChessAnimations({ onSound });
 *   useEffect(() => { if (lastMove) anim.play(lastMove); }, [lastMove]);
 *   <AnimationLayer animations={anim.active} captures={anim.captures} ... />
 */
export const useChessAnimations = (opts: UseChessAnimationsOptions = {}) => {
  const { durationMs = 220, onSound } = opts;
  const [active, setActive] = useState<ActiveAnimation[]>([]);
  const [captures, setCaptures] = useState<ActiveCapture[]>([]);
  const [checkmateAt, setCheckmateAt] = useState<Square | null>(null);
  const timers = useRef<number[]>([]);

  useEffect(() => () => {
    timers.current.forEach((t) => window.clearTimeout(t));
  }, []);

  const play = useCallback((move: AnimatedMove) => {
    const id = crypto.randomUUID();
    setActive((a) => [...a, { id, move, startedAt: performance.now() }]);

    if (move.capturedPiece) {
      const capId = crypto.randomUUID();
      setCaptures((c) => [
        ...c,
        { id: capId, square: move.to, piece: move.capturedPiece!, startedAt: performance.now() },
      ]);
      onSound?.("capture");
      const t = window.setTimeout(() => {
        setCaptures((c) => c.filter((x) => x.id !== capId));
      }, 420);
      timers.current.push(t);
    } else if (move.isCastle) {
      onSound?.("castle");
    } else {
      onSound?.("move");
    }

    const t = window.setTimeout(() => {
      setActive((a) => a.filter((x) => x.id !== id));
    }, durationMs + 20);
    timers.current.push(t);

    if (move.isCheckmate) {
      setCheckmateAt(move.to);
      onSound?.("checkmate");
    } else if (move.isCheck) {
      onSound?.("check");
    }
  }, [durationMs, onSound]);

  const clearCheckmate = useCallback(() => setCheckmateAt(null), []);

  return { active, captures, checkmateAt, play, clearCheckmate, durationMs };
};
