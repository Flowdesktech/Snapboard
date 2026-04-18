import type { Metadata } from "next";
import Link from "next/link";

const title = "Best PicPick Alternative for Windows";
const description =
  "Snapboard is a focused PicPick alternative for teams that want modern region, window, and scrolling capture, pin-to-screen, blur, OCR, color picking, and pixel measuring in one clean open-source workflow.";

export const metadata: Metadata = {
  title,
  description,
  keywords: [
    "PicPick alternative",
    "best PicPick alternative",
    "screenshot and color picker app",
    "pixel ruler Windows",
  ],
  alternates: {
    canonical: "/picpick-alternative",
  },
};

export default function PicPickAlternativePage() {
  return (
    <section className="section">
      <div className="container">
        <h1>{title}</h1>
        <p className="hero-subtitle">{description}</p>

        <div className="grid two" style={{ marginTop: "1rem" }}>
          <article className="card">
            <h2>Why people compare Snapboard vs PicPick</h2>
            <p>
              Both target productivity workflows, but Snapboard emphasizes a cleaner capture-to-share path with
              privacy-first defaults and open-source transparency.
            </p>
          </article>
          <article className="card">
            <h2>What Snapboard brings</h2>
            <ul className="check-list">
              <li>Region, window, and PicPick-style content-only scrolling capture</li>
              <li>Pin any capture to screen as a floating reference</li>
              <li>Reverse image search on Google and Bing</li>
              <li>Built-in blur, OCR, and QR / barcode scan in the same toolchain</li>
              <li>Color picker and pixel ruler with global hotkeys</li>
              <li>Silent auto-update from GitHub Releases</li>
              <li>Open-source MIT codebase — free for commercial use</li>
            </ul>
          </article>
        </div>

        <div className="table-wrap" style={{ marginTop: "1rem" }}>
          <table>
            <thead>
              <tr>
                <th>Feature</th>
                <th>Snapboard</th>
                <th>PicPick</th>
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
                <td>Yes</td>
                <td>No</td>
              </tr>
              <tr>
                <td>Blur / pixelate</td>
                <td>Yes</td>
                <td>Yes</td>
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
                <td>In-app auto-update</td>
                <td>Yes (GitHub Releases)</td>
                <td>Partial (manual)</td>
              </tr>
              <tr>
                <td>Color picker + ruler</td>
                <td>Yes</td>
                <td>Yes</td>
              </tr>
              <tr>
                <td>Free for commercial use</td>
                <td>Yes</td>
                <td>No (paid upgrade)</td>
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
          See broader comparisons on <Link href="/compare">Snapboard vs alternatives</Link>.
        </p>
      </div>
    </section>
  );
}
