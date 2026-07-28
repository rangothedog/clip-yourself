import Reveal from "./Reveal";

export default function DawTeaser() {
  return (
    <section className="section" id="daw-bridge">
      <div className="container">
        <Reveal>
          <div className="card daw-card">
            <p className="pill pill--soon">Coming soon</p>
            <h2>DAW Bridge</h2>
            <p className="section__sub daw-card__sub">
              Capture audio out of <strong>Audacity</strong>,{" "}
              <strong>REAPER</strong>, <strong>Pro Tools</strong> &amp; friends
              — even though their clipboards never touch the OS. DAW Bridge
              watches your export folders and ships scripting companions for
              the DAWs that support them, so a bounce or an export becomes a
              clip the moment it hits disk.
            </p>
            <div className="daw-card__chips" aria-label="Planned integrations">
              <span className="chip">Audacity</span>
              <span className="chip">REAPER</span>
              <span className="chip">Pro Tools</span>
              <span className="chip">Export watching</span>
              <span className="chip">Scripting companions</span>
            </div>
          </div>
        </Reveal>
      </div>
    </section>
  );
}
