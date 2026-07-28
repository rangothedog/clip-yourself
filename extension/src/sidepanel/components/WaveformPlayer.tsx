import { useEffect, useRef, useState } from 'react';
import { formatSeconds } from '../util';

const BAR_COUNT = 90;
const ACCENT = '#4f8cff';
const IDLE_BAR = '#3a4150';

/**
 * Compact waveform audio player. Fetches + decodes the audio to draw ~90 peak
 * bars on a canvas; falls back to a plain <audio controls> if fetch/decode
 * fails (e.g. CORS). A failure never breaks the enclosing clip row.
 */
export function WaveformPlayer({ url }: { url: string }) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const audioRef = useRef<HTMLAudioElement | null>(null);
  const [peaks, setPeaks] = useState<number[] | null>(null);
  const [failed, setFailed] = useState(false);
  const [playing, setPlaying] = useState(false);
  const [time, setTime] = useState(0);
  const [duration, setDuration] = useState(0);

  // Decode audio and compute peak bars.
  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const response = await fetch(url);
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        const buffer = await response.arrayBuffer();
        const ctx = new AudioContext();
        try {
          const decoded = await ctx.decodeAudioData(buffer);
          if (cancelled) return;
          setDuration((d) => d || decoded.duration);
          const channel = decoded.getChannelData(0);
          const block = Math.max(1, Math.floor(channel.length / BAR_COUNT));
          const bars: number[] = [];
          for (let i = 0; i < BAR_COUNT; i++) {
            let peak = 0;
            const start = i * block;
            const end = Math.min(start + block, channel.length);
            for (let j = start; j < end; j += 16) {
              const v = Math.abs(channel[j]);
              if (v > peak) peak = v;
            }
            bars.push(peak);
          }
          const max = Math.max(...bars, 0.01);
          setPeaks(bars.map((p) => p / max));
        } finally {
          void ctx.close().catch(() => {});
        }
      } catch {
        if (!cancelled) setFailed(true);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [url]);

  // Playback element.
  useEffect(() => {
    const audio = new Audio(url);
    audio.preload = 'metadata';
    audioRef.current = audio;
    const onTime = () => setTime(audio.currentTime);
    const onMeta = () => setDuration((d) => d || audio.duration || 0);
    const onPlay = () => setPlaying(true);
    const onPause = () => setPlaying(false);
    audio.addEventListener('timeupdate', onTime);
    audio.addEventListener('loadedmetadata', onMeta);
    audio.addEventListener('play', onPlay);
    audio.addEventListener('pause', onPause);
    audio.addEventListener('ended', onPause);
    return () => {
      audio.pause();
      audio.removeEventListener('timeupdate', onTime);
      audio.removeEventListener('loadedmetadata', onMeta);
      audio.removeEventListener('play', onPlay);
      audio.removeEventListener('pause', onPause);
      audio.removeEventListener('ended', onPause);
      audioRef.current = null;
    };
  }, [url]);

  // Draw the bars with a progress overlay.
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas || !peaks) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    const barWidth = canvas.width / peaks.length;
    const progress = duration > 0 ? time / duration : 0;
    peaks.forEach((peak, i) => {
      ctx.fillStyle = (i + 0.5) / peaks.length <= progress ? ACCENT : IDLE_BAR;
      const h = Math.max(2, peak * (canvas.height - 4));
      ctx.fillRect(i * barWidth + 0.5, (canvas.height - h) / 2, Math.max(1, barWidth - 1.2), h);
    });
  }, [peaks, time, duration]);

  if (failed) {
    return (
      <audio
        className="audio-fallback"
        controls
        preload="none"
        src={url}
        onClick={(e) => e.stopPropagation()}
      />
    );
  }

  const toggle = () => {
    const audio = audioRef.current;
    if (!audio) return;
    if (playing) audio.pause();
    else void audio.play().catch(() => setFailed(true));
  };

  return (
    <div className="wave" onClick={(e) => e.stopPropagation()}>
      <button className="wave-btn" onClick={toggle} title={playing ? 'Pause' : 'Play'}>
        {playing ? '⏸' : '▶'}
      </button>
      <canvas ref={canvasRef} className="wave-canvas" width={200} height={34} />
      <span className="wave-time">
        {formatSeconds(time)} / {formatSeconds(duration)}
      </span>
    </div>
  );
}
