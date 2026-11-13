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
        can capture it. That&rsquo;s what <strong>DAW Bridge</strong> is for:
        in Settings, watch the folder your DAW exports to, and every bounce or
        export is clipped automatically the moment it finishes writing. Drag
        &amp; drop onto the shelf works too.
      </p>
    ),
  },
  {
    q: "Why does 🎯 type the text instead of pasting it?",
    a: (
      <p>
        Typing works where pasting doesn&rsquo;t. Plenty of forms block paste
        (confirm-your-email boxes, some bank and shipping sites), and remote
        desktops or terminals often mangle it. Typing also leaves your
        clipboard alone, so whatever you copied last is still there. Drag the
        🎯 button at the top of the sidebar onto any text field, then let go.
        If a long clip is still typing, <kbd>Esc</kbd> stops it.
      </p>
    ),
  },
  {
    q: "Can I drag a clip straight into a text box?",
    a: (
      <p>
        Yes. Drag any text clip out of the sidebar and hover a text field in
        any app. It lights up with <strong>📋 Paste here</strong>, and letting
        go pastes the clip into it (it also becomes your clipboard, like any
        paste). Hold <kbd>Shift</kbd> while dropping to have it typed in
        keystroke by keystroke instead, for fields that block paste. Press{" "}
        <kbd>Esc</kbd> mid-drag to call it off. Dropping onto Explorer or the
        desktop still saves the clip as a file.
      </p>
    ),
  },
  {
    q: "Is there a browser extension?",
    a: (
      <p>
        No — and you don&rsquo;t need one. Anything you copy in your browser
        goes through the Windows clipboard, so the desktop sidebar captures it
        already. (We prototyped a Chrome side panel, but browsers can&rsquo;t
        see the OS clipboard, so it could never keep up with the desktop app.)
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
