import Reveal from "./Reveal";

export default function ReelShowcase() {
  return (
    <section className="section section--panel" id="reel">
      <div className="container">
        <Reveal className="section__head">
          <h2>Open a drawer, get a reel</h2>
          <p className="section__sub">
            Drawers keep projects apart; the reel view lays a drawer out like a
            strip of film — scroll the takes, pick the one you want.
          </p>
        </Reveal>
        <Reveal delay={100}>
          <div className="film-strip">
            <div className="film-strip__holes" aria-hidden="true" />
            <div className="film-strip__frames">
              <figure className="film-frame">
                <img
                  src="/screenshots/sidebar.svg"
                  alt="Screenshot placeholder — the desktop sidebar docked to the screen edge"
                  loading="lazy"
                />
                <figcaption>Windows sidebar, docked to the edge</figcaption>
              </figure>
              <figure className="film-frame">
                <img
                  src="/screenshots/reel.svg"
                  alt="Screenshot placeholder — a drawer opened as a scrollable movie reel"
                  loading="lazy"
                />
                <figcaption>A drawer, unrolled as a reel</figcaption>
              </figure>
              <figure className="film-frame">
                <img
                  src="/screenshots/search.svg"
                  alt="Screenshot placeholder — searching across every drawer with pinned clips on top"
                  loading="lazy"
                />
                <figcaption>Search every drawer, pin what matters</figcaption>
              </figure>
            </div>
            <div className="film-strip__holes" aria-hidden="true" />
          </div>
        </Reveal>
      </div>
    </section>
  );
}
