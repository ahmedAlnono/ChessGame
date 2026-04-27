// src/hooks/useOnlineGame.ts - Updated to use fixed store
import { useEffect, useState, useCallback, useRef } from "react";
import { useChessStore } from "@/store/chessStore";
import { useChess } from "@/contexts/ChessContext";
import { useAuth } from "@/contexts/AuthContext";
import { gameApi } from "@/services/api";
import { showErrorToast } from "@/components/ChallengeToast";
import { Square } from "lucide-react";

const promotionPieceMap: Record<string, string> = {
  q: "Queen",
  r: "Rook",
  b: "Bishop",
  n: "Knight",
};

const getField = <T,>(source: any, ...keys: string[]): T | undefined => {
  for (const key of keys) {
    if (source && source[key] !== undefined && source[key] !== null) {
      return source[key] as T;
    }
  }
  return undefined;
};

export const useOnlineGame = (urlGameId?: string) => {
  const { connection, isConnected } = useChess();
  const { user } = useAuth();
  const mode = useChessStore((state) => state.mode);
  const setOnlineMoveHandler = useChessStore(
    (state) => state.setOnlineMoveHandler,
  );
  const startGame = useChessStore((state) => state.startGame);
  const setFen = useChessStore((state) => state.setFen);
  const setLastMove = useChessStore((state) => state.setLastMove);
  const addMoveToHistory = useChessStore((state) => state.addMoveToHistory);
  const setStatus = useChessStore((state) => state.setStatus);
  const setResultText = useChessStore((state) => state.setResultText);
  const [opponent, setOpponent] = useState("");
  const [playerColor, setPlayerColor] = useState<"white" | "black">("white");
  const [isMyTurn, setIsMyTurn] = useState(false);
  const loadedGameIdRef = useRef<string | null>(null);
  const joinedGameIdRef = useRef<string | null>(null);

  const resolvePlayerColor = useCallback(
    (data: any): "white" | "black" => {
      
      const currentUserId = String(user?.Id ?? "").toLowerCase();
      const currentUsername = String(user?.Username ?? "").toLowerCase();

      const whitePlayerId = String(
        data.WhitePlayer?.Id ?? data.WhitePlayerId ?? "",
      ).toLowerCase();
      const blackPlayerId = String(
        data.BlackPlayer?.Id ?? data.BlackPlayerId ?? "",
      ).toLowerCase();
      const whiteUsername = String(
        data.WhitePlayer?.Username ?? data.WhitePlayerName ?? "",
      ).toLowerCase();
      const blackUsername = String(
        data.BlackPlayer?.Username ?? data.BlackPlayerName ?? "",
      ).toLowerCase();
      console.log("white player id is: "+ whitePlayerId);
      console.log("black player id is: "+ blackPlayerId);
      console.log("current player id is: "+ currentUserId);
      if (
        (currentUserId && currentUserId === whitePlayerId) ||
        (currentUsername && currentUsername === whiteUsername)
      ) {
        console.log("white is the color");
        return "white";
      }

      if (
        (currentUserId && currentUserId === blackPlayerId) ||
        (currentUsername && currentUsername === blackUsername)
      ) {
        console.log("black is the color");
        return "black";
      }

      return "white";
    },
    [user?.Id, user?.Username],
  );

  const getTurnFromGame = useCallback((data: any): "white" | "black" => {
    const fen = data.currentFen ?? data.CurrentFen;
    if (typeof fen === "string") {
      const parts = fen.split(" ");
      if (parts[1] === "w") return "white";
      if (parts[1] === "b") return "black";
    }

    const currentTurn = String(data.currentTurn ?? data.CurrentTurn ?? "")
      .trim()
      .toLowerCase();

    if (currentTurn.includes("white") || currentTurn === "w") return "white";
    if (currentTurn.includes("black") || currentTurn === "b") return "black";

    return "white";
  }, []);

  // Setup online move handler
  useEffect(() => {
    if (mode !== "online") return;

    setOnlineMoveHandler(async (from, to, promotion) => {
      if (connection && isConnected) {
        try {
          const gameId = urlGameId;
          if (gameId) {
            await connection.invoke("MakeMove", {
              GameId: gameId,
              From: from,
              To: to,
              PromotionPiece: promotion
                ? (promotionPieceMap[promotion.toLowerCase()] ?? "Queen")
                : undefined,
            });
          }
        } catch (error) {
          console.error("Failed to send move:", error);
          showErrorToast("Error", "Failed to make move");
        }
      }
    });
  }, [connection, isConnected, mode, setOnlineMoveHandler, urlGameId]);

  // Load game from server
  const loadGame = useCallback(
    async (gameId: string) => {
      if (!gameId || gameId === "undefined") {
        return false;
      }
      try {
        const response = await gameApi.getById(gameId);
        const data = response.data;
        console.log("Game data is: ");
        console.log(data);
        const color = resolvePlayerColor(data);
        const opponentName =
          color === "white"
            ? data.BlackPlayer?.Username
            : data.WhitePlayer?.Username;
        const activeTurn = getTurnFromGame(data);

        startGame({
          mode: "online",
          white: data.whitePlayer?.username || "White",
          black: data.blackPlayer?.username || "Black",
        });

        if (data.currentFen) {
          setFen(data.currentFen);
        }

        setPlayerColor(color);
        setOpponent(opponentName || "");
        setIsMyTurn(color === activeTurn);

        return true;
      } catch (error) {
        console.error("Failed to load game:", error);
        showErrorToast("Error", "Failed to load game");
        return false;
      }
    },
    [getTurnFromGame, resolvePlayerColor, setFen, startGame],
  );

  // Setup SignalR listeners
  useEffect(() => {
    if (!connection || !isConnected) return;

    const handleGameUpdate = (update: any) => {
      console.log("Game update:", update);

      const updateType = getField<string>(update, "updateType", "UpdateType");
      const data = getField<any>(update, "data", "Data");

      if (updateType === "MoveMade" && data) {
        const newFen = getField<string>(data, "newFen", "NewFen");
        const move = getField<any>(data, "move", "Move");
        const gameResult = getField<string>(data, "gameResult", "GameResult");
        const isCheck = getField<boolean>(data, "isCheck", "IsCheck");
        const isCheckmate = getField<boolean>(
          data,
          "isCheckmate",
          "IsCheckmate",
        );

        if (newFen) {
          setFen(newFen);
        }

        if (move) {
          const from = getField<string>(move, "from", "From");
          const to = getField<string>(move, "to", "To");
          const piece = getField<string>(move, "piece", "Piece");

          if (!from || !to || !piece) {
            console.warn("GameUpdate move payload missing fields:", move);
            return;
          }

          setLastMove({
            from,
            to,
            piece,
            capturedPiece: getField<string>(
              move,
              "capturedPiece",
              "CapturedPiece",
            ),
            san: getField<string>(move, "san", "San"),
            isCheck,
            isCheckmate,
            isCastle: getField<boolean>(move, "isCastle", "IsCastle"),
            rookFrom: getField<string>(move, "rookFrom", "RookFrom"),
            rookTo: getField<string>(move, "rookTo", "RookTo"),
            ts: Date.now(),
          });

          addMoveToHistory({
            san: getField<string>(move, "san", "San") || `${from}-${to}`,
            from,
            to,
            piece,
            color: getField<string>(move, "color", "Color") || "",
          });
        }

        const nextTurn =
          typeof newFen === "string"
            ? getTurnFromGame({ currentFen: newFen })
            : getTurnFromGame(data);
        setIsMyTurn(playerColor === nextTurn);

        if (gameResult) {
          setStatus("finished");
          setResultText(getResultText(gameResult));
        }
      } else if (updateType === "GameEnded") {
        setStatus("finished");
        setResultText(
          getField<string>(data, "result", "Result") || "Game Over",
        );
      } else if (updateType === "PlayerDisconnected") {
        showErrorToast(
          "Opponent Disconnected",
          "Your opponent has left the game",
        );
      }
    };

    connection.on("GameUpdate", handleGameUpdate);

    return () => {
      connection.off("GameUpdate", handleGameUpdate);
    };
  }, [
    addMoveToHistory,
    connection,
    isConnected,
    setFen,
    getTurnFromGame,
    setLastMove,
    playerColor,
    setResultText,
    setStatus,
  ]);

  // Join game on mount
  useEffect(() => {
    if (!urlGameId || !isConnected) return;

    if (loadedGameIdRef.current !== urlGameId) {
      loadedGameIdRef.current = urlGameId;
      loadGame(urlGameId);
    }

    if (connection && joinedGameIdRef.current !== urlGameId) {
      joinedGameIdRef.current = urlGameId;
      connection.invoke("JoinGame", urlGameId).catch((error) => {
        console.error("Failed to join game:", error);
        joinedGameIdRef.current = null;
      });
    }
  }, [urlGameId, isConnected, connection, loadGame]);

  return {
    gameId: urlGameId,
    playerColor,
    opponent,
    isMyTurn,
    isConnected,
    loadGame,
  };
};

function getResultText(result: string): string {
  const results: Record<string, string> = {
    WhiteWin: "White wins!",
    BlackWin: "Black wins!",
    Draw: "Draw!",
    Stalemate: "Stalemate!",
    Resigned: "Resigned",
    Timeout: "Timeout",
  };
  return results[result] || "Game Over";
}
