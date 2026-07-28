/**
 * Decorative CSS/JSX recreation of the Clip Yourself desktop sidebar.
 * Not functional — purely a hero illustration.
 */

const WAVEFORM_HEIGHTS = [
  6, 10, 16, 22, 14, 26, 30, 20, 12, 24, 32, 18, 10, 22, 28, 34, 24, 14, 20,
  28, 16, 10, 14, 8,
];

function Waveform() {
  return (
    <div className="mock-wave" aria-hidden="true">
      {WAVEFORM_HEIGHTS.map((h, i) => (
        <span
          key={i}
          className="mock-wave__bar"
          style={{ height: `${h}px`, animationDelay: `${i * 70}ms` }}
        />
      ))}
    </div>
  );
}

export default function SidebarMockup() {
  return (
    <div className="mock" role="img" aria-label="Illustration of the Clip Yourself sidebar showing text, image, and audio clips plus drawers">
      <div className="mock__glow" aria-hidden="true" />
      <div className="mock__panel" aria-hidden="true">
        {/* Header */}
        <div className="mock__header">
          <span className="mock__title">📋 Clip Yourself</span>
          <span className="mock__header-actions">
            <span className="mock__hbtn">⚙</span>
            <span className="mock__hbtn">—</span>
          </span>
        </div>

        {/* Session bar */}
        <div className="mock__session">
          <span className="mock__session-dot" />
          <span>Session drawer · today 14:02</span>
          <span className="mock__session-count">3 clips</span>
        </div>

        {/* Clip cards */}
        <div className="mock__clips">
          {/* Text clip (pinned) */}
          <div className="mock-clip">
            <div className="mock-clip__top">
              <span className="mock-clip__kind">TXT</span>
              <span className="mock-clip__time">just now</span>
              <span className="mock-clip__pin">📌</span>
            </div>
            <p className="mock-clip__text">
              Ship the waveform player, then wire the drawer reel to the new
              store…
            </p>
          </div>

          {/* Image clip */}
          <div className="mock-clip">
            <div className="mock-clip__top">
              <span className="mock-clip__kind mock-clip__kind--img">IMG</span>
              <span className="mock-clip__time">2 min ago</span>
            </div>
            <div className="mock-clip__imgrow">
              <span className="mock-clip__thumb" />
              <span className="mock-clip__meta">
                sidebar-mock.png
                <br />
                1280 × 800 · 412 KB
              </span>
            </div>
          </div>

          {/* Audio clip with animated waveform */}
          <div className="mock-clip mock-clip--audio">
            <div className="mock-clip__top">
              <span className="mock-clip__kind mock-clip__kind--audio">AUD</span>
              <span className="mock-clip__time">5 min ago</span>
            </div>
            <div className="mock-clip__audiorow">
              <span className="mock-clip__play">▶</span>
              <Waveform />
            </div>
            <div className="mock-clip__meta">guitar-take3.wav · 0:07</div>
          </div>
        </div>

        {/* Drawers */}
        <div className="mock__drawers">
          <div className="mock__drawers-label">DRAWERS</div>
          <div className="mock-drawer">
            <span className="mock-drawer__icon">🎬</span>
            <span className="mock-drawer__name">Podcast — ep.12</span>
            <span className="mock-drawer__count">32</span>
          </div>
          <div className="mock-drawer">
            <span className="mock-drawer__icon">🎬</span>
            <span className="mock-drawer__name">Client mockups</span>
            <span className="mock-drawer__count">11</span>
          </div>
        </div>
      </div>
    </div>
  );
}
