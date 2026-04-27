// src/components/MoveList.tsx
import { useChessStore } from "@/store/chessStore";

export const MoveList = () => {
  const moveHistory = useChessStore((s) => s.moveHistory);

  return (
    <div className="flex-1 overflow-y-auto">
      <h3 className="font-semibold mb-2">Moves</h3>
      <div className="space-y-1">
        {moveHistory.length === 0 ? (
          <p className="text-sm text-muted-foreground">No moves yet</p>
        ) : (
          moveHistory.map((move, index) => (
            <div key={index} className="text-sm font-mono">
              {index % 2 === 0 && (
                <span className="text-muted-foreground mr-2">
                  {Math.floor(index / 2) + 1}.
                </span>
              )}
              <span className={move.color === "w" ? "text-foreground" : "text-muted-foreground"}>
                {move.san}
              </span>
            </div>
          ))
        )}
      </div>
    </div>
  );
};