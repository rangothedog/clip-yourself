import Reveal from "./Reveal";
import SidebarMockup from "./SidebarMockup";

export default function Hero() {
  return (
    <section className="hero" id="top">
      <div className="container hero__inner">
        <Reveal className="hero__copy">
          <p className="pill pill--free">Free. No account. No cloud.</p>
          <h1 className="hero__title">
            Your clipboard, <span className="accent">remembered.</span>
          </h1>
          <p className="hero__sub">
            Clip Yourself is a sidebar for Windows and a side panel for Chrome
            that keeps every copy — text, images, and <strong>sound</strong> —
            as a living clip you can preview, pin, search, and drag straight
            back into any app.
          </p>
          <div className="hero__ctas">
            <a
              className="btn btn--primary btn--big"
              href="/downloads/ClipYourself-0.1.0-win-x64.msi"
            >
              ⬇ Download for Windows (.msi)
            </a>
            <a className="btn btn--ghost btn--big" href="#download">
              Add to Chrome
            </a>
          </div>
          <p className="hero__hint">
            Windows 10/11 · x64 · summon it anywhere with{" "}
            <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>V</kbd>
          </p>
        </Reveal>
        <Reveal className="hero__mock" delay={120}>
          <SidebarMockup />
        </Reveal>
      </div>
    </section>
  );
}
