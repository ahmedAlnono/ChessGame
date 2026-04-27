import { memo } from "react";
import { motion } from "framer-motion";
import type { Square } from "chess.js";

interface Props {
  piece: string;          // "wP", "bN", ...
  from: Square;
  to: Square;
  boardSize: number;      // px
  orientation: "white" | "black";
  durationMs: number;
  onDone?: () => void;
}

const FILES = "abcdefgh";

// Piece symbols mapping
const PIECE_SYMBOLS: Record<string, string> = {
  wp: "♙",
  wn: "♘",
  wb: "♗",
  wr: "♖",
  wq: "♕",
  wk: "♔",
  bp: "♟",
  bn: "♞",
  bb: "♝",
  br: "♜",
  bq: "♛",
  bk: "♚",
};

// Piece colors
const PIECE_COLORS: Record<string, string> = {
  w: "#FFFFFF",
  b: "#000000",
};

const squareToXY = (sq: Square, size: number, orientation: "white" | "black") => {
  const file = FILES.indexOf(sq[0]);
  const rank = parseInt(sq[1], 10) - 1;
  const sqSize = size / 8;
  const x = orientation === "white" ? file * sqSize : (7 - file) * sqSize;
  const y = orientation === "white" ? (7 - rank) * sqSize : rank * sqSize;
  return { x, y, sqSize };
};

const getPieceSymbol = (piece: string): string => {
  return PIECE_SYMBOLS[piece.toLowerCase()] || "";
};

const getPieceColor = (piece: string): string => {
  return PIECE_COLORS[piece[0].toLowerCase()] || "#000000";
};

const getFontSize = (sqSize: number): number => {
  return sqSize * 0.8; // 80% of square size for good proportions
};

/**
 * Absolutely positioned piece that slides from `from` → `to`.
 * Renders above the board using Unicode chess symbols.
 */
export const AnimatedPiece = memo(({ piece, from, to, boardSize, orientation, durationMs, onDone }: Props) => {
  const a = squareToXY(from, boardSize, orientation);
  const b = squareToXY(to, boardSize, orientation);
  const symbol = getPieceSymbol(piece);
  const color = getPieceColor(piece);
  const fontSize = getFontSize(a.sqSize);

  return (
    <motion.div
      initial={{ x: a.x, y: a.y, opacity: 1 }}
      animate={{ x: b.x, y: b.y }}
      transition={{ duration: durationMs / 1000, ease: [0.22, 0.61, 0.36, 1] }}
      onAnimationComplete={onDone}
      style={{
        position: "absolute",
        top: 0,
        left: 0,
        width: a.sqSize,
        height: a.sqSize,
        pointerEvents: "none",
        zIndex: 30,
        willChange: "transform",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        fontSize: `${fontSize}px`,
        color: color,
        textShadow: color === "#FFFFFF" 
          ? "0 0 2px #000000, 0 0 4px #000000, 1px 1px 2px rgba(0,0,0,0.5)" 
          : "0 0 2px #FFFFFF, 0 0 4px #FFFFFF, 1px 1px 2px rgba(255,255,255,0.3)",
        fontWeight: "bold",
        lineHeight: 1,
        userSelect: "none",
      }}
    >
      {symbol}
    </motion.div>
  );
});
AnimatedPiece.displayName = "AnimatedPiece";

interface CaptureProps {
  piece: string;
  square: Square;
  boardSize: number;
  orientation: "white" | "black";
}

export const CapturedPieceFX = memo(({ piece, square, boardSize, orientation }: CaptureProps) => {
  const { x, y, sqSize } = squareToXY(square, boardSize, orientation);
  const symbol = getPieceSymbol(piece);
  const color = getPieceColor(piece);
  const fontSize = getFontSize(sqSize);

  return (
    <motion.div
      initial={{ x, y, opacity: 1, scale: 1, rotate: 0 }}
      animate={{ x, y, opacity: 0, scale: 1.6, rotate: 25 }}
      transition={{ duration: 0.4, ease: "easeOut" }}
      style={{
        position: "absolute",
        top: 0,
        left: 0,
        width: sqSize,
        height: sqSize,
        pointerEvents: "none",
        zIndex: 25,
        willChange: "transform, opacity",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        fontSize: `${fontSize}px`,
        color: color,
        textShadow: color === "#FFFFFF" 
          ? "0 0 8px #FFD700, 0 0 12px #FFA500, 1px 1px 3px rgba(0,0,0,0.5)" 
          : "0 0 8px #FFD700, 0 0 12px #FFA500, 1px 1px 3px rgba(255,255,255,0.3)",
        fontWeight: "bold",
        lineHeight: 1,
        userSelect: "none",
        filter: "drop-shadow(0 0 12px hsl(var(--highlight-check) / 0.7))",
      }}
    >
      {symbol}
    </motion.div>
  );
});
CapturedPieceFX.displayName = "CapturedPieceFX";