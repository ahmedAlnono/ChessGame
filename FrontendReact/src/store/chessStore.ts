// src/store/chessStore.ts
import { create } from "zustand";
import { Chess, Square } from "chess.js";

export interface LastMoveMeta {
  from: Square;
  to: Square;
  piece: string;
  capturedPiece?: string;
  san?: string;
  isCheck?: boolean;
  isCheckmate?: boolean;
  isCastle?: boolean;
  rookFrom?: string;
  rookTo?: string;
  ts?: number;
}

export interface MoveHistory {
  san: string;
  from: string;
  to: string;
  piece: string;
  color: string;
}

type GameStatus = "idle" | "playing" | "finished";
type GameMode = "local" | "ai" | "online";

interface ChessStore {
  // State
  chess: Chess;
  fen: string;
  status: GameStatus;
  mode: GameMode;
  whiteName: string;
  blackName: string;
  lastMove: LastMoveMeta | null;
  resultText: string;
  moveHistory: MoveHistory[];
  onlineMoveHandler:
    | ((from: string, to: string, promotion?: string) => void)
    | null;

  // Actions
  makeMove: (from: Square, to: Square, promotion?: string) => boolean;
  setFen: (fen: string) => void;
  setLastMove: (move: LastMoveMeta) => void;
  addMoveToHistory: (move: MoveHistory) => void;
  setStatus: (status: GameStatus) => void;
  setResultText: (text: string) => void;
  setOnlineMoveHandler: (
    handler: (from: string, to: string, promotion?: string) => void,
  ) => void;
  resign: (color: "w" | "b") => void;
  startGame: (options: {
    mode: GameMode;
    white: string;
    black: string;
  }) => void;
  reset: () => void;
}

const initialChess = new Chess();

export const useChessStore = create<ChessStore>((set, get) => ({
  // Initial state
  chess: initialChess,
  fen: initialChess.fen(),
  status: "idle",
  mode: "local",
  whiteName: "White",
  blackName: "Black",
  lastMove: null,
  resultText: "",
  moveHistory: [],
  onlineMoveHandler: null,

  makeMove: (from: Square, to: Square, promotion?: string) => {
    const state = get();
    const chess = state.chess;

    if (state.mode === "online") {
      if (state.onlineMoveHandler) {
        state.onlineMoveHandler(from, to, promotion);
      }
      return true;
    }

    try {
      const move = chess.move({
        from,
        to,
        promotion: promotion || "q",
      });

      if (move) {
        set({
          fen: chess.fen(),
          lastMove: {
            from: move.from as Square,
            to: move.to as Square,
            piece: move.color + move.piece.toUpperCase(),
            capturedPiece: move.captured
              ? (move.color === "w" ? "b" : "w") + move.captured.toUpperCase()
              : undefined,
            san: move.san,
            isCheck: chess.inCheck(),
            isCheckmate: chess.isCheckmate(),
            ts: Date.now(),
          },
          status: chess.isGameOver() ? "finished" : "playing",
          resultText: chess.isCheckmate()
            ? `Checkmate! ${chess.turn() === "w" ? "Black" : "White"} wins!`
            : chess.isDraw()
              ? "Draw!"
              : "",
        });

        // Add to move history
        set((prev) => ({
          moveHistory: [
            ...prev.moveHistory,
            {
              san: move.san,
              from: move.from,
              to: move.to,
              piece: move.piece,
              color: move.color,
            },
          ],
        }));

        return true;
      }
    } catch (e) {
      console.error("Invalid move", e);
    }
    return false;
  },

  
  setFen: (fen: string) => {
    const chess = get().chess;
    if (get().fen === fen) return;
    try {
      chess.load(fen);
      set({ fen });
    } catch (e) {
      console.error("Invalid FEN:", e);
    }
  },

  setLastMove: (move: LastMoveMeta) => {
    set({ lastMove: move });
  },

  addMoveToHistory: (move: MoveHistory) => {
    set((state) => ({
      moveHistory: [...state.moveHistory, move],
    }));
  },

  setStatus: (status: GameStatus) => {
    set({ status });
  },

  setResultText: (text: string) => {
    set({ resultText: text });
  },

  setOnlineMoveHandler: (
    handler: (from: string, to: string, promotion?: string) => void,
  ) => {
    if (get().onlineMoveHandler === handler) return;
    set({ onlineMoveHandler: handler });
  },

  resign: (color: "w" | "b") => {
    const state = get();
    if (state.status !== "playing") return;

    set({
      status: "finished",
      resultText: `${color === "w" ? "White" : "Black"} resigned. ${color === "w" ? "Black" : "White"} wins!`,
    });
  },

  startGame: (options: { mode: GameMode; white: string; black: string }) => {
    const newChess = new Chess();

    set({
      chess: newChess,
      fen: newChess.fen(),
      status: "playing",
      mode: options.mode,
      whiteName: options.white,
      blackName: options.black,
      lastMove: null,
      resultText: "",
      moveHistory: [],
    });
  },

  reset: () => {
    const newChess = new Chess();

    set({
      chess: newChess,
      fen: newChess.fen(),
      status: "idle",
      mode: "local",
      whiteName: "White",
      blackName: "Black",
      lastMove: null,
      resultText: "",
      moveHistory: [],
      onlineMoveHandler: null,
    });
  },
}));
