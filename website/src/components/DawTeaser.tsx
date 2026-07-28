import Reveal from "./Reveal";

export default function DawTeaser() {
  return (
    <section className="section" id="daw-bridge">
      <div className="container">
        <Reveal>
          <div className="card daw-card">
            <p className="pill pill--free">Built in</p>
            <h2>DAW Bridge</h2>
            <p className="section__sub daw-card__sub">
              Get audio out of <strong>Audacity</strong>,{" "}
              <strong>REAPER</strong>, <strong>Pro Tools</strong> &amp; friends
              — even though their clipboards never touch the OS. Point DAW
              Bridge at your export folders and every bounce or export becomes
              a clip the moment the DAW finishes writing it: waveform, player,
              ready to drag anywhere.
            </p>
            <div className="daw-card__chips" aria-label="How it works">
              <span className="chip">Watch any export folder</span>
              <span className="chip">Subfolders included</span>
              <span className="chip">Waits for the write to finish</span>
              <span className="chip">Audacity · REAPER · Pro Tools · any DAW</span>
            </div>
          </div>
        </Reveal>
      </div>
    </section>
  );
}
