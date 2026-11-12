import Reveal from "./Reveal";

const TYPED = "1Z 999 AA1 01 2345 6784";

export default function TypeAnywhere() {
  return (
    <section className="section type-anywhere" id="type-anywhere">
      <div className="container type-anywhere__inner">
        <Reveal className="type-anywhere__demo">
          <div className="type-demo card" aria-hidden="true">
            <div className="type-demo__titlebar">
              <span className="type-demo__dot" />
              <span className="type-demo__dot" />
              <span className="type-demo__dot" />
              <span className="type-demo__title">Return request</span>
            </div>
            <div className="type-demo__body">
              <span className="type-demo__label">Order number</span>
              <div className="type-demo__field type-demo__field--idle">#48213</div>

              <span className="type-demo__label">Tracking number</span>
              <div className="type-demo__target">
                <span className="type-demo__tag">⌨ Type here</span>
                <span className="type-demo__corner type-demo__corner--tl" />
                <span className="type-demo__corner type-demo__corner--tr" />
                <span className="type-demo__corner type-demo__corner--bl" />
                <span className="type-demo__corner type-demo__corner--br" />
                <div className="type-demo__field type-demo__field--live">
                  <span
                    className="type-demo__typed"
                    style={{ ["--chars" as string]: TYPED.length }}
                  >
                    {TYPED}
                  </span>
                </div>
                <span className="type-demo__picker">🎯</span>
              </div>
              <p className="type-demo__note">Paste is disabled for this field</p>
            </div>
          </div>
        </Reveal>

        <Reveal className="type-anywhere__copy" delay={120}>
          <p className="pill">🎯 New in 1.0.6</p>
          <h2 className="type-anywhere__title">
            Drag, drop, and <span className="accent">it types itself</span>
          </h2>
          <p className="section__sub">
            Grab the 🎯 target at the top of the sidebar (or of any open
            drawer) and drag it onto any text box in any app. A focus reticle locks onto the field under your
            cursor. Let go, and Clip Yourself types your clip into it, one
            keystroke at a time.
          </p>
          <ul className="type-anywhere__list">
            <li>Works in forms, chat boxes, editors, and memo fields across Windows</li>
            <li>
              Types instead of pasting, so it gets past &ldquo;no paste&rdquo;
              fields and leaves your clipboard alone
            </li>
            <li>Select several text clips and they&rsquo;re typed in order, one per line</li>
          </ul>
          <p className="type-anywhere__honest">
            Fine print: Windows won&rsquo;t let a regular app type into programs
            running as administrator, and <kbd>Esc</kbd> stops a long clip
            mid-type.
          </p>
        </Reveal>
      </div>
    </section>
  );
}
