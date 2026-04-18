import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";

const repoUrl = "https://github.com/Flowdesktech/Snapboard";

export const metadata: Metadata = {
  title: "Best Lightshot, PicPick, Greenshot & ShareX Alternative",
  description:
    "Snapboard is a modern alternative to Lightshot, PicPick, Greenshot, and ShareX for Windows with window capture, PicPick-style scrolling capture, blur, OCR, QR & barcode scan, color picker, pixel ruler, pin-to-screen, and zero cloud uploads.",
};

const softwareSchema = {
  "@context": "https://schema.org",
  "@type": "SoftwareApplication",
  name: "Snapboard",
  applicationCategory: "UtilityApplication",
  operatingSystem: "Windows 10, Windows 11",
  offers: {
    "@type": "Offer",
    price: "0",
    priceCurrency: "USD",
  },
  isAccessibleForFree: true,
  url: repoUrl,
  description:
    "Open-source screenshot tool for Windows with annotation, blur, OCR, color picker, and pixel ruler.",
};

export default function HomePage() {
  return (
    <>
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(softwareSchema) }}
      />

      <section className="hero">
        <div className="container">
          <div className="hero-brand">
            <Image src="/images/snapboard-logo.svg" alt="Snapboard" width={260} height={66} priority />
          </div>
          <p className="eyebrow">Open-source screenshot app for Windows</p>
          <h1>Capture regions, windows, and whole scrolling pages — all local.</h1>
          <p className="hero-subtitle">
            Snapboard is a privacy-first alternative to Lightshot, PicPick, Greenshot, and ShareX for
            fast everyday screenshot workflows on Windows 10/11. Region, window, and PicPick-style
            scrolling capture; blur, OCR, QR &amp; barcode scan, color picker, pixel ruler, and
            pin-to-screen — in one tiny tray app.
          </p>
          <div className="cta-row">
            <a className="btn btn-primary" href={`${repoUrl}/releases`} target="_blank" rel="noreferrer">
              Download Latest Release
            </a>
            <a className="btn btn-secondary" href={repoUrl} target="_blank" rel="noreferrer">
              View Source on GitHub
            </a>
          </div>

          <div className="hero-metrics" aria-label="Product highlights">
            <span>MIT licensed</span>
            <span>Offline by default</span>
            <span>Global hotkeys</span>
            <span>Region · Window · Scrolling</span>
            <span>Pin-to-screen</span>
            <span>Blur + OCR + QR + Ruler</span>
            <span>Auto-update from GitHub</span>
          </div>
        </div>
      </section>

      <section className="section">
        <div className="container">
          <h2>See Snapboard in action</h2>
          <p className="hero-subtitle">
            Clean dashboard for daily use, plus a Lightshot-style capture overlay for fast annotate-and-share
            workflows.
          </p>
          <div className="shot-grid">
            <figure className="shot-card shot-card-dashboard">
        <Image
                src="/images/snapboard-dashboard.png"
                alt="Snapboard main window with capture button, shortcuts panel, and utility tools."
                width={976}
                height={687}
                sizes="(max-width: 900px) 100vw, 1100px"
          priority
        />
              <figcaption>Main dashboard: quick launch + utility tools in one place.</figcaption>
            </figure>
            <figure className="shot-card shot-card-capture">
              <Image
              className="shot-card-capture-img"
                src="/images/snapboard-capture.png"
                alt="Snapboard capture overlay with selection rectangle and vertical/horizontal toolbars."
                width={1919}
                height={1032}
                sizes="(max-width: 900px) 100vw, 1100px"
              />
              <figcaption>
                Real capture result: sensitive text masked with blur before sharing.
              </figcaption>
            </figure>
          </div>
        </div>
      </section>

      <section className="section">
        <div className="container">
          <h2>Why teams choose Snapboard</h2>
          <div className="grid three">
            <article className="card">
              <h3>Privacy by default</h3>
              <p>No uploads, no account, no telemetry. Screenshots stay on your machine.</p>
            </article>
            <article className="card">
              <h3>All-in-one workflow</h3>
              <p>
                Region capture, annotation, blur, OCR, color picker, and pixel ruler in one small tray app.
              </p>
            </article>
            <article className="card">
              <h3>Built for speed</h3>
              <p>
                Global hotkeys, dark UI, and instant save options for documentation, QA, support, and dev teams.
              </p>
            </article>
          </div>
        </div>
      </section>

      <section className="section">
        <div className="container">
          <h2>Built for real-world screenshot workflows</h2>
          <div className="grid three">
            <article className="card">
              <h3>QA and bug reporting</h3>
              <p>
                Capture UI bugs quickly, annotate exactly where the issue is, and keep sensitive test data hidden with
                blur.
              </p>
            </article>
            <article className="card">
              <h3>Support and customer success</h3>
              <p>
                Create step-by-step replies with arrows, text labels, and instant copy/save actions that speed up
                ticket resolution.
              </p>
            </article>
            <article className="card">
              <h3>Engineering and docs</h3>
              <p>
                Use global hotkeys, OCR extraction, and auto-save to document flows without context switching or extra
                tooling.
              </p>
            </article>
          </div>
        </div>
      </section>

      <section className="section">
        <div className="container">
          <h2>Core features</h2>
          <div className="grid two">
            <article className="card">
              <h3>Region capture + annotate</h3>
              <p>Drag-to-select any area, then use pen, rectangle, arrow, text, blur, and undo — all offline.</p>
            </article>
            <article className="card">
              <h3>Window capture (new)</h3>
              <p>
                A compact dark dropdown lists every open window. Select one and Snapboard grabs it via
                pixel-perfect <code>PrintWindow</code>, <strong>copies it to the clipboard</strong>, and
                opens a pre-filled save dialog — works with hardware-accelerated Chromium, Electron, and
                UWP apps.
              </p>
            </article>
            <article className="card">
              <h3>Scrolling capture, PicPick-style (new)</h3>
              <p>
                Hover any scrollable window and Snapboard red-outlines only the content child — no
                title bar, tabs, or toolbars. Click once and it auto-scrolls by posting
                <code> WM_MOUSEWHEEL</code> to the right child (Chrome, Edge, Electron, Slack,
                Discord, Cursor), stitches every frame with multi-strip overlap correlation, then
                copies to the clipboard and prompts to save.
              </p>
            </article>
            <article className="card">
              <h3>Pin to screen</h3>
              <p>
                Pin any capture as a floating, always-on-top reference window. Drag, zoom, and keep it visible
                while you type — the flagship feature ShareX doesn&apos;t ship.
              </p>
            </article>
            <article className="card">
              <h3>Privacy blur tool</h3>
              <p>Pixelate sensitive data before sharing screenshots in docs, chat, or tickets.</p>
            </article>
            <article className="card">
              <h3>OCR on selection</h3>
              <p>Extract text from any selected area using the built-in Windows OCR engine — no cloud calls.</p>
            </article>
            <article className="card">
              <h3>QR &amp; barcode scan (new)</h3>
              <p>
                Drag a rectangle around any QR, Data Matrix, Aztec, PDF-417, or EAN/UPC/Code-128
                barcode and Snapboard decodes it offline via ZXing.Net — with automatic upscale and
                colour-inversion fallbacks for tiny or dark-mode codes. Copy the payload or open
                <code> http(s)</code> links in one click. Default hotkey <code>Ctrl+Shift+Q</code>.
              </p>
            </article>
            <article className="card">
              <h3>Reverse image search</h3>
              <p>
                One-click Google Images and Bing Visual Search directly from the capture toolbar — the bitmap
                goes straight to the search engine, never to a third-party host.
              </p>
            </article>
            <article className="card">
              <h3>Utilities included</h3>
              <p>Color picker with magnifier + HEX/RGB/HSL, and a floating pixel ruler — same hotkey-driven app.</p>
            </article>
            <article className="card">
              <h3>Auto-update from GitHub (new)</h3>
              <p>
                Snapboard checks the public GitHub Releases feed on startup and once a day; a dark
                prompt shows the release notes with Install / Later / Skip. Updates land in place
                with a silent installer, no Store, no telemetry, and auto-relaunch after upgrade.
              </p>
            </article>
          </div>
        </div>
      </section>

      <section className="section">
        <div className="container">
          <h2>What you get out of the box</h2>
          <div className="grid two">
            <article className="card list-card">
              <h3>Productivity defaults</h3>
              <ul className="check-list">
                <li>System tray app with global hotkeys</li>
                <li>Instant full-screen capture to file</li>
                <li>Auto-save mode for zero-click exports</li>
                <li>Configurable output format and JPEG quality</li>
              </ul>
            </article>
            <article className="card list-card">
              <h3>Privacy and control</h3>
              <ul className="check-list">
                <li>No required account, no upload flow</li>
                <li>Conflict-aware hotkey registration</li>
                <li>Per-user startup behavior with tray launch</li>
                <li>Open-source MIT codebase on GitHub</li>
              </ul>
            </article>
          </div>
        </div>
      </section>

      <section className="section">
        <div className="container">
          <h2>Fast workflow, no learning curve</h2>
          <div className="workflow">
            <article className="workflow-step">
              <span className="workflow-index">1</span>
              <div>
                <h3>Press hotkey</h3>
                <p>Use Print Screen (or your custom hotkey) from anywhere in Windows.</p>
              </div>
            </article>
            <article className="workflow-step">
              <span className="workflow-index">2</span>
              <div>
                <h3>Select and annotate</h3>
                <p>Draw a region, add arrows/text, blur sensitive details, and undo as needed.</p>
              </div>
            </article>
            <article className="workflow-step">
              <span className="workflow-index">3</span>
              <div>
                <h3>Copy or save</h3>
                <p>Send to clipboard instantly or auto-save to your preferred folder with zero upload.</p>
              </div>
            </article>
          </div>
        </div>
      </section>

      <section className="section">
        <div className="container">
          <h2>Snapboard vs alternatives</h2>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Feature</th>
                  <th>Snapboard</th>
                  <th>Lightshot</th>
                  <th>PicPick</th>
                  <th>Greenshot</th>
                  <th>ShareX</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td>Region capture</td>
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
                  <td>Partial</td>
                  <td>No</td>
                  <td>No</td>
                  <td>No</td>
                </tr>
                <tr>
                  <td>Blur sensitive data</td>
                  <td>Yes</td>
                  <td>No</td>
                  <td>Yes</td>
                  <td>Yes</td>
                  <td>Yes</td>
                </tr>
                <tr>
                  <td>OCR on selection</td>
                  <td>Yes</td>
                  <td>No</td>
                  <td>No</td>
                  <td>No</td>
                  <td>Yes</td>
                </tr>
                <tr>
                  <td>QR / barcode scan on selection</td>
                  <td>Yes</td>
                  <td>No</td>
                  <td>No</td>
                  <td>No</td>
                  <td>No</td>
                </tr>
                <tr>
                  <td>Color picker + pixel ruler</td>
                  <td>Yes</td>
                  <td>No</td>
                  <td>Yes</td>
                  <td>Partial</td>
                  <td>Yes</td>
                </tr>
                <tr>
                  <td>100% offline, no account</td>
                  <td>Yes</td>
                  <td>No</td>
                  <td>Yes</td>
                  <td>Yes</td>
                  <td>Yes</td>
                </tr>
                <tr>
                  <td>Open-source (MIT)</td>
                  <td>Yes</td>
                  <td>No</td>
                  <td>No</td>
                  <td>Yes (GPL)</td>
                  <td>Yes (GPL)</td>
                </tr>
              </tbody>
            </table>
          </div>
          <p className="section-footnote">
            Snapboard matches ShareX on capture modes (region, window, scrolling) while adding pin-to-screen —
            the one feature ShareX never shipped. Want a deeper breakdown?{" "}
            <Link href="/compare">See the full comparison page</Link>.
          </p>
        </div>
      </section>

      <section className="section">
        <div className="container">
          <h2>Looking for a specific alternative?</h2>
          <div className="grid two">
            <article className="card">
              <h3>Lightshot alternative</h3>
              <p>Compare Snapboard with Lightshot for privacy, OCR, and productivity workflows.</p>
              <p className="section-footnote">
                <Link href="/lightshot-alternative">Read Lightshot alternative guide</Link>
              </p>
            </article>
            <article className="card">
              <h3>PicPick alternative</h3>
              <p>See how Snapboard compares with PicPick for all-in-one screenshot and utility workflows.</p>
              <p className="section-footnote">
                <Link href="/picpick-alternative">Read PicPick alternative guide</Link>
              </p>
            </article>
            <article className="card">
              <h3>Greenshot alternative</h3>
              <p>See where Snapboard offers a more modern and focused screenshot workflow.</p>
              <p className="section-footnote">
                <Link href="/greenshot-alternative">Read Greenshot alternative guide</Link>
              </p>
            </article>
            <article className="card">
              <h3>ShareX alternative</h3>
              <p>For teams that want speed and simplicity instead of broad power-user complexity.</p>
              <p className="section-footnote">
                <Link href="/sharex-alternative">Read ShareX alternative guide</Link>
              </p>
            </article>
          </div>
        </div>
      </section>

      <section className="section">
        <div className="container">
          <h2>Explore by use case</h2>
          <div className="grid two">
            <article className="card">
              <h3>Best Windows screenshot tool</h3>
              <p>See what to look for in a modern screenshot app for Windows teams.</p>
              <p className="section-footnote">
                <Link href="/windows-screenshot-tool">Read Windows screenshot tool guide</Link>
              </p>
            </article>
            <article className="card">
              <h3>Screenshot tool with OCR</h3>
              <p>Learn how OCR improves bug reporting, docs, and support workflows.</p>
              <p className="section-footnote">
                <Link href="/screenshot-tool-with-ocr">Read OCR screenshot guide</Link>
              </p>
            </article>
          </div>
        </div>
      </section>

      <section className="section">
        <div className="container">
          <h2>Technical highlights</h2>
          <div className="grid two">
            <article className="card">
              <h3>Native Windows foundation</h3>
              <p>
                Built with C#, WPF, and .NET 10 for smooth Windows integration, dark title bars, and reliable desktop
                behavior.
              </p>
            </article>
            <article className="card">
              <h3>Safe and responsive OCR flow</h3>
              <p>
                OCR runs off the UI thread with timeout handling, so text extraction never blocks your desktop while
                work continues.
              </p>
            </article>
          </div>
        </div>
      </section>

      <section className="section">
        <div className="container">
          <h2>FAQ</h2>
          <div className="faq-list">
            <details className="faq-item">
              <summary>Is Snapboard free?</summary>
              <p>Yes. Snapboard is MIT-licensed and free for personal and commercial use.</p>
            </details>
            <details className="faq-item">
              <summary>Does Snapboard upload screenshots to the cloud?</summary>
              <p>No. Snapboard is built for local workflows and does not require sign-up or cloud upload.</p>
            </details>
            <details className="faq-item">
              <summary>Which Windows versions are supported?</summary>
              <p>Windows 10 (1903+) and Windows 11.</p>
            </details>
            <details className="faq-item">
              <summary>Can I set my own hotkeys?</summary>
              <p>Yes. Every Snapboard tool has configurable global hotkeys in Settings.</p>
            </details>
          </div>
          <p className="section-footnote">
            Need more answers? <Link href="/faq">Read all FAQs</Link>.
          </p>
        </div>
      </section>

      <section className="section">
        <div className="container">
          <div className="cta-panel">
            <h2>Ready to replace your screenshot stack?</h2>
            <p>
              Download Snapboard for Windows and streamline capture, blur, OCR, and utility workflows in one local
              app.
            </p>
            <div className="cta-row">
              <a className="btn btn-primary" href={`${repoUrl}/releases`} target="_blank" rel="noreferrer">
                Get Snapboard Release
              </a>
              <a className="btn btn-secondary" href={repoUrl} target="_blank" rel="noreferrer">
                Star on GitHub
              </a>
            </div>
          </div>
        </div>
      </section>
    </>
  );
}
