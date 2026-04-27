/* eslint-disable @typescript-eslint/no-explicit-any */
import { useChessStore } from "@/store/chessStore";
import { Card } from "@/components/ui/card";
import { Crown } from "lucide-react";

export const GamePanel = () => {
  const whiteName = useChessStore((s) => s.whiteName);
  const blackName = useChessStore((s) => s.blackName);
  const chess = useChessStore((s) => s.chess);
  const fen = useChessStore((s) => s.fen);
  const status = useChessStore((s) => s.status);
  const resultText = useChessStore((s) => s.resultText);

  // recompute on fen change
  void fen;
  const turn = chess.turn();
  const inCheck = chess.inCheck();
  const moveNo = Math.floor(chess.history().length / 2) + 1;

  // captured pieces
  const initial = { p: 8, n: 2, b: 2, r: 2, q: 1 } as Record<string, number>;
  const counts = { w: { p: 0, n: 0, b: 0, r: 0, q: 0 }, b: { p: 0, n: 0, b: 0, r: 0, q: 0 } } as any;
  chess.board().forEach((row) =>
    row.forEach((sq) => {
      if (sq && sq.type !== "k") counts[sq.color][sq.type]++;
    })
  );
  
  const captured = (color: "w" | "b") => {
    const arr: string[] = [];
    (["q", "r", "b", "n", "p"] as const).forEach((t) => {
      const missing = initial[t] - counts[color][t];
      for (let i = 0; i < missing; i++) arr.push(symbolFor(color, t));
    });
    return arr;
  };

  return (
    <Card className="paper-card p-3 md:p-4 space-y-3 md:space-y-4 shrink-0">
      <PlayerRow
        name={blackName}
        color="b"
        active={status === "playing" && turn === "b"}
        captured={captured("w")}
      />
      <div className="border-t border-border" />
      <PlayerRow
        name={whiteName}
        color="w"
        active={status === "playing" && turn === "w"}
        captured={captured("b")}
      />

      <div className="rounded-md bg-secondary/60 px-3 py-2 text-sm">
        {status === "finished" && resultText ? (
          <span className="font-display text-base font-semibold text-accent">
            {resultText}
          </span>
        ) : status === "playing" ? (
          <span className="block truncate">
            Move <strong>{moveNo}</strong> ·{" "}
            <strong>{turn === "w" ? whiteName : blackName}</strong> to play
            {inCheck && <span className="ml-2 text-destructive font-semibold">CHECK</span>}
          </span>
        ) : (
          <span className="text-muted-foreground">No game in progress.</span>
        )}
      </div>
    </Card>
  );
};

const PlayerRow = ({
  name,
  color,
  active,
  captured,
}: {
  name: string;
  color: "w" | "b";
  active: boolean;
  captured: string[];
}) => (
  <div className="flex items-start justify-between gap-2 md:gap-3">
    <div className="flex items-center gap-2 min-w-0 flex-1">
      <div
        className={`h-8 w-8 md:h-9 md:w-9 rounded-full flex items-center justify-center text-sm font-semibold shrink-0 ${
          color === "w"
            ? "bg-background text-foreground border border-border"
            : "bg-foreground text-background"
        } ${active ? "ring-2 ring-accent" : ""}`}
      >
        {color === "w" ? "♙" : "♟"}
      </div>
      <div className="min-w-0 flex-1">
        <div className="font-display text-sm md:text-base font-semibold truncate flex items-center gap-1">
          {name}
          {active && <Crown className="h-3 w-3 md:h-3.5 md:w-3.5 text-accent shrink-0" />}
        </div>
        <div className="text-xs text-muted-foreground">
          {color === "w" ? "White" : "Black"}
        </div>
      </div>
    </div>
    <div className="text-base md:text-lg leading-none text-muted-foreground tracking-tight max-w-[8rem] md:max-w-[10rem] text-right font-mono">
      {captured.length > 0 ? captured.join("") : <span className="text-xs">—</span>}
    </div>
  </div>
);

const symbolFor = (color: "w" | "b", type: "p" | "n" | "b" | "r" | "q") => {
  const map: Record<string, [string, string]> = {
    p: ["♙", "♟"],
    n: ["♘", "♞"],
    b: ["♗", "♝"],
    r: ["♖", "♜"],
    q: ["♕", "♛"],
  };
  return color === "w" ? map[type][0] : map[type][1];
};