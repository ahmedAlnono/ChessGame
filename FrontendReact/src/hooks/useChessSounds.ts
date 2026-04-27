import { useCallback, useRef } from "react";

type SoundKind = "move" | "capture" | "check" | "checkmate" | "castle";

/**
 * Lightweight WebAudio-based SFX. No assets required; tones are synthesized.
 * Replace `play` internals with HTMLAudioElement if you ship .mp3/.ogg files.
 */
export const useChessSounds = (enabled = true) => {
  const ctxRef = useRef<AudioContext | null>(null);

  const ensureCtx = () => {
    if (typeof window === "undefined") return null;
    if (!ctxRef.current) {
      const Ctor = window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
      if (!Ctor) return null;
      ctxRef.current = new Ctor();
    }
    return ctxRef.current;
  };

  const tone = (freq: number, duration = 0.08, type: OscillatorType = "triangle", gain = 0.07) => {
    const ctx = ensureCtx();
    if (!ctx) return;
    const osc = ctx.createOscillator();
    const g = ctx.createGain();
    osc.type = type;
    osc.frequency.value = freq;
    g.gain.setValueAtTime(gain, ctx.currentTime);
    g.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + duration);
    osc.connect(g).connect(ctx.destination);
    osc.start();
    osc.stop(ctx.currentTime + duration);
  };

  const play = useCallback((kind: SoundKind) => {
    if (!enabled) return;
    switch (kind) {
      case "move":      tone(380, 0.06, "triangle", 0.05); break;
      case "capture":   tone(180, 0.12, "sawtooth", 0.08); break;
      case "castle":    tone(300, 0.06); setTimeout(() => tone(420, 0.06), 70); break;
      case "check":     tone(720, 0.10, "square", 0.05); break;
      case "checkmate":
        tone(520, 0.18, "square", 0.08);
        setTimeout(() => tone(380, 0.18, "square", 0.08), 160);
        setTimeout(() => tone(220, 0.30, "sawtooth", 0.09), 320);
        break;
    }
  }, [enabled]);

  return play;
};
