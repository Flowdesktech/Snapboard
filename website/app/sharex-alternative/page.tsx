import type { Metadata } from "next";
import Link from "next/link";

const title = "Simple ShareX Alternative for Daily Work";
const description =
  "Snapboard is a focused ShareX alternative with region, window, and scrolling capture, pin-to-screen, blur, OCR, color picker, and pixel ruler — all in a clean dark UI with no setup.";

export const metadata: Metadata = {
  title,
  description,
  keywords: ["ShareX alternative", "simple ShareX alternative", "fast screenshot workflow", "pin to screen ShareX"],
  alternates: {
    canonical: "/sharex-alternative",
  },
};

export default function SharexAlternativePage() {
  return (
    <section className="section">
      <div className="container">
        <h1>{title}</h1>
        <p className="hero-subtitle">{description}</p>

        <div className="grid two" style={{ marginTop: "1rem" }}>
          <article className="card">
            <h2>When Snapboard is a better fit</h2>
            <ul className="check-list">
              <li>You need quick screenshot workflows with less setup overhead</li>
              <li>
                You want <strong>pin-to-screen</strong> — the killer feature ShareX doesn&apos;t ship
              </li>
              <li>You want region, window, and PicPick-style scrolling capture in one tool</li>
              <li>You want blur, OCR, QR / barcode scan, color picker, and ruler in one compact app</li>
              <li>You prefer a straightforward dark UI for non-technical teammates</li>
            </ul>
          </article>
          <article className="card">
            <h2>When ShareX may still be better</h2>
            <p>
              If you need deep FTP/Imgur/Dropbox uploaders, screen recording, automation pipelines, advanced
              post-processing, and highly custom workflows, ShareX remains a great power-user option.
            </p>
          </article>
        </div>

        <div className="table-wrap" style={{ marginTop: "1rem" }}>
          <table>
            <thead>
              <tr>
                <th>Feature</th>
                <th>Snapboard</th>
                <th>ShareX</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td>Region capture + annotation</td>
                <td>Yes</td>
                <td>Yes</td>
              </tr>
              <tr>
                <td>Window capture (dropdown &rarr; clipboard + save dialog)</td>
                <td>Yes</td>
                <td>Yes</td>
              </tr>
              <tr>
                <td>Scrolling capture (auto-scroll + auto-stitch)</td>
                <td>Yes</td>
                <td>Yes</td>
              </tr>
              <tr>
                <td>Pin screenshot to screen</td>
                <td>Yes</td>
                <td>No</td>
              </tr>
              <tr>
                <td>Reverse image search (Google / Bing)</td>
                <td>Yes (built-in)</td>
                <td>No (external workflow)</td>
              </tr>
              <tr>
                <td>OCR on selection</td>
                <td>Yes</td>
                <td>Yes</td>
              </tr>
              <tr>
                <td>QR / barcode scan on selection</td>
                <td>Yes (built-in)</td>
                <td>No</td>
              </tr>
              <tr>
                <td>Blur sensitive data</td>
                <td>Yes</td>
                <td>Yes</td>
              </tr>
              <tr>
                <td>Color picker + pixel ruler</td>
                <td>Yes</td>
                <td>Yes</td>
              </tr>
              <tr>
                <td>Dark-themed native UI</td>
                <td>Yes</td>
                <td>Partial</td>
              </tr>
              <tr>
                <td>Setup complexity</td>
                <td>None</td>
                <td>High (workflows, destinations)</td>
              </tr>
              <tr>
                <td>Open-source</td>
                <td>Yes (MIT)</td>
                <td>Yes (GPL)</td>
              </tr>
            </tbody>
          </table>
        </div>

        <p className="section-footnote">
          Explore feature-by-feature details on <Link href="/compare">the comparison page</Link>.
        </p>
      </div>
    </section>
  );
}
