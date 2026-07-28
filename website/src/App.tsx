import Nav from "./components/Nav";
import Hero from "./components/Hero";
import Features from "./components/Features";
import ReelShowcase from "./components/ReelShowcase";
import AudioSection from "./components/AudioSection";
import DawTeaser from "./components/DawTeaser";
import Download from "./components/Download";
import Faq from "./components/Faq";
import Footer from "./components/Footer";

export default function App() {
  return (
    <>
      <Nav />
      <main>
        <Hero />
        <Features />
        <ReelShowcase />
        <AudioSection />
        <DawTeaser />
        <Download />
        <Faq />
      </main>
      <Footer />
    </>
  );
}
