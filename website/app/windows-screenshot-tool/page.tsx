import type { Metadata } from "next";
import Link from "next/link";

const title = "Best Windows Screenshot Tool for Fast Daily Work";
const description =
  "Snapboard is a fast Windows screenshot tool for region, window, and scrolling capture, annotate, blur, OCR, QR & barcode scan, color picking, and pixel measuring in one privacy-first app.";

export const metadata: Metadata = {
  title,
  description,
  keywords: [
    "Windows screenshot tool",
    "best screenshot tool for Windows",
    "Windows 11 screenshot app",
    "Windows 10 screenshot app",
  ],
  alternates: {
    canonical: "/windows-screenshot-tool",
  },
};

export default function WindowsScreenshotToolPage() {
  return (
    <section className="section">
      <div className="container">
        <h1>{title}</h1>
        <p className="hero-subtitle">{description}</p>

        <div className="grid two" style={{ marginTop: "1rem" }}>
          <article className="card">
            <h2>What makes a great Windows screenshot app</h2>
            <ul className="check-list">
              <li>Fast region capture and clear annotation tools</li>
              <li>Blur for sensitive information before sharing</li>
              <li>Global hotkeys with conflict detection</li>
              <li>Reliable performance on Windows 10 and 11</li>
            </ul>
          </article>
          <article className="card">
            <h2>Why teams pick Snapboard</h2>
            <p>
              Snapboard balances speed and capability: it keeps everyday capture workflows simple
              while still giving you <strong>PicPick-style scrolling capture</strong>, OCR,{" "}
              <strong>QR &amp; barcode scan</strong>, pin-to-screen, a color picker, a pixel ruler,
              and <strong>silent auto-update from GitHub</strong> — all behind privacy-first defaults.
            </p>
          </article>
        </div>

        <p className="section-footnote">
          Compare alternatives on <Link href="/compare">the comparison page</Link> and read common questions in{" "}
          <Link href="/faq">FAQ</Link>.
        </p>
      </div>
    </section>
  );
}
