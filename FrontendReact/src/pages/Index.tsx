// src/pages/Index.tsx (Updated with all online users section)
import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { useChessStore } from "@/store/chessStore";
import { SiteHeader } from "@/components/SiteHeader";
import { useAuth } from "@/contexts/AuthContext";
import { useChess } from "@/contexts/ChessContext";
import { 
  Crown, 
  Swords, 
  Bot, 
  History, 
  Circle, 
  Users, 
  UserPlus,
  Clock,
  Trophy,
  Wifi,
  WifiOff,
  ChevronRight
} from "lucide-react";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";

const Index = () => {
  const navigate = useNavigate();
  const { user, isAuthenticated } = useAuth();
  const { onlineUsers, isConnected, sendChallenge, pendingChallenges } = useChess();
  const startGame = useChessStore((s) => s.startGame);
  
  const [white, setWhite] = useState(isAuthenticated ? user?.Username || "You" : "You");
  const [black, setBlack] = useState("Hikaru");
  const [selectedOpponent, setSelectedOpponent] = useState<string>("");
  const [aiName, setAiName] = useState("Stockfish Jr.");
  const [playerName, setPlayerName] = useState(isAuthenticated ? user?.Username || "You" : "You");
  const [activeTab, setActiveTab] = useState("online");

  const startLocal = () => {
    startGame({ mode: "local", white, black });
    navigate("/play");
  };

  const startAI = () => {
    startGame({ mode: "ai", white: playerName, black: aiName });
    navigate("/play");
  };

  const challengePlayer = async () => {
    if (selectedOpponent) {
      await sendChallenge(selectedOpponent);
      // Show success message or navigate
    }
  };

  const getRatingBadgeColor = (rating: number) => {
    if (rating >= 2000) return "bg-purple-500/10 text-purple-500 border-purple-500/20";
    if (rating >= 1600) return "bg-blue-500/10 text-blue-500 border-blue-500/20";
    if (rating >= 1200) return "bg-green-500/10 text-green-500 border-green-500/20";
    return "bg-gray-500/10 text-gray-500 border-gray-500/20";
  };

  const getInitials = (name: string) => {
    return name;
  };

  const allConnectedUsers = user
    ? [
        {
          UserId: user.Id,
          Username: user.Username,
          Rating: user.Rating,
          Status: user.Status || "online",
        },
        ...onlineUsers.filter((onlineUser) => onlineUser.UserId !== user.Id),
      ]
    : onlineUsers;

  return (
    <div className="min-h-screen flex flex-col">
      <SiteHeader />

      {/* Hero */}
      <section className="relative overflow-hidden border-b border-border">
        <div className="absolute inset-0 opacity-[0.06] pointer-events-none"
          style={{
            backgroundImage:
              "repeating-conic-gradient(hsl(var(--foreground)) 0% 25%, transparent 0% 50%)",
            backgroundSize: "120px 120px",
          }}
        />
        <div className="container py-12 md:py-20 relative">
          <div className="max-w-3xl">
            <div className="inline-flex items-center gap-2 text-xs uppercase tracking-[0.25em] text-accent mb-5">
              <Crown className="h-3.5 w-3.5" /> Est. MMXXV · Classical Rules
            </div>
            <h1 className="font-display text-4xl md:text-6xl font-bold leading-[1.05] mb-5">
              {isAuthenticated ? (
                <>
                  Welcome back,
                  <br />
                  <span className="italic text-accent">{user?.Username}</span>
                </>
              ) : (
                <>
                  The royal game,
                  <br />
                  <span className="italic text-accent">refined.</span>
                </>
              )}
            </h1>
            <p className="text-lg text-muted-foreground max-w-xl">
              {isAuthenticated 
                ? "Ready for your next match? Challenge a player online or practice against the AI."
                : "A faithfully crafted chess parlour. Drag your pieces, command your bishops, and outwit your rival."
              }
            </p>
            {!isAuthenticated && (
              <div className="mt-6 flex items-center gap-3">
                <Button onClick={() => navigate("/login")} size="lg">
                  Sign in to play online
                </Button>
                <Button variant="outline" size="lg" onClick={() => navigate("/play")}>
                  Play as guest
                </Button>
              </div>
            )}
          </div>
        </div>
      </section>

      {/* Main Content */}
      <section className="container py-8 md:py-12">
        <div className="grid lg:grid-cols-3 gap-6">
          
          {/* Left Column - Online Players */}
          <div className="lg:col-span-1">
            <Card className="paper-card p-4 md:p-6">
              <div className="flex items-center justify-between mb-4">
                <div className="flex items-center gap-2">
                  <Users className="h-5 w-5 text-accent" />
                  <h2 className="font-display text-xl font-semibold">Online Players</h2>
                </div>
                <div className="flex items-center gap-2">
                  {isConnected ? (
                    <Badge variant="outline" className="bg-green-500/10 text-green-500 border-green-500/20">
                      <Wifi className="h-3 w-3 mr-1" />
                      Connected
                    </Badge>
                  ) : (
                    <Badge variant="outline" className="bg-red-500/10 text-red-500 border-red-500/20">
                      <WifiOff className="h-3 w-3 mr-1" />
                      Disconnected
                    </Badge>
                  )}
                  <Badge variant="secondary">
                    {onlineUsers.length} online
                  </Badge>
                </div>
              </div>

              {!isAuthenticated ? (
                <div className="text-center py-8">
                  <Users className="h-12 w-12 mx-auto text-muted-foreground mb-3 opacity-50" />
                  <p className="text-muted-foreground mb-4">
                    Sign in to see and challenge online players
                  </p>
                  <Button onClick={() => navigate("/login")} variant="outline" size="sm">
                    Sign In
                  </Button>
                </div>
              ) : (
                <>
                  {/* Online Users List */}
                  <div className="space-y-2 max-h-[400px] overflow-y-auto pr-1">
                    {allConnectedUsers.map((onlineUser) => {
                      const isCurrentUser = onlineUser.UserId === user?.Id;

                      return (
                        <div
                          key={onlineUser.UserId}
                          className={`p-3 rounded-lg transition-colors border group ${
                            isCurrentUser
                              ? "bg-blue-500/10 border-blue-500/30"
                              : "border-transparent hover:bg-accent/5 hover:border-accent/20 cursor-pointer"
                          }`}
                          onClick={() => {
                            if (!isCurrentUser) {
                              setSelectedOpponent(onlineUser.UserId);
                            }
                          }}
                        >
                          <div className="flex items-center gap-3">
                            <Avatar className={`h-10 w-10 ${isCurrentUser ? "border-2 border-blue-500/40" : ""}`}>
                              <AvatarFallback className={isCurrentUser ? "bg-blue-500/15 text-blue-600" : "bg-secondary"}>
                                {getInitials(onlineUser.Username)}
                              </AvatarFallback>
                            </Avatar>
                            <div className="flex-1 min-w-0">
                              <div className="flex items-center gap-2">
                                <p className={`font-medium truncate ${isCurrentUser ? "text-blue-600" : ""}`}>
                                  {onlineUser.Username}
                                </p>
                                {isCurrentUser && (
                                  <Badge variant="outline" className="border-blue-500/30 bg-blue-500/10 text-blue-600">
                                    You
                                  </Badge>
                                )}
                                <Circle className="h-2 w-2 fill-green-500 text-green-500 shrink-0" />
                              </div>
                              <div className="flex items-center gap-2 text-xs text-muted-foreground">
                                <Trophy className="h-3 w-3" />
                                <span>{onlineUser.Rating} ELO</span>
                                <span className="inline-block w-1 h-1 bg-border rounded-full" />
                                <span className="capitalize">{onlineUser.Status}</span>
                              </div>
                            </div>
                            <Button
                              size="sm"
                              variant="ghost"
                              className={`transition-opacity ${
                                isCurrentUser ? "hidden" : "opacity-0 group-hover:opacity-100"
                              }`}
                              onClick={(e) => {
                                e.stopPropagation();
                                if (!isCurrentUser) {
                                  setSelectedOpponent(onlineUser.UserId);
                                }
                              }}
                            >
                              <Swords className="h-4 w-4" />
                            </Button>
                          </div>
                        </div>
                      );
                    })}
                    
                    {allConnectedUsers.filter((u) => u.UserId !== user?.Id).length === 0 && (
                      <div className="text-center py-8">
                        <Users className="h-12 w-12 mx-auto text-muted-foreground mb-3 opacity-50" />
                        <p className="text-muted-foreground text-sm">
                          No other players online
                        </p>
                        <p className="text-xs text-muted-foreground mt-1">
                          Check back later or challenge the AI!
                        </p>
                      </div>
                    )}
                  </div>

                  {/* Challenge Section */}
                  {selectedOpponent && (
                    <div className="mt-4 pt-4 border-t">
                      <div className="flex items-center justify-between mb-3">
                        <p className="text-sm font-medium">Selected Opponent</p>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => setSelectedOpponent("")}
                        >
                          Clear
                        </Button>
                      </div>
                      <Button 
                        onClick={challengePlayer} 
                        className="w-full"
                        disabled={!isConnected}
                      >
                        <Swords className="h-4 w-4 mr-2" />
                        Challenge to a Match
                      </Button>
                    </div>
                  )}
                </>
              )}
            </Card>
          </div>

          {/* Right Column - Game Options */}
          <div className="lg:col-span-2">
            <Tabs defaultValue="local" className="space-y-4">
              <TabsList className="grid w-full grid-cols-2">
                <TabsTrigger value="local">
                  <Users className="h-4 w-4 mr-2" />
                  Local Multiplayer
                </TabsTrigger>
                <TabsTrigger value="ai">
                  <Bot className="h-4 w-4 mr-2" />
                  vs AI
                </TabsTrigger>
              </TabsList>

              <TabsContent value="local">
                <Card className="paper-card p-6 md:p-8">
                  <div className="flex items-center gap-3 mb-4">
                    <Swords className="h-6 w-6 text-accent" />
                    <h2 className="font-display text-2xl font-semibold">Two Players, One Board</h2>
                  </div>
                  <p className="text-sm text-muted-foreground mb-6">
                    Pass-and-play. Perfect for sharing a screen across the table.
                  </p>
                  <div className="space-y-4">
                    <div>
                      <Label htmlFor="w">White player</Label>
                      <Input 
                        id="w" 
                        value={white} 
                        onChange={(e) => setWhite(e.target.value)} 
                        className="bg-background mt-1.5" 
                      />
                    </div>
                    <div>
                      <Label htmlFor="b">Black player</Label>
                      <Input 
                        id="b" 
                        value={black} 
                        onChange={(e) => setBlack(e.target.value)} 
                        className="bg-background mt-1.5" 
                      />
                    </div>
                    <Button onClick={startLocal} size="lg" className="w-full font-semibold">
                      Open the board
                      <ChevronRight className="h-4 w-4 ml-2" />
                    </Button>
                  </div>
                </Card>
              </TabsContent>

              <TabsContent value="ai">
                <Card className="paper-card p-6 md:p-8">
                  <div className="flex items-center gap-3 mb-4">
                    <Bot className="h-6 w-6 text-accent" />
                    <h2 className="font-display text-2xl font-semibold">Challenge the House</h2>
                  </div>
                  <p className="text-sm text-muted-foreground mb-6">
                    A spirited bot — favours captures, otherwise plays by intuition. You command white.
                  </p>
                  <div className="space-y-4">
                    <div>
                      <Label htmlFor="p">Your name</Label>
                      <Input 
                        id="p" 
                        value={playerName} 
                        onChange={(e) => setPlayerName(e.target.value)} 
                        className="bg-background mt-1.5" 
                      />
                    </div>
                    <div>
                      <Label htmlFor="ai">Opponent</Label>
                      <Input 
                        id="ai" 
                        value={aiName} 
                        onChange={(e) => setAiName(e.target.value)} 
                        className="bg-background mt-1.5" 
                      />
                    </div>
                    <div className="flex items-center gap-2 text-sm text-muted-foreground">
                      <Clock className="h-4 w-4" />
                      <span>Time Control: 10 minutes</span>
                    </div>
                    <Button onClick={startAI} size="lg" variant="secondary" className="w-full font-semibold">
                      Begin the duel
                      <ChevronRight className="h-4 w-4 ml-2" />
                    </Button>
                  </div>
                </Card>
              </TabsContent>
            </Tabs>

            {/* Quick Stats Card */}
            {isAuthenticated && (
              <Card className="paper-card p-4 md:p-6 mt-4">
                <h3 className="font-display text-lg font-semibold mb-3">Your Stats</h3>
                <div className="grid grid-cols-3 gap-4 text-center">
                  <div>
                    <p className="text-2xl font-bold text-accent">{user?.Rating || 1200}</p>
                    <p className="text-xs text-muted-foreground">Rating</p>
                  </div>
                  <div>
                    <p className="text-2xl font-bold">{user?.GamesPlayed || 0}</p>
                    <p className="text-xs text-muted-foreground">Games</p>
                  </div>
                  <div>
                    <p className="text-2xl font-bold">{user?.WinRate || 0}%</p>
                    <p className="text-xs text-muted-foreground">Win Rate</p>
                  </div>
                </div>
              </Card>
            )}
          </div>
        </div>
      </section>

      {/* Footer strip */}
      <section className="border-t border-border bg-card/40 mt-auto">
        <div className="container py-6 flex flex-col md:flex-row items-center justify-between gap-4">
          <p className="text-sm text-muted-foreground flex items-center gap-2">
            <History className="h-4 w-4" /> Past games are recorded in your local archive.
          </p>
          <p className="text-xs uppercase tracking-[0.2em] text-muted-foreground">
            Castling · En passant · Promotion · Stalemate
          </p>
        </div>
      </section>
    </div>
  );
};

export default Index;
