/* eslint-disable @typescript-eslint/no-explicit-any */
// src/contexts/ChessContext.tsx (Updated)
import {
  createContext,
  useContext,
  useState,
  useEffect,
  ReactNode,
  useRef,
} from "react";
import { signalRService } from "../services/signalR";
import { useAuth } from "./AuthContext";
import * as signalR from "@microsoft/signalr";
import { useNavigate } from "react-router-dom";
import {
  showChallengeAcceptedToast,
  showChallengeDeclinedToast,
  showChallengeSentToast,
  showChallengeToast,
  showErrorToast,
  showOpponentOfflineToast,
} from "../components/ChallengeToast";
import { useChessStore } from "@/store/chessStore";

interface OnlineUser {
  UserId: string;
  Username: string;
  Rating: number;
  Status: string;
  AvatarUrl?: string;
}

interface ChessContextType {
  connection: signalR.HubConnection | null;
  isConnected: boolean;
  onlineUsers: OnlineUser[];
  pendingChallenges: Challenge[];
  sendChallenge: (opponentId: string) => Promise<void>;
  acceptChallenge: (challengeId: string) => Promise<void>;
  declineChallenge: (challengeId: string) => Promise<void>;
  reconnect: () => Promise<void>;
}

interface Challenge {
  id: string;
  fromUserId: string;
  fromUsername: string;
  fromRating: number;
  toUserId: string;
  timeControl: number;
  increment: number;
  createdAt: Date;
}

type RawOnlineUser = OnlineUser & {
  userId?: string;
  username?: string;
  rating?: number;
  status?: string;
  avatarUrl?: string;
};

type RawChallenge = Challenge & {
  Id?: string;
  FromUserId?: string;
  FromUsername?: string;
  FromRating?: number;
  ToUserId?: string;
  TimeControl?: number;
  Increment?: number;
  CreatedAt?: string | Date;
};

type RawChallengeAccepted = {
  gameId?: string;
  challengeId?: string;
  GameId?: string;
  ChallengeId?: string;
};

type RawChallengeDeclined = {
  challengeId?: string;
  declinedBy?: string;
  ChallengeId?: string;
  DeclinedBy?: string;
};

const ChessContext = createContext<ChessContextType | undefined>(undefined);

export const ChessProvider = ({ children }: { children: ReactNode }) => {
  const navigate = useNavigate();
  const { user, isAuthenticated } = useAuth();
  const [connection, setConnection] = useState<signalR.HubConnection | null>(
    null,
  );
  const [isConnected, setIsConnected] = useState(false);
  const [onlineUsers, setOnlineUsers] = useState<OnlineUser[]>([]);
  const [pendingChallenges, setPendingChallenges] = useState<Challenge[]>([]);
  const connectionAttempted = useRef(false);
  const reconnectTimer = useRef<NodeJS.Timeout | null>(null);
  const pendingChallengesRef = useRef<Map<string, string>>(new Map()); // challengeId -> toastId
  const gameStateLoadedRef = useRef<Set<string>>(new Set());
  const setFen = useChessStore((state) => state.setFen);
  const setLastMove = useChessStore((state) => state.setLastMove);

  const normalizeOnlineUser = (onlineUser: RawOnlineUser): OnlineUser => ({
    UserId: onlineUser.UserId ?? onlineUser.userId ?? "",
    Username: onlineUser.Username ?? onlineUser.username ?? "",
    Rating: onlineUser.Rating ?? onlineUser.rating ?? 0,
    Status: onlineUser.Status ?? onlineUser.status ?? "online",
    AvatarUrl: onlineUser.AvatarUrl ?? onlineUser.avatarUrl,
  });

  const normalizeChallenge = (challenge: RawChallenge): Challenge => ({
    id: challenge.id ?? challenge.Id ?? "",
    fromUserId: challenge.fromUserId ?? challenge.FromUserId ?? "",
    fromUsername: challenge.fromUsername ?? challenge.FromUsername ?? "",
    fromRating: challenge.fromRating ?? challenge.FromRating ?? 0,
    toUserId: challenge.toUserId ?? challenge.ToUserId ?? "",
    timeControl: challenge.timeControl ?? challenge.TimeControl ?? 600,
    increment: challenge.increment ?? challenge.Increment ?? 0,
    createdAt: new Date(
      challenge.createdAt ?? challenge.CreatedAt ?? new Date(),
    ),
  });

  const normalizeAcceptedChallenge = (
    data: RawChallengeAccepted,
  ): { gameId: string; challengeId?: string } => ({
    gameId: data.gameId ?? data.GameId ?? "",
    challengeId: data.challengeId ?? data.ChallengeId,
  });

  const normalizeDeclinedChallenge = (
    data: RawChallengeDeclined,
  ): { challengeId: string; declinedBy: string } => ({
    challengeId: data.challengeId ?? data.ChallengeId ?? "",
    declinedBy: data.declinedBy ?? data.DeclinedBy ?? "",
  });
  // Connect when authenticated
  useEffect(() => {
    const connectSignalR = async () => {
      if (!isAuthenticated || !user) {
        // Disconnect if not authenticated
        if (connection) {
          await signalRService.disconnect();
          setConnection(null);
          setIsConnected(false);
          setOnlineUsers([]);
        }
        connectionAttempted.current = false;
        return;
      }

      // Don't attempt if already connecting
      if (connectionAttempted.current) return;

      const token = localStorage.getItem("access_token");
      if (!token) {
        console.warn("No access token available");
        return;
      }

      connectionAttempted.current = true;

      try {
        // Disconnect existing connection first
        if (signalRService.getConnection()) {
          await signalRService.disconnect();
        }

        // Connect with new token
        await signalRService.connect(token);

        const conn = signalRService.getConnection();
        if (conn) {
          setConnection(conn);
          setIsConnected(true);
          setupHubListeners(conn);

          // Clear any reconnect timer
          if (reconnectTimer.current) {
            clearTimeout(reconnectTimer.current);
            reconnectTimer.current = null;
          }
        }
      } catch (error) {
        console.error("Failed to connect SignalR:", error);
        setIsConnected(false);

        // Schedule reconnect
        if (reconnectTimer.current) {
          clearTimeout(reconnectTimer.current);
        }
        reconnectTimer.current = setTimeout(() => {
          connectionAttempted.current = false;
          connectSignalR();
        }, 5000);
      } finally {
        connectionAttempted.current = false;
      }
    };

    connectSignalR();

    // Cleanup
    return () => {
      if (reconnectTimer.current) {
        clearTimeout(reconnectTimer.current);
      }
    };
  }, [isAuthenticated, user]);
  useEffect(() => {
    if (!connection || !isConnected) return;

    const pingInterval = setInterval(async () => {
      try {
        if (connection.state === signalR.HubConnectionState.Connected) {
          await connection.invoke("Ping");
        }
      } catch (error) {
        console.warn("Ping failed:", error);
      }
    }, 30000); // Every 30 seconds

    return () => clearInterval(pingInterval);
  }, [connection, isConnected]);

  const setupHubListeners = (conn: signalR.HubConnection) => {
    // Remove any existing listeners
    conn.off("OnlineUsers");
    conn.off("UserOnline");
    conn.off("UserOffline");
    conn.off("ChallengeReceived");
    conn.off("ChallengeAccepted");
    conn.off("ChallengeDeclined");
    conn.off("ChallengeSent");
    conn.off("Error");

    // Connection events
    conn.onreconnecting((error) => {
      console.log("SignalR Reconnecting...", error);
      setIsConnected(false);
    });

    conn.onreconnected((connectionId) => {
      console.log("SignalR Reconnected:", connectionId);
      setIsConnected(true);
      // Refresh online users after reconnect
      conn.invoke("GetOnlineUsers").catch(console.error);
    });

    conn.onclose((error) => {
      console.log("SignalR Connection Closed:", error);
      gameStateLoadedRef.current.clear();
      setIsConnected(false);
      setOnlineUsers([]);

      // Attempt to reconnect after delay
      if (reconnectTimer.current) {
        clearTimeout(reconnectTimer.current);
      }
      reconnectTimer.current = setTimeout(() => {
        connectionAttempted.current = false;
      }, 3000);
    });

    // Receive online users list
    conn.on("OnlineUsers", (users: RawOnlineUser[]) => {
      console.log("Received online users:", users);
      setOnlineUsers(users.map(normalizeOnlineUser));
    });

    // User came online
    conn.on("UserOnline", (newUser: RawOnlineUser) => {
      console.log("User online:", newUser);
      const normalizedUser = normalizeOnlineUser(newUser);
      setOnlineUsers((prev) => {
        const filtered = prev.filter((u) => u.UserId !== normalizedUser.UserId);
        return [...filtered, normalizedUser];
      });
    });

    // User went offline
    conn.on("UserOffline", (userId: string) => {
      console.log("User offline:", userId);
      setOnlineUsers((prev) => prev.filter((u) => u.UserId !== userId));
    });

    // Receive challenge
    conn.on("ChallengeReceived", (challenge: RawChallenge) => {
      const normalizedChallenge = normalizeChallenge(challenge);
      console.log("Challenge received:", normalizedChallenge);

      // Add to pending challenges
      setPendingChallenges((prev) => [...prev, normalizedChallenge]);

      // Show toast notification
      showChallengeToast(
        normalizedChallenge,
        () => {
          // Accept callback
          return acceptChallenge(normalizedChallenge.id);
        },
        () => {
          // Decline callback
          declineChallenge(normalizedChallenge.id);
        },
      );

      // Play notification sound (optional)
      // playNotificationSound();
    });

    // Challenge sent confirmation
    conn.on("ChallengeSent", (challenge: RawChallenge) => {
      const normalizedChallenge = normalizeChallenge(challenge);
      console.log("Challenge sent confirmation:", normalizedChallenge);
      showChallengeSentToast(
        normalizedChallenge.fromUsername === user?.Username
          ? onlineUsers.find((u) => u.UserId === normalizedChallenge.toUserId)
              ?.Username || "Opponent"
          : normalizedChallenge.fromUsername,
        normalizedChallenge.fromRating,
      );
    });
    // Challenge accepted - navigate to game
    conn.on("ChallengeAccepted", (data: RawChallengeAccepted) => {
      console.log("ChallengeAccepted - Full data:", data);

      const { gameId } = normalizeAcceptedChallenge(data);

      if (!gameId) {
        console.error("No gameId in response");
        return;
      }

      showChallengeAcceptedToast(gameId);

      // Instead of navigating immediately, join the game via SignalR first
      if (conn) {
        conn
          .invoke("JoinGame", gameId)
          .then(() => {
            console.log("Joined game via SignalR:", gameId);

            // Now navigate
            setTimeout(() => {
              window.location.href = `/online-play/${gameId}`;
            }, 500);
          })
          .catch((err) => {
            console.error("Failed to join game:", err);
            // Still navigate - the page will retry
            window.location.href = `/online-play/${gameId}`;
          });
      }
    });
    // Challenge declined
    // Challenge declined
    conn.on("ChallengeDeclined", (data: RawChallengeDeclined) => {
      const normalizedData = normalizeDeclinedChallenge(data);
      console.log("Challenge declined:", normalizedData);

      setPendingChallenges((prev) =>
        prev.filter((c) => c.id !== normalizedData.challengeId),
      );

      const opponentName =
        onlineUsers.find((u) => u.UserId === normalizedData.declinedBy)
          ?.Username || "Player";
      showChallengeDeclinedToast(opponentName);
      navigate("/");
    });
    // Error handling
    conn.on("Error", (error: string) => {
      console.error("Server error:", error);

      if (error.includes("not online") || error.includes("offline")) {
        showOpponentOfflineToast();
      } else {
        showErrorToast("Error", error);
      }
    });

    conn.on("UserOnline", (newUser: OnlineUser) => {
      console.log("User online:", newUser);
      setOnlineUsers((prev) => {
        const filtered = prev.filter((u) => u.UserId !== newUser.UserId);
        return [...filtered, newUser];
      });
    });

    // Game related events
    conn.on("GameUpdate", (update: any) => {
      console.log("Game update:", update);
    });

    conn.on("GameState", (state: any) => {
      console.log("Game state:", state);

      // Prevent infinite loop - only process if gameId changed
      if (state.GameId && gameStateLoadedRef.current.has(state.GameId)) {
        console.log("Skipping duplicate GameState for:", state.GameId);
        return;
      }

      if (state.GameId) {
        gameStateLoadedRef.current.add(state.GameId);
      }

      if (state.Fen && useChessStore.getState().fen !== state.Fen) {
        setFen(state.Fen);
      }

      if (state.LastMove) {
        setLastMove({
          from: state.LastMove.from,
          to: state.LastMove.to,
          piece: state.LastMove.piece,
          capturedPiece: state.LastMove.capturedPiece,
          san: state.LastMove.san,
          isCheck: state.LastMove.isCheck,
          isCheckmate: state.LastMove.isCheckmate,
          isCastle: state.LastMove.isCastle,
          rookFrom: state.LastMove.rookFrom,
          rookTo: state.LastMove.rookTo,
          ts: Date.now(),
        });
      }
    });

    conn.on("OpponentJoined", (data: any) => {
      console.log("Opponent joined:", data);
    });
  };

  const sendChallenge = async (opponentId: string): Promise<void> => {
    // Check connection and try to reconnect if needed
    if (
      !connection ||
      connection.state !== signalR.HubConnectionState.Connected
    ) {
      console.warn("Not connected, attempting to reconnect...");

      // Try to reconnect
      const token = localStorage.getItem("access_token");
      if (token && isAuthenticated) {
        try {
          await signalRService.connect(token);
          const newConn = signalRService.getConnection();
          if (newConn) {
            setConnection(newConn);
            setIsConnected(true);
            setupHubListeners(newConn);

            // Wait a bit for connection to stabilize
            await new Promise((resolve) => setTimeout(resolve, 500));
          }
        } catch (error) {
          console.error("Reconnection failed:", error);
          showErrorToast(
            "Connection Error",
            "Please refresh the page and try again",
          );
          throw new Error("Not connected to server");
        }
      } else {
        showErrorToast("Not Connected", "Please log in again");
        throw new Error("Not connected to server");
      }
    }

    // Double check connection state
    if (
      !connection ||
      connection.state !== signalR.HubConnectionState.Connected
    ) {
      showErrorToast("Connection Lost", "Please refresh the page");
      throw new Error("Not connected to server");
    }

    console.log("Sending challenge to:", opponentId);
    console.log("Connection state:", connection.state);

    try {
      await connection.invoke("SendChallenge", opponentId, 600, 0);
      console.log("Challenge sent successfully");
    } catch (error: any) {
      console.error("Failed to send challenge:", error);

      if (error.message?.includes("not online")) {
        showOpponentOfflineToast();
      } else if (error.message?.includes("Connection")) {
        setIsConnected(false);
        showErrorToast("Connection Lost", "Please refresh the page");
      } else {
        showErrorToast("Failed to Send Challenge", error.message);
      }
      throw error;
    }
  };

  const acceptChallenge = async (challengeId: string): Promise<void> => {
    console.log("Accepting challenge:", challengeId);
    console.log("Connection state:", connection?.state);

    // Check connection and try to reconnect if needed
    let activeConnection = connection;

    if (
      !activeConnection ||
      activeConnection.state !== signalR.HubConnectionState.Connected
    ) {
      console.warn(
        "Connection lost, attempting to reconnect for challenge acceptance...",
      );

      const token = localStorage.getItem("access_token");
      if (token && isAuthenticated) {
        try {
          await signalRService.connect(token);
          activeConnection = signalRService.getConnection();
          if (activeConnection) {
            setConnection(activeConnection);
            setIsConnected(true);
            setupHubListeners(activeConnection);

            // Wait for connection to stabilize
            await new Promise((resolve) => setTimeout(resolve, 500));
          }
        } catch (error) {
          console.error("Reconnection failed:", error);
          showErrorToast(
            "Connection Error",
            "Cannot accept challenge. Please refresh the page.",
          );
          throw new Error("Not connected to server");
        }
      }
    }

    // Final check
    if (
      !activeConnection ||
      activeConnection.state !== signalR.HubConnectionState.Connected
    ) {
      showErrorToast(
        "Connection Lost",
        "Cannot accept challenge. Please refresh the page.",
      );
      throw new Error("Not connected to server");
    }

    try {
      await activeConnection.invoke("AcceptChallenge", challengeId);
      console.log("Challenge accepted successfully");
    } catch (error: any) {
      console.error("Failed to accept challenge:", error);

      if (error.message?.includes("Connection")) {
        setIsConnected(false);
        showErrorToast(
          "Connection Lost",
          "Please refresh the page and try again",
        );
      } else {
        showErrorToast("Failed to Accept Challenge", error.message);
      }
      throw error;
    }
  };

  const declineChallenge = async (challengeId: string): Promise<void> => {
    console.log("Declining challenge:", challengeId);

    // For decline, we can just remove locally if not connected
    if (
      !connection ||
      connection.state !== signalR.HubConnectionState.Connected
    ) {
      console.warn("Not connected, removing challenge locally");
      setPendingChallenges((prev) => prev.filter((c) => c.id !== challengeId));
      return;
    }

    try {
      await connection.invoke("DeclineChallenge", challengeId);
      setPendingChallenges((prev) => prev.filter((c) => c.id !== challengeId));
    } catch (error: any) {
      console.error("Failed to decline challenge:", error);
      // Still remove locally
      setPendingChallenges((prev) => prev.filter((c) => c.id !== challengeId));
    }
  };

  const reconnect = async (): Promise<void> => {
    connectionAttempted.current = false;

    const token = localStorage.getItem("access_token");
    if (token && isAuthenticated) {
      try {
        await signalRService.connect(token);
        const conn = signalRService.getConnection();
        if (conn) {
          setConnection(conn);
          setIsConnected(true);
          setupHubListeners(conn);
        }
      } catch (error) {
        console.error("Manual reconnect failed:", error);
      }
    }
  };

  return (
    <ChessContext.Provider
      value={{
        connection,
        isConnected,
        onlineUsers,
        pendingChallenges,
        sendChallenge,
        acceptChallenge,
        declineChallenge,
        reconnect,
      }}
    >
      {children}
    </ChessContext.Provider>
  );
};

export const useChess = () => {
  const context = useContext(ChessContext);
  if (context === undefined) {
    throw new Error("useChess must be used within a ChessProvider");
  }
  return context;
};
