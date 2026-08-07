import Reveal from "./Reveal";

export default function Download() {
  const installerName = "ClipYourself-1.0.4-win-x64.msi";
  const portableName = "ClipYourself-1.0.4-win-x64-portable.zip";

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
              <div className="download-card__actions">
                <a className="btn btn--primary" href={`/downloads/${installerName}`}>
                  ⬇ Download .msi
                </a>
                <a className="btn btn--ghost" href={`/downloads/${portableName}`}>
                  ⬇ Download portable .zip
                </a>
                <a className="btn btn--ghost" href="/downloads/checksums.txt">
                  View checksums.txt
                </a>
              </div>
              <p className="download-card__meta">Windows 10/11 · x64 · free</p>
              <p className="download-card__note">
                Self-contained — no .NET install needed. You can also grab this
                build from the{" "}
                <a
                  href="https://github.com/rangothedog/clip-yourself/releases/tag/v1.0.4"
                  target="_blank"
                  rel="noreferrer"
                >
                  v1.0.4 release on GitHub
                </a>
                , or build from{" "}
                <a
                  href="https://github.com/rangothedog/clip-yourself"
                  target="_blank"
                  rel="noreferrer"
                >
                  source
                </a>
                .
              </p>
              <div className="download-verify" role="note" aria-label="Verify download in PowerShell">
                <h4>Verify your download (PowerShell)</h4>
                <pre>
                  <code>{`cd "$env:USERPROFILE\\Downloads"
Get-FileHash .\\${installerName} -Algorithm SHA256
Get-Content .\\checksums.txt`}</code>
                </pre>
                <p className="download-card__note">
                  The hash from <code>Get-FileHash</code> should exactly match the
                  corresponding line in <code>checksums.txt</code>.
                </p>
              </div>
            </article>
          </Reveal>
        </div>
      </div>
    </section>
  );
}
