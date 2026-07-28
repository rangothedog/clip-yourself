export default function Nav() {
  return (
    <header className="nav">
      <div className="container nav__inner">
        <a className="nav__brand" href="#top" aria-label="Clip Yourself — home">
          <span className="nav__logo" aria-hidden="true">
            📋
          </span>
          <span className="nav__name">Clip Yourself</span>
        </a>
        <nav className="nav__links" aria-label="Main">
          <a href="#features">Features</a>
          <a href="#audio">Audio</a>
          <a href="#download">Download</a>
          <a href="#faq">FAQ</a>
          <a
            href="https://github.com/rangothedog/clip-yourself"
            target="_blank"
            rel="noreferrer"
          >
            GitHub
          </a>
        </nav>
        <a className="btn btn--small btn--primary nav__cta" href="#download">
          Get it free
        </a>
      </div>
    </header>
  );
}
