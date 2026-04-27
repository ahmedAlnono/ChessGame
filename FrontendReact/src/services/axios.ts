// src/services/axios.ts
import axios from "axios";

const API_BASE_URL = "http://localhost:5000/api";

export const axiosInstance = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    "Content-Type": "application/json",
  },
  timeout: 30000,
});

// Helper to check JWT format
const isValidJwtFormat = (token: string): boolean => {
  if (!token || typeof token !== 'string') return false;
  const parts = token.split('.');
  return parts.length === 3 && parts.every(part => part.length > 0);
};

// Request interceptor to add token
axiosInstance.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem("access_token");
    
    // Only add token if it's valid
    if (token && isValidJwtFormat(token)) {
      config.headers.Authorization = `Bearer ${token}`;
    } else if (token) {
      // Token is invalid - clear it
      console.warn("Invalid token format detected in interceptor, clearing");
      localStorage.removeItem("access_token");
    }
    
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

// Response interceptor for token refresh
axiosInstance.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config;
    
    // Prevent infinite retry loops
    if (error.response?.status === 401 && !originalRequest._retry) {
      originalRequest._retry = true;
      
      const refreshToken = localStorage.getItem("refresh_token");
      
      // Only attempt refresh if we have a valid refresh token
      if (refreshToken && isValidJwtFormat(refreshToken)) {
        try {
          const response = await axios.post(`${API_BASE_URL}/auth/refresh`, {
            refreshToken,
          });
          
          const { accessToken, refreshToken: newRefreshToken } = response.data;
          
          // Validate new tokens before saving
          if (isValidJwtFormat(accessToken) && isValidJwtFormat(newRefreshToken)) {
            localStorage.setItem("access_token", accessToken);
            localStorage.setItem("refresh_token", newRefreshToken);
            
            // Update Authorization header and retry
            originalRequest.headers.Authorization = `Bearer ${accessToken}`;
            return axiosInstance(originalRequest);
          } else {
            throw new Error("Invalid token format received from refresh");
          }
        } catch (refreshError) {
          // Refresh failed - clear auth and redirect
          localStorage.removeItem("access_token");
          localStorage.removeItem("refresh_token");
          localStorage.removeItem("chess_user");
          
          // Only redirect if not already on login page
          if (!window.location.pathname.includes("/login")) {
            window.location.href = "/login";
          }
          return Promise.reject(refreshError);
        }
      } else {
        // No valid refresh token - clear and redirect
        localStorage.removeItem("access_token");
        localStorage.removeItem("refresh_token");
        localStorage.removeItem("chess_user");
        
        if (!window.location.pathname.includes("/login")) {
          window.location.href = "/login";
        }
      }
    }
    
    return Promise.reject(error);
  }
);

export default axiosInstance;