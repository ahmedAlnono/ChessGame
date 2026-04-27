// src/services/api.ts
import axiosInstance from "./axios";

// ==================== Auth API ====================
export const authApi = {
  login: (email: string, password: string) =>
    axiosInstance.post("/auth/login", { email, password }),

  register: (username: string, email: string, password: string, confirmPassword: string) =>
    axiosInstance.post("/auth/register", { username, email, password, confirmPassword }),

  logout: () =>
    axiosInstance.post("/auth/logout"),

  refresh: (refreshToken: string) =>
    axiosInstance.post("/auth/refresh", { refreshToken }),

  validate: () =>
    axiosInstance.get("/auth/validate"),

  me: () =>
    axiosInstance.get("/auth/me"),

  changePassword: (currentPassword: string, newPassword: string, confirmNewPassword: string) =>
    axiosInstance.post("/auth/change-password", {
      currentPassword,
      newPassword,
      confirmNewPassword,
    }),

  checkEmail: (email: string) =>
    axiosInstance.get("/auth/check-email", { params: { email } }),

  checkUsername: (username: string) =>
    axiosInstance.get("/auth/check-username", { params: { username } }),

  revokeAllTokens: () =>
    axiosInstance.post("/auth/revoke-all"),
};

// ==================== User API ====================
export const userApi = {
  getProfile: (userId?: string) =>
    axiosInstance.get(userId ? `/users/${userId}` : "/users/me"),

  updateProfile: (data: {
    username?: string;
    bio?: string;
    country?: string;
    avatarUrl?: string;
  }) =>
    axiosInstance.put("/users/profile", data),

  getLeaderboard: (page: number = 1, pageSize: number = 20) =>
    axiosInstance.get("/users/leaderboard", { params: { page, pageSize } }),

  getStatistics: (userId?: string) =>
    axiosInstance.get(userId ? `/users/${userId}/statistics` : "/users/me/statistics"),

  searchUsers: (query: string) =>
    axiosInstance.get("/users/search", { params: { query } }),

  getUserById: (userId: string) =>
    axiosInstance.get(`/users/${userId}`),

  getOnlineUsers: () =>
    axiosInstance.get("/users/online"),
};

// ==================== Game API ====================
export const gameApi = {
  makePromotionPiece: (promotionPiece?: string) => {
    if (!promotionPiece) return undefined;

    const pieceMap: Record<string, string> = {
      q: "Queen",
      r: "Rook",
      b: "Bishop",
      n: "Knight",
    };

    return pieceMap[promotionPiece.toLowerCase()] ?? "Queen";
  },

  create: (data: {
    mode: number;
    timeControl: number;
    increment: number;
    isRated: boolean;
    isPrivate: boolean;
    opponentId?: string;
  }) =>
    axiosInstance.post("/games", data),

  getById: (gameId: string) =>
    axiosInstance.get(`/games/${gameId}`),

  getUserGames: (userId: string, page: number = 1, pageSize: number = 20) =>
    axiosInstance.get(`/games/user/${userId}`, { params: { page, pageSize } }),

  getActive: () =>
    axiosInstance.get("/games/active"),

  getState: (gameId: string) =>
    axiosInstance.get(`/games/${gameId}/state`),

  makeMove: (gameId: string, from: string, to: string, promotionPiece?: string) =>
    axiosInstance.post(`/games/${gameId}/moves`, {
      GameId: gameId,
      From: from,
      To: to,
      PromotionPiece: gameApi.makePromotionPiece(promotionPiece),
    }),

  getLegalMoves: (gameId: string, square: string) =>
    axiosInstance.get(`/games/${gameId}/moves/${square}`),

  resign: (gameId: string) =>
    axiosInstance.post(`/games/${gameId}/resign`),

  offerDraw: (gameId: string) =>
    axiosInstance.post(`/games/${gameId}/offer-draw`),

  respondToDraw: (gameId: string, accept: boolean) =>
    axiosInstance.post(`/games/${gameId}/respond-draw`, { gameId, accept }),

  abort: (gameId: string) =>
    axiosInstance.post(`/games/${gameId}/abort`),

  getMoves: (gameId: string, page: number = 1, pageSize: number = 100) =>
    axiosInstance.get(`/games/${gameId}/moves`, { params: { page, pageSize } }),

  join: (gameId: string) =>
    axiosInstance.post(`/games/${gameId}/join`),

  leave: (gameId: string) =>
    axiosInstance.post(`/games/${gameId}/leave`),
};

// ==================== Matchmaking API ====================
export const matchmakingApi = {
  joinQueue: (data: {
    mode: number;
    timeControl: number;
    increment: number;
    isRated: boolean;
    ratingRange?: number;
  }) =>
    axiosInstance.post("/matchmaking/join", data),

  leaveQueue: () =>
    axiosInstance.post("/matchmaking/leave"),

  getStatus: () =>
    axiosInstance.get("/matchmaking/status"),

  getQueueInfo: (mode?: number) =>
    axiosInstance.get("/matchmaking/queue", { params: { mode } }),
};

// ==================== Chat API ====================
export const chatApi = {
  sendMessage: (data: {
    content: string;
    gameId?: string;
    receiverId?: string;
  }) =>
    axiosInstance.post("/chat/messages", data),

  getHistory: (params: {
    gameId?: string;
    receiverId?: string;
    page?: number;
    pageSize?: number;
  }) =>
    axiosInstance.get("/chat/history", { params }),

  sendPrivateMessage: (receiverId: string, content: string) =>
    axiosInstance.post("/chat/private", { receiverId, content }),

  markAsRead: (messageId: string) =>
    axiosInstance.post(`/chat/messages/${messageId}/read`),

  getUnreadCount: () =>
    axiosInstance.get("/chat/unread"),

  getConversations: () =>
    axiosInstance.get("/chat/conversations"),

  deleteMessage: (messageId: string) =>
    axiosInstance.delete(`/chat/messages/${messageId}`),
};

// ==================== Friends API ====================
export const friendsApi = {
  getFriends: () =>
    axiosInstance.get("/friends"),

  getFriendRequests: () =>
    axiosInstance.get("/friends/requests"),

  sendFriendRequest: (friendId: string) =>
    axiosInstance.post("/friends/requests", { friendId }),

  acceptFriendRequest: (requestId: string) =>
    axiosInstance.post(`/friends/requests/${requestId}/accept`),

  rejectFriendRequest: (requestId: string) =>
    axiosInstance.post(`/friends/requests/${requestId}/reject`),

  removeFriend: (friendId: string) =>
    axiosInstance.delete(`/friends/${friendId}`),

  blockUser: (userId: string) =>
    axiosInstance.post(`/users/${userId}/block`),

  unblockUser: (userId: string) =>
    axiosInstance.post(`/users/${userId}/unblock`),
};

// ==================== Statistics API ====================
export const statisticsApi = {
  getMyStats: () =>
    axiosInstance.get("/statistics/me"),

  getUserStats: (userId: string) =>
    axiosInstance.get(`/statistics/user/${userId}`),

  getRatingHistory: (userId?: string) =>
    axiosInstance.get(userId ? `/statistics/rating/${userId}` : "/statistics/rating/me"),

  getGameHistory: (page: number = 1, pageSize: number = 20) =>
    axiosInstance.get("/statistics/games", { params: { page, pageSize } }),
};

// ==================== Admin API ====================
export const adminApi = {
  getAllUsers: (page: number = 1, pageSize: number = 50) =>
    axiosInstance.get("/admin/users", { params: { page, pageSize } }),

  banUser: (userId: string, reason: string, duration?: number) =>
    axiosInstance.post(`/admin/users/${userId}/ban`, { reason, duration }),

  unbanUser: (userId: string) =>
    axiosInstance.post(`/admin/users/${userId}/unban`),

  deleteUser: (userId: string) =>
    axiosInstance.delete(`/admin/users/${userId}`),

  getSystemStats: () =>
    axiosInstance.get("/admin/stats"),

  getActiveGames: () =>
    axiosInstance.get("/admin/games/active"),

  terminateGame: (gameId: string, reason: string) =>
    axiosInstance.post(`/admin/games/${gameId}/terminate`, { reason }),
};

// Export all APIs
export default {
  auth: authApi,
  user: userApi,
  game: gameApi,
  matchmaking: matchmakingApi,
  chat: chatApi,
  friends: friendsApi,
  statistics: statisticsApi,
  admin: adminApi,
};
