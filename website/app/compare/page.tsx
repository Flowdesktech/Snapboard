import type { Metadata } from "next";
import Link from "next/link";

export const metadata: Metadata = {
  title: "Snapboard vs Lightshot, PicPick, Greenshot, ShareX",
  description:
    "Detailed comparison of Snapboard with Lightshot, PicPick, Greenshot, and ShareX: region / window / scrolling capture, pin-to-screen, privacy, OCR, QR & barcode scan, blur, color picker, pixel ruler, and workflow focus.",
  alternates: {
    canonical: "/compare",
  },
};

export default function ComparePage() {
  return (
    <section className="section">
      <div className="container">
        <h1>Snapboard vs Lightshot, PicPick, Greenshot, and ShareX</h1>
        <p className="hero-subtitle">
          Snapboard focuses on fast, daily screenshot work with strong privacy defaults and practical tools in one
          clean Windows app.
        </p>

        <div className="table-wrap" style={{ marginTop: "1rem" }}>
          <table>
            <thead>
              <tr>
                <th>Category</th>
                <th>Snapboard</th>
                <th>Lightshot</th>
                <th>PicPick</th>
                <th>Greenshot</th>
                <th>ShareX</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td>Region capture + annotation</td>
                <td>Yes</td>
                <td>Yes</td>
                <td>Yes</td>
                <td>Yes</td>
                <td>Yes</td>
              </tr>
              <tr>
                <td>Window capture (dropdown &rarr; clipboard + save dialog)</td>
                <td>Yes</td>
                <td>No</td>
                <td>Yes</td>
                <td>Yes</td>
                <td>Yes</td>
              </tr>
              <tr>
                <td>Scrolling capture (auto-scroll + auto-stitch)</td>
                <td>Yes</td>
                <td>No</td>
                <td>Yes</td>
                <td>No</td>
                <td>Yes</td>
              </tr>
              <tr>
                <td>Pin screenshot to screen (Snipaste-style)</td>
                <td>Yes</td>
                <td>No</td>
                <td>No</td>
                <td>No</td>
                <td>No</td>
              </tr>
              <tr>
                <td>Reverse image search (Google / Bing)</td>
                <td>Yes</td>
                <td>Partial (Google only, cloud upload)</td>
                <td>No</td>
                <td>No</td>
                <td>No</td>
              </tr>
              <tr>
                <td>Blur / pixelate sensitive data</td>
                <td>Yes</td>
                <td>No</td>
                <td>Yes</td>
                <td>Yes</td>
                <td>Yes</td>
              </tr>
              <tr>
                <td>OCR on selected region</td>
                <td>Yes (built-in Windows OCR)</td>
                <td>No</td>
                <td>No</td>
                <td>No</td>
                <td>Yes</td>
              </tr>
              <tr>
                <td>QR / barcode scan on selected region</td>
                <td>Yes (ZXing.Net, offline)</td>
                <td>No</td>
                <td>No</td>
                <td>No</td>
                <td>No</td>
              </tr>
              <tr>
                <td>In-app auto-update from GitHub</td>
                <td>Yes</td>
                <td>No</td>
                <td>Partial (manual)</td>
                <td>Partial (manual)</td>
                <td>Yes</td>
              </tr>
              <tr>
                <td>Color picker and pixel ruler</td>
                <td>Yes</td>
                <td>No</td>
                <td>Yes</td>
                <td>Partial</td>
                <td>Yes</td>
              </tr>
              <tr>
                <td>Dark-themed native UI</td>
                <td>Yes</td>
                <td>No</td>
                <td>Partial</td>
                <td>No</td>
                <td>Partial</td>
              </tr>
              <tr>
                <td>Offline-first workflow</td>
                <td>Yes</td>
                <td>Not primary</td>
                <td>Yes</td>
                <td>Yes</td>
                <td>Yes</td>
              </tr>
              <tr>
                <td>Open-source</td>
                <td>Yes (MIT)</td>
                <td>No</td>
                <td>No</td>
                <td>Yes (GPL)</td>
                <td>Yes (GPL)</td>
              </tr>
            </tbody>
          </table>
        </div>

        <div className="grid two" style={{ marginTop: "1rem" }}>
          <article className="card">
            <h3>Why replace Lightshot?</h3>
            <p>
              Lightshot is region-only. Snapboard adds window capture, scrolling capture, pin-to-screen, privacy
              blur, OCR, color picker, and ruler while keeping the quick select-and-annotate flow users love.
            </p>
          </article>
          <article className="card">
            <h3>Why compare with PicPick?</h3>
            <p>
              PicPick covers the same capture modes, but Snapboard is MIT-licensed and fully free for commercial
              use, adds pin-to-screen and reverse image search, and keeps a tighter privacy-first workflow.
            </p>
          </article>
          <article className="card">
            <h3>Why pick Snapboard over Greenshot?</h3>
            <p>
              Greenshot stopped shipping scrolling capture and has no OCR, color picker, pin-to-screen, or
              reverse image search. Snapboard matches its capture modes and adds a modern utility stack in a
              tighter dark UI.
            </p>
          </article>
          <article className="card">
            <h3>Why not just ShareX?</h3>
            <p>
              ShareX is deep but overwhelming. Snapboard matches ShareX on region / window / scrolling capture
              and OCR, then adds <strong>pin-to-screen</strong> (ShareX doesn&apos;t ship this) and reverse image
              search — all behind a clean, focused, keyboard-driven UI.
            </p>
          </article>
        </div>

        <p className="section-footnote">
          Dive into focused comparisons: <Link href="/lightshot-alternative">Lightshot</Link>,{" "}
          <Link href="/picpick-alternative">PicPick</Link>,{" "}
          <Link href="/greenshot-alternative">Greenshot</Link>, and{" "}
          <Link href="/sharex-alternative">ShareX</Link>.
        </p>
      </div>
    </section>
  );
}
