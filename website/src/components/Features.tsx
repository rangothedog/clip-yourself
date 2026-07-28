import Reveal from "./Reveal";

type Feature = {
  icon: string;
  title: string;
  body: string;
};

const FEATURES: Feature[] = [
  {
    icon: "⚡",
    title: "Hotkey sidebar",
    body: "Ctrl+Alt+V slides the sidebar out of your screen edge in any app. Copy, glance, paste — no window juggling.",
  },
  {
    icon: "🗂",
    title: "Drawers & the reel",
    body: "Every session starts a fresh drawer. Create, rename, and delete drawers per project — open one and it unrolls as a scrollable movie reel.",
  },
  {
    icon: "🎵",
    title: "Audio with waveforms",
    body: "Copied sound becomes a clip with a tiny waveform player. Preview it in place, then drag it into any app that takes audio.",
  },
  {
    icon: "🧠",
    title: "Smart dedup",
    body: "Copy the same thing twice and it never repeats — the existing clip just resurfaces at the top of the stack.",
  },
  {
    icon: "📎",
    title: "Drag & drop shelf",
    body: "Drop files or text onto the shelf without copying at all. Drag any clip out into Explorer or another app and it lands as real content.",
  },
  {
    icon: "🔒",
    title: "Local & private",
    body: "100% on your machine. Nothing is uploaded, ever. Keeping history between sessions is opt-in, with per-drawer size limits.",
  },
];

export default function Features() {
  return (
    <section className="section" id="features">
      <div className="container">
        <Reveal className="section__head">
          <h2>Everything you copy, one calm sidebar</h2>
          <p className="section__sub">
            Pin the important stuff (📌 pinned clips are never auto-evicted),
            search across every drawer, and cap each drawer by clip count or
            megabytes.
          </p>
        </Reveal>
        <div className="feature-grid">
          {FEATURES.map((f, i) => (
            <Reveal key={f.title} delay={i * 60}>
              <article className="card feature-card">
                <span className="feature-card__icon" aria-hidden="true">
                  {f.icon}
                </span>
                <h3>{f.title}</h3>
                <p>{f.body}</p>
              </article>
            </Reveal>
          ))}
        </div>
      </div>
    </section>
  );
}
