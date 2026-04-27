import { memo, useEffect, useRef } from "react";
import { ScrollArea } from "@/components/ui/scroll-area";
import { cn } from "@/lib/utils";
import type { ReplayMove } from "@/hooks/useReplay";

interface Props {
  moves: ReplayMove[];
  currentPly: number;
  onSelect: (ply: number) => void;
  className?: string;
  height?: number;
}

/**
 * PGN-style move list. Highlights current ply, click to jump.
 * Auto-scrolls the active move into view.
 */
export const ReplayMoveList = memo(({ moves, currentPly, onSelect, className, height = 420 }: Props) => {
  const activeRef = useRef<HTMLTableCellElement>(null);

  useEffect(() => {
    activeRef.current?.scrollIntoView({ block: "nearest", behavior: "smooth" });
  }, [currentPly]);

  if (moves.length === 0) {
    return <p className="text-sm text-muted-foreground">No moves recorded.</p>;
  }

  const rows = Math.ceil(moves.length / 2);

  return (
    <ScrollArea className={cn("pr-2", className)} style={{ height }}>
      <table className="w-full text-sm font-mono">
        <tbody>
          {Array.from({ length: rows }).map((_, i) => {
            const wPly = i * 2 + 1;
            const bPly = i * 2 + 2;
            const w = moves[wPly - 1];
            const b = moves[bPly - 1];
            return (
              <tr key={i} className="border-b border-border/50 last:border-0">
                <td className="py-1 pr-2 text-muted-foreground w-8">{i + 1}.</td>
                <td
                  ref={currentPly === wPly ? activeRef : undefined}
                  onClick={() => onSelect(wPly)}
                  className={cn(
                    "py-1 pr-2 cursor-pointer rounded px-1 hover:bg-secondary",
                    currentPly === wPly && "bg-foreground text-background hover:bg-foreground"
                  )}
                >
                  {w?.san}
                </td>
                <td
                  ref={currentPly === bPly ? activeRef : undefined}
                  onClick={() => b && onSelect(bPly)}
                  className={cn(
                    "py-1 cursor-pointer rounded px-1 hover:bg-secondary",
                    currentPly === bPly && "bg-foreground text-background hover:bg-foreground"
                  )}
                >
                  {b?.san || ""}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </ScrollArea>
  );
});
ReplayMoveList.displayName = "ReplayMoveList";
