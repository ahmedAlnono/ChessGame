// src/services/signalr.ts (Updated)
import * as signalR from "@microsoft/signalr";

const HUB_URL = "http://localhost:5000/hubs/chess";

class SignalRService {
  private connection: signalR.HubConnection | null = null;
  private reconnectAttempts = 0;
  private maxReconnectAttempts = 10;
  private reconnectDelay = 3000;

  async connect(token: string): Promise<void> {
    // If already connected with same token, skip
    if (this.connection?.state === signalR.HubConnectionState.Connected) {
      console.log("SignalR already connected");
      return;
    }

    // Disconnect existing connection if any
    if (this.connection) {
      await this.disconnect();
    }

    console.log("Connecting to SignalR...");
    
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL, {
        accessTokenFactory: () => token,
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          this.reconnectAttempts = retryContext.previousRetryCount;
          
          if (retryContext.previousRetryCount >= this.maxReconnectAttempts) {
            console.log("Max reconnect attempts reached");
            return null;
          }
          
          const delay = Math.min(
            1000 * Math.pow(2, retryContext.previousRetryCount),
            30000
          );
          console.log(`Reconnecting in ${delay}ms (attempt ${retryContext.previousRetryCount + 1})`);
          return delay;
        },
      })
      .configureLogging(signalR.LogLevel.Information)
      .build();

    // Setup connection event handlers
    this.connection.onreconnecting((error) => {
      console.log("SignalR reconnecting...", error);
    });

    this.connection.onreconnected((connectionId) => {
      console.log("SignalR reconnected:", connectionId);
      this.reconnectAttempts = 0;
    });

    this.connection.onclose((error) => {
      console.log("SignalR connection closed:", error);
      this.connection = null;
    });

    try {
      await this.connection.start();
      this.reconnectAttempts = 0;
      console.log("SignalR connected successfully");
    } catch (error) {
      console.error("SignalR connection failed:", error);
      this.connection = null;
      throw error;
    }
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      try {
        await this.connection.stop();
        console.log("SignalR disconnected");
      } catch (error) {
        console.error("Error disconnecting SignalR:", error);
      } finally {
        this.connection = null;
      }
    }
  }

  getConnection(): signalR.HubConnection | null {
    return this.connection;
  }

  isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected;
  }

  getState(): signalR.HubConnectionState | null {
    return this.connection?.state ?? null;
  }
}

export const signalRService = new SignalRService();