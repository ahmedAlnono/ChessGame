// src/components/ChatBox.tsx
import { useState, useEffect, useRef } from "react";
import { useChess } from "@/contexts/ChessContext";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Send } from "lucide-react";

interface ChatMessage {
  userId: string;
  username: string;
  message: string;
  timestamp: string;
}

interface ChatBoxProps {
  gameId?: string;
}

export const ChatBox = ({ gameId }: ChatBoxProps) => {
  const { connection, isConnected } = useChess();
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState("");
  const messagesEndRef = useRef<HTMLDivElement>(null);

  // Listen for incoming messages
  useEffect(() => {
    if (!connection || !isConnected) return;

    const handleGameMessage = (message: ChatMessage) => {
      setMessages(prev => [...prev, message]);
    };

    connection.on("GameMessage", handleGameMessage);

    return () => {
      connection.off("GameMessage", handleGameMessage);
    };
  }, [connection, isConnected]);

  // Scroll to bottom when new messages arrive
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages]);

  const sendMessage = () => {
    if (!input.trim() || !connection || !isConnected) return;

    if (gameId) {
      connection.invoke("SendGameMessage", gameId, input.trim())
        .catch(err => console.error("Failed to send message:", err));
    }

    // Add message locally (optimistic)
    const userId = JSON.parse(localStorage.getItem("chess_user") || "{}")?.id || "";
    setMessages(prev => [...prev, {
      userId,
      username: "You",
      message: input.trim(),
      timestamp: new Date().toISOString(),
    }]);

    setInput("");
  };

  const handleKeyPress = (e: React.KeyboardEvent) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      sendMessage();
    }
  };

  return (
    <div className="flex flex-col h-full">
      <h3 className="font-semibold mb-2">Chat</h3>
      
      {/* Messages */}
      <div className="flex-1 overflow-y-auto space-y-2 mb-2 min-h-[100px]">
        {messages.length === 0 ? (
          <p className="text-sm text-muted-foreground text-center py-4">
            No messages yet
          </p>
        ) : (
          messages.map((msg, index) => (
            <div key={index} className="text-sm">
              <span className="font-medium">{msg.username}: </span>
              <span className="text-muted-foreground">{msg.message}</span>
            </div>
          ))
        )}
        <div ref={messagesEndRef} />
      </div>

      {/* Input */}
      <div className="flex gap-2">
        <Input
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyPress={handleKeyPress}
          placeholder="Type a message..."
          className="flex-1 h-8 text-sm"
          disabled={!isConnected}
        />
        <Button 
          size="sm" 
          onClick={sendMessage}
          disabled={!input.trim() || !isConnected}
          className="h-8 w-8 p-0"
        >
          <Send className="h-3.5 w-3.5" />
        </Button>
      </div>
    </div>
  );
};