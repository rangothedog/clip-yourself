import Reveal from "./Reveal";

export default function Download() {
  return (
    <section className="section section--panel" id="download">
      <div className="container">
        <Reveal className="section__head">
          <h2>Get Clip Yourself</h2>
          <p className="section__sub">
            Free on both platforms. No account, no cloud, no catch.
          </p>
        </Reveal>
        <div className="download-grid">
          <Reveal delay={60}>
            <article className="card download-card">
              <span className="download-card__icon" aria-hidden="true">
                🖥
              </span>
              <h3>Windows desktop</h3>
              <p>
                The full sidebar experience: global hotkey, drawers, audio
                waveforms, and the drag &amp; drop shelf.
              </p>
              <a
                className="btn btn--primary"
                href="/downloads/ClipYourself-0.1.0-win-x64.msi"
              >
                ⬇ Download .msi
              </a>
              <p className="download-card__meta">Windows 10/11 · x64 · free</p>
              <p className="download-card__note">
                Self-contained — no .NET install needed.
              </p>
            </article>
          </Reveal>
          <Reveal delay={140}>
            <article className="card download-card">
              <span className="download-card__icon" aria-hidden="true">
                🧩
              </span>
              <h3>Chrome extension</h3>
              <p>
                Clip Yourself as a Chrome side panel, for everything you copy
                in the browser. It isn&rsquo;t on the Web Store yet, so it
                loads unpacked:
              </p>
              <ol className="download-card__steps">
                <li>
                  Grab the extension from{" "}
                  <a
                    href="https://github.com/rangothedog/clip-yourself"
                    target="_blank"
                    rel="noreferrer"
                  >
                    GitHub
                  </a>{" "}
                  and build it (<code>npm install &amp;&amp; npm run build</code>).
                </li>
                <li>
                  Open <code>chrome://extensions</code> and switch on{" "}
                  <strong>Developer mode</strong>.
                </li>
                <li>
                  Click <strong>Load unpacked</strong> and pick the extension&rsquo;s{" "}
                  <code>dist</code> folder.
                </li>
              </ol>
              <a
                className="btn btn--ghost"
                href="https://github.com/rangothedog/clip-yourself"
                target="_blank"
                rel="noreferrer"
              >
                View on GitHub
              </a>
              <p className="download-card__meta">Chrome side panel · free</p>
            </article>
          </Reveal>
        </div>
      </div>
    </section>
  );
}
