// src/components/ChallengeToast.tsx
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Swords, X, Trophy } from "lucide-react";

interface ChallengeData {
  id: string;
  fromUserId: string;
  fromUsername: string;
  fromRating: number;
  toUserId: string;
  timeControl: number;
  increment: number;
  createdAt: Date;
}

export const showChallengeToast = (
  challenge: ChallengeData,
  onAccept: () => void,
  onDecline: () => void
) => {
  const formatTimeControl = (seconds: number, increment: number) => {
    const minutes = Math.floor(seconds / 60);
    return increment > 0 ? `${minutes}+${increment}` : `${minutes} min`;
  };

  toast.custom((t) => (
    <div className="bg-background border border-border rounded-lg shadow-lg p-4 min-w-[320px] max-w-[400px]">
      <div className="flex items-start gap-3">
        <div className="h-10 w-10 rounded-full bg-accent/10 flex items-center justify-center shrink-0">
          <Swords className="h-5 w-5 text-accent" />
        </div>
        <div className="flex-1">
          <p className="font-semibold">Challenge Received!</p>
          <p className="text-sm text-muted-foreground mt-1">
            <span className="font-medium text-foreground">{challenge.fromUsername}</span>
            <span className="ml-1">({challenge.fromRating} ELO)</span>
          </p>
          <p className="text-xs text-muted-foreground mt-1">
            Time Control: {formatTimeControl(challenge.timeControl, challenge.increment)}
          </p>
          <div className="flex gap-2 mt-3">
            <Button
              size="sm"
              className="flex-1"
              onClick={() => {
                onAccept();
                toast.dismiss(t);
              }}
            >
              <Swords className="h-3.5 w-3.5 mr-1" />
              Accept
            </Button>
            <Button
              size="sm"
              variant="outline"
              className="flex-1"
              onClick={() => {
                onDecline();
                toast.dismiss(t);
              }}
            >
              <X className="h-3.5 w-3.5 mr-1" />
              Decline
            </Button>
          </div>
        </div>
        <button
          onClick={() => {
            onDecline();
            toast.dismiss(t);
          }}
          className="text-muted-foreground hover:text-foreground shrink-0"
        >
          <X className="h-4 w-4" />
        </button>
      </div>
    </div>
  ), {
    duration: 30000, // 30 seconds
    dismissible: true,
  });
};

// Success toast
export const showSuccessToast = (message: string, description?: string) => {
  toast.success(message, {
    description,
  });
};

// Error toast
export const showErrorToast = (message: string, description?: string) => {
  toast.error(message, {
    description,
  });
};

// Info toast
export const showInfoToast = (message: string, description?: string) => {
  toast.info(message, {
    description,
  });
};

// Challenge sent toast
export const showChallengeSentToast = (
  opponentName: string,
  opponentRating: number
) => {
  toast.custom((t) => (
    <div className="bg-background border border-border rounded-lg shadow-lg p-4">
      <div className="flex items-center gap-3">
        <div className="h-8 w-8 rounded-full bg-green-500/10 flex items-center justify-center">
          <Swords className="h-4 w-4 text-green-500" />
        </div>
        <div>
          <p className="font-medium">Challenge Sent!</p>
          <p className="text-sm text-muted-foreground">
            Waiting for {opponentName} ({opponentRating}) to respond...
          </p>
        </div>
      </div>
    </div>
  ), {
    duration: 5000,
  });
};

// Challenge accepted toast
export const showChallengeAcceptedToast = (gameId: string) => {
  toast.success("Challenge Accepted!", {
    description: "Redirecting to game...",
    action: {
      label: "Join Game",
      onClick: () => window.location.href = `/online-play/${gameId}`,
    },
  });
};

// Challenge declined toast
export const showChallengeDeclinedToast = (opponentName: string) => {
  toast.error("Challenge Declined", {
    description: `${opponentName} declined your challenge.`,
  });
};

// Opponent offline toast
export const showOpponentOfflineToast = () => {
  toast.error("Opponent is offline", {
    description: "They may have disconnected. Try again later.",
  });
};
