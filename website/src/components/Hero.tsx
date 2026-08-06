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
            Clip Yourself is a Windows sidebar that keeps every copy — text,
            images, <strong>sound</strong>, and even <strong>video</strong> — as
            a living clip you can preview, pin, search, and drag straight back
            into any app. Drop files and folders from Explorer onto the shelf,
            and drag any clip right back out.
          </p>
          <div className="hero__ctas">
            <a
              className="btn btn--primary btn--big"
              href="/downloads/ClipYourself-1.0.3-win-x64.msi"
            >
              ⬇ Download for Windows (.msi)
            </a>
            <a className="btn btn--ghost btn--big" href="#features">
              See what it does
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
