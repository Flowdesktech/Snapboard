import type { Metadata } from "next";
import Link from "next/link";

export const metadata: Metadata = {
  title: "FAQ",
  description:
    "Frequently asked questions about Snapboard screenshot app for Windows: pricing, privacy, OCR, hotkeys, and setup.",
  keywords: [
    "Snapboard FAQ",
    "screenshot app FAQ",
    "OCR screenshot app",
    "Lightshot alternative FAQ",
    "PicPick alternative FAQ",
    "Greenshot alternative FAQ",
    "ShareX alternative FAQ",
  ],
  alternates: {
    canonical: "/faq",
  },
};

const faqSchema = {
  "@context": "https://schema.org",
  "@type": "FAQPage",
  mainEntity: [
    {
      "@type": "Question",
      name: "Is Snapboard free?",
      acceptedAnswer: {
        "@type": "Answer",
        text: "Yes. Snapboard is free and open-source under the MIT license.",
      },
    },
    {
      "@type": "Question",
      name: "Does Snapboard upload screenshots?",
      acceptedAnswer: {
        "@type": "Answer",
        text: "No. Snapboard is designed for local workflows and does not require cloud upload.",
      },
    },
    {
      "@type": "Question",
      name: "What systems are supported?",
      acceptedAnswer: {
        "@type": "Answer",
        text: "Snapboard supports Windows 10 (1903+) and Windows 11.",
      },
    },
    {
      "@type": "Question",
      name: "Can I configure hotkeys?",
      acceptedAnswer: {
        "@type": "Answer",
        text: "Yes. Capture, OCR, color picker, full-screen capture, and ruler shortcuts are configurable.",
      },
    },
    {
      "@type": "Question",
      name: "Is Snapboard a good PicPick alternative?",
      acceptedAnswer: {
        "@type": "Answer",
        text: "Yes. Snapboard covers region, window, and scrolling capture, annotation, blur, color picking, and ruler workflows, while adding built-in OCR, pin-to-screen, reverse image search, and an open-source MIT codebase.",
      },
    },
    {
      "@type": "Question",
      name: "Can Snapboard capture a specific window?",
      acceptedAnswer: {
        "@type": "Answer",
        text: "Yes. Snapboard ships a dark-themed dropdown picker listing every open top-level window with its title, process name, and icon. Select one and Snapboard captures it with PrintWindow, copies it to the clipboard, and opens a save dialog with a pre-filled filename — hardware-accelerated Chromium, Electron, and UWP apps are all supported.",
      },
    },
    {
      "@type": "Question",
      name: "Does Snapboard support scrolling capture of long pages?",
      acceptedAnswer: {
        "@type": "Answer",
        text: "Yes. Click any scrollable window and Snapboard auto-scrolls it for you by posting mouse-wheel messages directly to the correct child window (so Chrome, Edge, Electron, Slack, Discord, and Cursor all work), captures every frame, detects per-frame overlap, and stitches them into a single tall image — then copies the result to the clipboard and opens a save dialog. No manual scrolling or cropping.",
      },
    },
    {
      "@type": "Question",
      name: "Which alternatives does Snapboard compete with?",
      acceptedAnswer: {
        "@type": "Answer",
        text: "Snapboard is commonly compared with Lightshot, PicPick, Greenshot, and ShareX for Windows screenshot workflows.",
      },
    },
  ],
};

export default function FaqPage() {
  return (
    <section className="section">
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(faqSchema) }} />
      <div className="container">
        <h1>Snapboard FAQ</h1>
        <p className="hero-subtitle">Quick answers for users comparing screenshot tools on Windows.</p>

        <div className="faq-list" style={{ marginTop: "1rem" }}>
          <details className="faq-item" open>
            <summary>Is Snapboard free and open-source?</summary>
            <p>Yes. Snapboard is MIT-licensed and free for both personal and commercial usage.</p>
          </details>
          <details className="faq-item">
            <summary>Does Snapboard upload images to cloud services?</summary>
            <p>No. Snapboard is privacy-first and built for local workflows with no required sign-up.</p>
          </details>
          <details className="faq-item">
            <summary>Can Snapboard blur sensitive data?</summary>
            <p>Yes. Snapboard includes a blur/pixelate tool for passwords, tokens, emails, and account details.</p>
          </details>
          <details className="faq-item">
            <summary>Does Snapboard support OCR?</summary>
            <p>Yes. You can select an on-screen region and extract text with built-in OCR support.</p>
          </details>
          <details className="faq-item">
            <summary>Can Snapboard capture a specific window?</summary>
            <p>
              Yes. Snapboard ships a dark-themed dropdown picker that lists every open top-level window with
              its title, process name, and icon. Select one and Snapboard captures it with{" "}
              <code>PrintWindow(PW_RENDERFULLCONTENT)</code>, <strong>copies it to the clipboard</strong>, and
              <strong> opens a save dialog</strong> with a pre-filled filename — hardware-accelerated
              Chromium, Electron, and UWP apps are all supported.
            </p>
          </details>
          <details className="faq-item">
            <summary>Does Snapboard support scrolling (long-page) capture?</summary>
            <p>
              Yes. Click any scrollable window and Snapboard takes over: it auto-scrolls the target by
              posting <code>WM_MOUSEWHEEL</code> to the correct child window (so Chrome, Edge, Electron,
              Slack, Discord, and Cursor all work — not just legacy Win32 controls), captures a frame
              every 500 ms with overlap detection, fires a page-sized &quot;booster&quot; scroll before
              declaring the page done, stitches the result, copies it to the clipboard, and opens a save
              dialog — no manual scrolling or cropping.
            </p>
          </details>
          <details className="faq-item">
            <summary>Can I pin a screenshot on top of other windows (Snipaste-style)?</summary>
            <p>
              Yes. Every capture has a <strong>Pin</strong> action that sticks the image to the screen as a
              floating, always-on-top card you can drag, zoom (25–400%), set opacity on, and close with{" "}
              <code>Esc</code>. ShareX does not ship this feature.
            </p>
          </details>
          <details className="faq-item">
            <summary>Can I set custom global hotkeys?</summary>
            <p>Yes. All key tools are configurable in Settings, and conflicts are surfaced clearly.</p>
          </details>
          <details className="faq-item">
            <summary>Which operating systems are supported?</summary>
            <p>Windows 10 (1903+) and Windows 11.</p>
          </details>
          <details className="faq-item">
            <summary>Is Snapboard a good alternative to PicPick?</summary>
            <p>
              Yes. Snapboard includes screenshot capture, annotation, blur, color picker, and pixel ruler features,
              plus built-in OCR and open-source transparency.
            </p>
          </details>
          <details className="faq-item">
            <summary>Which screenshot tools is Snapboard compared with most often?</summary>
            <p>Most users compare Snapboard with Lightshot, PicPick, Greenshot, and ShareX.</p>
          </details>
        </div>
        <p className="section-footnote">
          Alternative guides:{" "}
          <Link href="/lightshot-alternative">Lightshot</Link>,{" "}
          <Link href="/picpick-alternative">PicPick</Link>,{" "}
          <Link href="/greenshot-alternative">Greenshot</Link>,{" "}
          <Link href="/sharex-alternative">ShareX</Link>.
        </p>
      </div>
    </section>
  );
}
