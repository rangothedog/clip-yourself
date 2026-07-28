import Reveal from "./Reveal";

const BARS = [
  8, 14, 22, 30, 20, 34, 40, 28, 16, 32, 42, 24, 12, 28, 38, 44, 32, 18, 26,
  36, 22, 14, 18, 10,
];

export default function AudioSection() {
  return (
    <section className="section audio" id="audio">
      <div className="container audio__inner">
        <Reveal className="audio__copy">
          <p className="pill">🎵 The signature feature</p>
          <h2 className="audio__title">
            The clipboard manager that <span className="accent">understands audio</span>
          </h2>
          <p className="section__sub">
            Copy a sound and it lands in the sidebar with a waveform preview
            and a tiny player. Scrub it, replay it, then drag it out into any
            app — an editor, a chat window, a folder — and it arrives as a real
            audio file.
          </p>
          <ul className="audio__list">
            <li>Waveform preview rendered right in the clip row</li>
            <li>One-tap play/pause without leaving the sidebar</li>
            <li>Drag a take straight into Explorer, Slack, or your editor</li>
          </ul>
          <p className="audio__honest">
            Honest footnote: most DAWs keep copied audio on an internal
            clipboard the OS never sees — which is exactly why DAW Bridge is on
            the way.
          </p>
        </Reveal>
        <Reveal className="audio__demo" delay={120}>
          <div className="audio-player card" aria-hidden="true">
            <div className="audio-player__row">
              <span className="audio-player__play">▶</span>
              <div className="mock-wave mock-wave--big">
                {BARS.map((h, i) => (
                  <span
                    key={i}
                    className="mock-wave__bar"
                    style={{ height: `${h}px`, animationDelay: `${i * 90}ms` }}
                  />
                ))}
              </div>
            </div>
            <div className="audio-player__meta">
              <span>vocal-take-07.wav</span>
              <span>0:04 / 0:07</span>
            </div>
          </div>
        </Reveal>
      </div>
    </section>
  );
}
