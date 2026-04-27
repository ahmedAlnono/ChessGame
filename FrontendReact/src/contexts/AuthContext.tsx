/* eslint-disable @typescript-eslint/no-explicit-any */
// src/contexts/AuthContext.tsx
import { createContext, useContext, useState, useEffect, ReactNode } from "react";
import axiosInstance from "@/services/axios";

interface User {
  Id: string;
  Email: string;
  Username: string;
  Rating: number;
  AvatarUrl?: string;
  Country?: string;
  Bio?: string;
  Role: string;
  Status: string;
  Tier: string;
  GamesPlayed: number;
  GamesWon: number;
  GamesLost: number;
  GamesDrawn: number;
  WinRate: number;
}

interface AuthContextType {
  user: User | null;
  login: (email: string, password: string) => Promise<{ success: boolean; error?: string }>;
  register: (username: string, email: string, password: string, confirmPassword: string) => Promise<{ success: boolean; error?: string }>;
  logout: () => Promise<void>;
  refreshToken: () => Promise<boolean>;
  updateUser: (user: User) => void;
  isAuthenticated: boolean;
  isLoading: boolean;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider = ({ children }: { children: ReactNode }) => {
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const initAuth = async () => {
      const accessToken = localStorage.getItem("access_token");
      const savedUser = localStorage.getItem("chess_user");

      // Check if token exists and is properly formatted
      if (accessToken && savedUser) {
        // Basic JWT format validation (three segments separated by dots)
        const isValidJwtFormat = (token: string): boolean => {
          const parts = token.split('.');
          return parts.length === 3 && parts.every(part => part.length > 0);
        };

        if (!isValidJwtFormat(accessToken)) {
          console.warn("Invalid JWT format detected, clearing tokens");
          clearAuthData();
          setIsLoading(false);
          return;
        }

        try {
          // Only validate if token format is correct
          const response = await axiosInstance.get("/auth/validate");
          
          if (response.status === 200) {
            const parsedUser = JSON.parse(savedUser);
            setUser(parsedUser);
          } else {
            clearAuthData();
          }
        } catch (error: any) {
          console.error("Token validation failed:", error?.response?.status);
          
          // If unauthorized, try to refresh token
          if (error?.response?.status === 401) {
            const refreshed = await refreshAccessToken();
            if (refreshed) {
              // Retry validation with new token
              try {
                const retryResponse = await axiosInstance.get("/auth/validate");
                if (retryResponse.status === 200) {
                  const parsedUser = JSON.parse(savedUser);
                  setUser(parsedUser);
                } else {
                  clearAuthData();
                }
              } catch {
                clearAuthData();
              }
            } else {
              clearAuthData();
            }
          } else {
            clearAuthData();
          }
        }
      } else {
        // No token, just clear
        clearAuthData(false); // Don't remove from storage again
      }
      
      setIsLoading(false);
    };

    initAuth();
  }, []);

  const clearAuthData = (removeStorage: boolean = true) => {
    if (removeStorage) {
      localStorage.removeItem("access_token");
      localStorage.removeItem("refresh_token");
      localStorage.removeItem("chess_user");
    }
    setUser(null);
  };

  const refreshAccessToken = async (): Promise<boolean> => {
    const refreshTokenValue = localStorage.getItem("refresh_token");
    if (!refreshTokenValue) return false;

    try {
      const response = await axiosInstance.post("/auth/refresh", {
        refreshToken: refreshTokenValue,
      });

      const { accessToken, refreshToken: newRefreshToken } = response.data;
      localStorage.setItem("access_token", accessToken);
      localStorage.setItem("refresh_token", newRefreshToken);
      
      return true;
    } catch (error) {
      console.error("Token refresh failed:", error);
      clearAuthData();
      return false;
    }
  };

  const login = async (email: string, password: string): Promise<{ success: boolean; error?: string }> => {
    try {
      // Clear any existing invalid tokens first
      clearAuthData();
      
      const response = await axiosInstance.post("/auth/login", {
        email,
        password,
      });

      const { AccessToken, RefreshToken, User } = response.data;
      
      // Validate token format before saving
      if (!AccessToken || typeof AccessToken !== 'string' || !AccessToken.includes('.')) {
        console.error("Invalid token format received");
        return { success: false, error: "Invalid response from server" };
      }
      
      localStorage.setItem("access_token", AccessToken);
      localStorage.setItem("refresh_token", RefreshToken);
      localStorage.setItem("chess_user", JSON.stringify(User));
      
      setUser(User);
      
      return { success: true };
    } catch (error: any) {
      console.error("Login error:", error);
      const errorMessage = error.response?.data?.message || "Invalid credentials";
      return { success: false, error: errorMessage };
    }
  };

  const register = async (
    username: string,
    email: string,
    password: string,
    confirmPassword: string
  ): Promise<{ success: boolean; error?: string }> => {
    try {
      clearAuthData();
      
      const response = await axiosInstance.post("/auth/register", {
        username,
        email,
        password,
        confirmPassword,
      });

      const { AccessToken, RefreshToken, User } = response.data;
      
      localStorage.setItem("access_token", AccessToken);
      localStorage.setItem("refresh_token", RefreshToken);
      localStorage.setItem("chess_user", JSON.stringify(User));
      
      setUser(User);
      
      return { success: true };
    } catch (error: any) {
      console.error("Registration error:", error);
      const errorMessage = error.response?.data?.message || "Registration failed";
      return { success: false, error: errorMessage };
    }
  };

  const logout = async (): Promise<void> => {
    try {
      const token = localStorage.getItem("access_token");
      if (token) {
        await axiosInstance.post("/auth/logout");
      }
    } catch (error) {
      console.error("Logout error:", error);
    } finally {
      clearAuthData();
    }
  };

  const refreshToken = async (): Promise<boolean> => {
    return await refreshAccessToken();
  };

  const updateUser = (updatedUser: User) => {
    setUser(updatedUser);
    localStorage.setItem("chess_user", JSON.stringify(updatedUser));
  };

  return (
    <AuthContext.Provider value={{ 
      user, 
      login, 
      register,
      logout, 
      refreshToken,
      updateUser,
      isAuthenticated: !!user,
      isLoading 
    }}>
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error("useAuth must be used within an AuthProvider");
  }
  return context;
};