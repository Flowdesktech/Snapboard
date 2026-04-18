import type { Metadata } from "next";
import Link from "next/link";

const title = "Best Greenshot Alternative for Windows";
const description =
  "Snapboard is a modern Greenshot alternative with region, window, and scrolling capture, pin-to-screen, OCR, blur, color picker, and pixel ruler in one dark-themed Windows app.";

export const metadata: Metadata = {
  title,
  description,
  keywords: [
    "Greenshot alternative",
    "best Greenshot alternative",
    "open source screenshot tool",
    "Windows screenshot app",
  ],
  alternates: {
    canonical: "/greenshot-alternative",
  },
};

export default function GreenshotAlternativePage() {
  return (
    <section className="section">
      <div className="container">
        <h1>{title}</h1>
        <p className="hero-subtitle">{description}</p>

        <div className="grid three" style={{ marginTop: "1rem" }}>
          <article className="card">
            <h2>Cleaner default experience</h2>
            <p>Snapboard keeps a focused UI so teams can capture, annotate, and move on quickly.</p>
          </article>
          <article className="card">
            <h2>More built-in utility tools</h2>
            <p>OCR, color picker, and pixel ruler are integrated instead of split across multiple tools.</p>
          </article>
          <article className="card">
            <h2>Privacy-oriented workflow</h2>
            <p>Offline-first behavior and open-source transparency for organizations that care about data control.</p>
          </article>
        </div>

        <div className="table-wrap" style={{ marginTop: "1rem" }}>
          <table>
            <thead>
              <tr>
                <th>Category</th>
                <th>Snapboard</th>
                <th>Greenshot</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td>Region capture + annotation</td>
                <td>Yes</td>
                <td>Yes</td>
              </tr>
              <tr>
                <td>Window capture</td>
                <td>Yes (dropdown &rarr; clipboard + save)</td>
                <td>Yes</td>
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
                <td>No</td>
              </tr>
              <tr>
                <td>OCR on selected area</td>
                <td>Yes</td>
                <td>No</td>
              </tr>
              <tr>
                <td>QR / barcode scan on selected area</td>
                <td>Yes</td>
                <td>No</td>
              </tr>
              <tr>
                <td>Blur sensitive data</td>
                <td>Yes</td>
                <td>Yes</td>
              </tr>
              <tr>
                <td>Color picker</td>
                <td>Yes</td>
                <td>Partial</td>
              </tr>
              <tr>
                <td>Pixel ruler</td>
                <td>Yes</td>
                <td>No</td>
              </tr>
              <tr>
                <td>Dark-themed native UI</td>
                <td>Yes</td>
                <td>No</td>
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
          Need Lightshot or ShareX comparison too? Visit <Link href="/compare">Snapboard comparison</Link>.
        </p>
      </div>
    </section>
  );
}
