import type { ReactNode } from "react";
import Reveal from "./Reveal";

type QA = {
  q: string;
  a: ReactNode;
};

const FAQS: QA[] = [
  {
    q: "Is it really free?",
    a: (
      <p>
        Yes. Clip Yourself is free — we&rsquo;re not selling it, there&rsquo;s
        no pro tier, no trial clock, and no account to create. It&rsquo;s a
        tool we wanted to exist.
      </p>
    ),
  },
  {
    q: "Where is my data?",
    a: (
      <p>
        On your machine and nowhere else, under{" "}
        <code>%LOCALAPPDATA%\ClipYourself</code>. Keeping clips between
        sessions is opt-in — and if you opt out, the stored data is deleted.
        Nothing is ever uploaded.
      </p>
    ),
  },
  {
    q: "What's the hotkey?",
    a: (
      <p>
        <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>V</kbd> toggles the sidebar from
        anywhere on Windows.
      </p>
    ),
  },
  {
    q: "Why doesn't copying in Audacity show up?",
    a: (
      <p>
        Most DAWs — Audacity included — keep copied audio on an internal
        clipboard that the operating system never sees, so no clipboard manager
        can capture it. For now, drag the audio out or export it and drop the
        file on the shelf. <strong>DAW Bridge</strong>, coming soon, will close
        this gap with export watching and scripting companions.
      </p>
    ),
  },
  {
    q: "Do the desktop app and Chrome extension sync?",
    a: (
      <p>
        Not yet. Today they&rsquo;re independent — the desktop app covers
        everything you copy on Windows, and the extension covers the browser.
      </p>
    ),
  },
  {
    q: "Is it open source?",
    a: (
      <p>
        Yes — the code lives at{" "}
        <a
          href="https://github.com/rangothedog/clip-yourself"
          target="_blank"
          rel="noreferrer"
        >
          github.com/rangothedog/clip-yourself
        </a>
        . Issues and pull requests welcome.
      </p>
    ),
  },
];

export default function Faq() {
  return (
    <section className="section" id="faq">
      <div className="container container--narrow">
        <Reveal className="section__head">
          <h2>Questions, answered</h2>
        </Reveal>
        <div className="faq-list">
          {FAQS.map((item, i) => (
            <Reveal key={item.q} delay={i * 40}>
              <details className="faq-item">
                <summary>{item.q}</summary>
                <div className="faq-item__body">{item.a}</div>
              </details>
            </Reveal>
          ))}
        </div>
      </div>
    </section>
  );
}
