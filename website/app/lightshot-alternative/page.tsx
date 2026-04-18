import type { Metadata } from "next";
import Link from "next/link";

const title = "Best Lightshot Alternative for Windows";
const description =
  "Snapboard is a privacy-first Lightshot alternative with region, window, and scrolling capture, pin-to-screen, blur, OCR, color picker, and pixel ruler — built for modern Windows workflows.";

export const metadata: Metadata = {
  title,
  description,
  keywords: [
    "Lightshot alternative",
    "best Lightshot alternative",
    "screenshot tool for Windows",
    "private screenshot app",
  ],
  alternates: {
    canonical: "/lightshot-alternative",
  },
};

const faqSchema = {
  "@context": "https://schema.org",
  "@type": "FAQPage",
  mainEntity: [
    {
      "@type": "Question",
      name: "Why use Snapboard instead of Lightshot?",
      acceptedAnswer: {
        "@type": "Answer",
        text: "Lightshot only does region capture. Snapboard adds window capture, scrolling capture, pin-to-screen, privacy blur, OCR, color picker, and a pixel ruler — while keeping a fast select-and-annotate workflow.",
      },
    },
    {
      "@type": "Question",
      name: "Is Snapboard free like Lightshot?",
      acceptedAnswer: {
        "@type": "Answer",
        text: "Yes. Snapboard is free and open-source under the MIT license.",
      },
    },
  ],
};

export default function LightshotAlternativePage() {
  return (
    <section className="section">
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(faqSchema) }} />
      <div className="container">
        <h1>{title}</h1>
        <p className="hero-subtitle">{description}</p>

        <div className="grid two" style={{ marginTop: "1rem" }}>
          <article className="card">
            <h2>What Lightshot users usually need next</h2>
            <p>
              Lightshot is quick, but many teams eventually need stronger privacy and utility features. Snapboard is
              designed for that upgrade path without adding complexity.
            </p>
          </article>
          <article className="card">
            <h2>What Snapboard adds</h2>
            <ul className="check-list">
              <li>Window capture — pick any open app from a dropdown, then clipboard + save dialog in one click</li>
              <li>PicPick-style scrolling capture — Snapboard auto-scrolls the inner content child and stitches long pages for you, without any window chrome</li>
              <li>Pin any screenshot to screen as a floating reference</li>
              <li>Blur/pixelate sensitive information</li>
              <li>OCR text extraction from selected regions</li>
              <li>QR &amp; barcode scan from any selected region (offline, via ZXing.Net)</li>
              <li>Reverse image search on Google and Bing</li>
              <li>Color picker and pixel ruler utilities</li>
              <li>Silent auto-update from GitHub Releases</li>
              <li>Offline-first, open-source, MIT-licensed</li>
            </ul>
          </article>
        </div>

        <div className="table-wrap" style={{ marginTop: "1rem" }}>
          <table>
            <thead>
              <tr>
                <th>Feature</th>
                <th>Snapboard</th>
                <th>Lightshot</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td>Region capture and annotation</td>
                <td>Yes</td>
                <td>Yes</td>
              </tr>
              <tr>
                <td>Window capture (dropdown &rarr; clipboard + save dialog)</td>
                <td>Yes</td>
                <td>No</td>
              </tr>
              <tr>
                <td>Scrolling capture (auto-scroll + auto-stitch)</td>
                <td>Yes</td>
                <td>No</td>
              </tr>
              <tr>
                <td>Pin screenshot to screen</td>
                <td>Yes</td>
                <td>No</td>
              </tr>
              <tr>
                <td>Reverse image search (Google / Bing)</td>
                <td>Yes</td>
                <td>Partial (Google only, cloud upload)</td>
              </tr>
              <tr>
                <td>Blur sensitive data</td>
                <td>Yes</td>
                <td>No</td>
              </tr>
              <tr>
                <td>OCR on selection</td>
                <td>Yes</td>
                <td>No</td>
              </tr>
              <tr>
                <td>QR / barcode scan on selection</td>
                <td>Yes</td>
                <td>No</td>
              </tr>
              <tr>
                <td>Color picker and ruler</td>
                <td>Yes</td>
                <td>No</td>
              </tr>
              <tr>
                <td>100% offline, no account</td>
                <td>Yes</td>
                <td>No</td>
              </tr>
              <tr>
                <td>Open-source</td>
                <td>Yes (MIT)</td>
                <td>No</td>
              </tr>
            </tbody>
          </table>
        </div>

        <p className="section-footnote">
          Compare all options on <Link href="/compare">the full comparison page</Link> or read practical answers in{" "}
          <Link href="/faq">FAQ</Link>.
        </p>
      </div>
    </section>
  );
}
