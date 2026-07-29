import Reveal from "./Reveal";

export default function Download() {
  return (
    <section className="section section--panel" id="download">
      <div className="container">
        <Reveal className="section__head">
          <h2>Get Clip Yourself</h2>
          <p className="section__sub">
            Free. No account, no cloud, no catch.
          </p>
        </Reveal>
        <div className="download-grid download-grid--single">
          <Reveal delay={60}>
            <article className="card download-card">
              <span className="download-card__icon" aria-hidden="true">
                🖥
              </span>
              <h3>Windows desktop</h3>
              <p>
                The full experience: global hotkey, drawers &amp; reels, audio
                waveforms, smart dedup, and the drag &amp; drop shelf. It
                captures everything you copy — in your browser too.
              </p>
              <a
                className="btn btn--primary"
                href="/downloads/ClipYourself-1.0.0-win-x64.msi"
              >
                ⬇ Download .msi
              </a>
              <p className="download-card__meta">Windows 10/11 · x64 · free</p>
              <p className="download-card__note">
                Self-contained — no .NET install needed. Prefer building from
                source?{" "}
                <a
                  href="https://github.com/rangothedog/clip-yourself"
                  target="_blank"
                  rel="noreferrer"
                >
                  It&rsquo;s on GitHub
                </a>
                .
              </p>
            </article>
          </Reveal>
        </div>
      </div>
    </section>
  );
}
