import { ImageResponse } from "next/og";

export const size = {
  width: 1200,
  height: 630,
};

export const contentType = "image/png";

export default function OpenGraphImage() {
  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          flexDirection: "column",
          justifyContent: "space-between",
          background:
            "radial-gradient(circle at 15% 0%, rgba(97,181,255,.26) 0%, rgba(97,181,255,0) 38%), #0c1018",
          color: "#e9efff",
          padding: 56,
          fontFamily: "Inter, Segoe UI, Arial",
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: 16 }}>
          <div
            style={{
              width: 54,
              height: 54,
              borderRadius: 14,
              background: "linear-gradient(180deg,#7fd0ff,#2a95f2)",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              color: "#06213f",
              fontWeight: 900,
              fontSize: 26,
            }}
          >
            S
          </div>
          <div style={{ fontSize: 36, fontWeight: 800 }}>Snapboard</div>
        </div>

        <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
          <div style={{ fontSize: 62, lineHeight: 1.08, fontWeight: 800 }}>
            Open-source screenshot app for Windows
          </div>
          <div style={{ fontSize: 30, color: "#b7c9e7" }}>
            Capture, annotate, blur, OCR, color picker, and pixel ruler.
          </div>
        </div>

        <div style={{ display: "flex", gap: 10 }}>
          {["Offline-first", "Lightshot alternative", "MIT licensed"].map((item) => (
            <div
              key={item}
              style={{
                border: "1px solid #2a3f62",
                borderRadius: 999,
                padding: "8px 14px",
                fontSize: 23,
                color: "#c5daf7",
              }}
            >
              {item}
            </div>
          ))}
        </div>
      </div>
    ),
    size,
  );
}
