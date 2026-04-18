import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { Geist, Geist_Mono } from "next/font/google";
import "./globals.css";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

const repoUrl = "https://github.com/Flowdesktech/Snapboard";
const siteUrl = process.env.NEXT_PUBLIC_SITE_URL ?? "https://snapboard.flowdesk.tech";

export const metadata: Metadata = {
  metadataBase: new URL(siteUrl),
  title: {
    default: "Snapboard | Open-Source Screenshot Tool for Windows",
    template: "%s | Snapboard",
  },
  description:
    "Snapboard is a privacy-first screenshot tool for Windows with region/window/scrolling capture, annotation, blur, OCR, QR & barcode scan, color picker, and pixel ruler — all local, no cloud uploads.",
  keywords: [
    "Snapboard",
    "Snapboard app",
    "Lightshot alternative",
    "PicPick alternative",
    "Greenshot alternative",
    "ShareX alternative",
    "Windows screenshot tool",
    "scrolling screenshot Windows",
    "OCR screenshot",
    "QR code scanner Windows",
    "barcode reader desktop",
    "blur sensitive info screenshot",
    "open-source screenshot app",
    "privacy-first screenshot app",
  ],
  alternates: {
    canonical: "/",
  },
  openGraph: {
    title: "Snapboard | Open-Source Screenshot Tool for Windows",
    description:
      "Best alternative to Lightshot, PicPick, Greenshot, and ShareX for private, fast screenshot workflows on Windows.",
    url: "/",
    siteName: "Snapboard",
    type: "website",
    images: [
      {
        url: "/images/snapboard-dashboard.png",
        width: 976,
        height: 687,
        alt: "Snapboard screenshot dashboard preview",
      },
    ],
  },
  twitter: {
    card: "summary_large_image",
    title: "Snapboard | Open-Source Screenshot Tool for Windows",
    description:
      "A privacy-first alternative to Lightshot, PicPick, Greenshot, and ShareX with scrolling capture, blur, OCR, QR scan, color picker, and pixel ruler.",
    images: ["/images/snapboard-dashboard.png"],
  },
  category: "technology",
  creator: "FlowDesk",
  publisher: "FlowDesk",
  robots: {
    index: true,
    follow: true,
    googleBot: {
      index: true,
      follow: true,
      "max-image-preview": "large",
      "max-snippet": -1,
      "max-video-preview": -1,
    },
  },
  icons: {
    icon: "/icon.svg",
    shortcut: "/icon.svg",
    apple: "/icon.svg",
  },
};

const organizationSchema = {
  "@context": "https://schema.org",
  "@type": "Organization",
  name: "FlowDesk",
  url: siteUrl,
  sameAs: [repoUrl],
  logo: `${siteUrl}/images/snapboard-mark.svg`,
};

const websiteSchema = {
  "@context": "https://schema.org",
  "@type": "WebSite",
  name: "Snapboard",
  url: siteUrl,
  inLanguage: "en",
  publisher: {
    "@type": "Organization",
    name: "FlowDesk",
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" className={`${geistSans.variable} ${geistMono.variable}`}>
      <body>
        <script
          type="application/ld+json"
          dangerouslySetInnerHTML={{ __html: JSON.stringify(organizationSchema) }}
        />
        <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(websiteSchema) }} />
        <header className="site-header">
          <div className="container nav">
            <Link href="/" className="brand">
              <Image src="/images/snapboard-mark.svg" alt="Snapboard logo" width={30} height={30} />
              <span>Snapboard</span>
            </Link>
            <nav className="nav-links" aria-label="Primary">
              <Link href="/compare">Compare</Link>
              <Link href="/faq">FAQ</Link>
              <a href={`${repoUrl}/releases`} target="_blank" rel="noopener noreferrer">
                Download
              </a>
              <a href={repoUrl} target="_blank" rel="noopener noreferrer">
                GitHub
              </a>
            </nav>
          </div>
        </header>

        <main className="site-main">{children}</main>

        <footer className="site-footer">
          <div className="container footer-row">
            <p className="footer-brand">
              <Image src="/images/snapboard-mark.svg" alt="" width={20} height={20} />
              <span>Snapboard — open-source, privacy-first screenshot tooling for Windows.</span>
            </p>
            <a href={repoUrl} target="_blank" rel="noopener noreferrer">
              Flowdesktech/Snapboard
            </a>
          </div>
        </footer>
      </body>
    </html>
  );
}
