import type { Metadata } from "next";
import Link from "next/link";

const title = "Screenshot Tool With OCR for Windows";
const description =
  "Snapboard is a Windows screenshot tool with OCR, blur, annotation, color picker, and pixel ruler for faster documentation and support workflows.";

export const metadata: Metadata = {
  title,
  description,
  keywords: [
    "screenshot tool with OCR",
    "OCR screenshot Windows",
    "extract text from screenshot",
    "Windows OCR screenshot app",
  ],
  alternates: {
    canonical: "/screenshot-tool-with-ocr",
  },
};

export default function ScreenshotToolWithOcrPage() {
  return (
    <section className="section">
      <div className="container">
        <h1>{title}</h1>
        <p className="hero-subtitle">{description}</p>

        <div className="grid two" style={{ marginTop: "1rem" }}>
          <article className="card">
            <h2>OCR workflow in Snapboard</h2>
            <ol className="check-list">
              <li>Press OCR hotkey and select a screen region</li>
              <li>Snapboard extracts text using Windows OCR</li>
              <li>Copy recognized text into docs, tickets, or chat</li>
            </ol>
          </article>
          <article className="card">
            <h2>Why OCR matters in screenshot tools</h2>
            <p>
              OCR reduces manual typing for logs, errors, and UI text. It helps QA, support, and engineering teams
              move faster when sharing technical context.
            </p>
          </article>
        </div>

        <p className="section-footnote">
          Looking for full feature comparisons? Visit <Link href="/compare">Snapboard vs alternatives</Link>.
        </p>
      </div>
    </section>
  );
}
