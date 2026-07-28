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
                  src="/screenshots/sidebar.png"
                  alt="The desktop sidebar docked to the screen edge with text, image, and audio clips"
                  loading="lazy"
                />
                <figcaption>Windows sidebar, docked to the edge</figcaption>
              </figure>
              <figure className="film-frame">
                <img
                  src="/screenshots/reel.png"
                  alt="A drawer opened as a scrollable movie reel of clips"
                  loading="lazy"
                />
                <figcaption>A drawer, unrolled as a reel</figcaption>
              </figure>
              <figure className="film-frame">
                <img
                  src="/screenshots/search.png"
                  alt="Searching across every drawer with pinned clips on top"
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
