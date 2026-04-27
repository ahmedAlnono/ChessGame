import { memo } from "react";
import { Button } from "@/components/ui/button";
import { ChevronLeft, ChevronRight, SkipBack, SkipForward, Play, Pause } from "lucide-react";

interface Props {
  currentPly: number;
  total: number;
  autoPlaying: boolean;
  onStart: () => void;
  onPrev: () => void;
  onNext: () => void;
  onEnd: () => void;
  onToggleAutoPlay: () => void;
}

export const ReplayControls = memo(({
  currentPly, total, autoPlaying, onStart, onPrev, onNext, onEnd, onToggleAutoPlay,
}: Props) => {
  const atStart = currentPly === 0;
  const atEnd = currentPly >= total;
  return (
    <div className="flex items-center justify-center gap-2">
      <Button size="icon" variant="outline" onClick={onStart} disabled={atStart} aria-label="Start">
        <SkipBack className="h-4 w-4" />
      </Button>
      <Button size="icon" variant="outline" onClick={onPrev} disabled={atStart} aria-label="Previous">
        <ChevronLeft className="h-4 w-4" />
      </Button>
      <Button
        size="icon"
        variant={autoPlaying ? "default" : "outline"}
        onClick={onToggleAutoPlay}
        disabled={total === 0}
        aria-label={autoPlaying ? "Pause auto-play" : "Auto-play"}
      >
        {autoPlaying ? <Pause className="h-4 w-4" /> : <Play className="h-4 w-4" />}
      </Button>
      <div className="px-3 py-1.5 rounded-md bg-secondary font-mono text-sm min-w-[90px] text-center">
        {currentPly} / {total}
      </div>
      <Button size="icon" variant="outline" onClick={onNext} disabled={atEnd} aria-label="Next">
        <ChevronRight className="h-4 w-4" />
      </Button>
      <Button size="icon" variant="outline" onClick={onEnd} disabled={atEnd} aria-label="End">
        <SkipForward className="h-4 w-4" />
      </Button>
    </div>
  );
});
ReplayControls.displayName = "ReplayControls";
