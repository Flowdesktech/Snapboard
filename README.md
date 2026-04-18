<h1 align="center">Snapboard</h1>

<p align="center">
  <strong>A fast, private, all-in-one screenshot &amp; screen utility for Windows.</strong><br>
  Capture · Annotate · Blur · OCR · Color pick · Measure — all offline, no sign-up, no cloud upload.
</p>

<p align="center">
  <img alt=".NET" src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white">
  <img alt="Platform" src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?logo=windows&logoColor=white">
  <img alt="UI" src="https://img.shields.io/badge/UI-WPF-005A9C">
  <img alt="License" src="https://img.shields.io/badge/license-MIT-green">
  <img alt="Privacy" src="https://img.shields.io/badge/telemetry-none-brightgreen">
</p>

<p align="center">
  <em>A privacy-first open-source alternative to Lightshot, Greenshot, and ShareX — built for Windows 10/11 in C# &amp; WPF on .NET 10.</em>
</p>

---

> **Need a developer for your business idea?** Reach out: **[contact@flowdesk.tech](mailto:contact@flowdesk.tech)**

## Table of contents

- [Why Snapboard](#why-snapboard)
- [Features](#features)
- [Snapboard vs. Lightshot / PicPick / Greenshot / ShareX](#snapboard-vs-lightshot--picpick--greenshot--sharex)
- [Quick start](#quick-start)
- [Keyboard shortcuts](#keyboard-shortcuts)
- [Settings](#settings)
- [How the Blur tool works](#how-the-blur-tool-works)
- [How OCR works](#how-ocr-works)
- [The pixel ruler](#the-pixel-ruler)
- [The color picker](#the-color-picker)
- [Build from source](#build-from-source)
- [Project layout](#project-layout)
- [FAQ](#faq)
- [Roadmap](#roadmap)
- [License](#license)

## Why Snapboard

Most screenshot tools on Windows fall into one of three camps: lightweight but feature-starved (Lightshot), powerful but sprawling (ShareX), or paid (SnagIt, PicPick Pro). **Snapboard** is the middle ground:

- **Zero telemetry, zero cloud uploads.** Everything stays on your machine.
- **A single tray app** instead of five different utilities for snapshotting, picking colors, measuring, and OCR.
- **Dark-themed, keyboard-driven UI** designed to feel native on Windows 11.
- **Open-source &amp; MIT-licensed.** Fork it, audit it, extend it.

Written in C# on **.NET 10** with **WPF**, targeting Windows 10 (1903+) and Windows 11.

## Features

### Screen capture

- **Region capture** with click-and-drag selection, live pixel-size readout, and a "Select area" crosshair cursor like Lightshot
- **Window capture (PicPick-style)** — a compact dark-themed dropdown listing every visible top-level window (title + process name + icon). Select one and Snapboard instantly **copies it to the clipboard**, **prompts you to save** with a pre-filled filename, and fires a single tray toast summarising the result. Uses `PrintWindow(PW_RENDERFULLCONTENT)` so it works correctly with hardware-accelerated Chromium, Electron, and UWP apps, with a fullscreen-fallback for the edge cases
- **Scrolling capture (auto-scroll)** — click any scrollable window and Snapboard takes over: it posts `WM_MOUSEWHEEL` directly to the correct child HWND (so Chrome, Edge, Electron, Slack, Discord, and Cursor all work, not just legacy Win32 controls), captures a frame every 500 ms, detects per-frame overlap, fires a page-sized "booster" scroll before declaring the page done, then **stitches**, **copies to clipboard**, **opens a save dialog**, and toasts — no manual scrolling or cropping
- **Instant full-screen capture** to clipboard + auto-save (great for documentation workflows)
- **Capture mouse cursor** toggle — include or exclude the cursor from the shot
- **Per-monitor-v2 DPI awareness** — correct coordinates on mixed-DPI multi-monitor setups
- Copy to **clipboard** or save as **PNG / JPEG** (quality configurable)
- **Auto-save after capture** option: edited shots go straight to `Pictures\Snapboard` (or your custom folder) — no file dialog

### Annotation toolbar

- **Pen · Rectangle · Arrow · Text · Blur**
- 6-color palette + 3 stroke weights
- **Undo** last annotation (`Ctrl+Z`) — every stroke, shape, and blur rectangle is individually undoable
- `Esc` cancels the capture, `Enter` / `Ctrl+C` copies, `Ctrl+S` saves

### Pin to screen (Snipaste-style — *ShareX doesn't have this*)

The killer feature ShareX users keep asking for. After you drag a selection, click **Pin** in the action bar to "stick" the capture to the screen as a floating, always-on-top card. Keep reference screenshots visible while you type, design, or debug:

- **Always on top**, borderless, over any app
- **Drag anywhere** on the pin to move it
- **Mouse wheel** to zoom (25 %–400 %), `Ctrl+0` resets
- **Right-click** menu: Copy, Save as…, Opacity (30–100 %), Always-on-top toggle, Close
- `Ctrl+C` / `Ctrl+S` / `Esc` shortcuts built in
- Multiple pins at once — stack up as many reference shots as you need

### Reverse image search — Google &amp; Bing

One-click reverse image search from the capture toolbar:

- **Search on Google Images** — uploads the selection straight to `searchbyimage/upload` and opens Google Lens results in your browser
- **Search on Bing Visual Search** — same flow against Bing's visual-search endpoint
- **No third-party image host.** The bitmap goes directly from Snapboard to the search engine — nothing is stored anywhere else
- If the engine rejects the upload, Snapboard falls back to copying the image to your clipboard and opening the search page so you can `Ctrl+V` to continue

### Privacy-first blur tool

Drag a rectangle over passwords, API tokens, email addresses, account numbers — anything sensitive — and the region is **pixelated** with a fast downscale/upscale blur. Layer multiple blurs; all of them survive copy-to-clipboard and save-to-file.

### OCR (optical character recognition)

- **Hotkey:** `Ctrl+Shift+O` by default
- Select a region and Snapboard runs OCR on it via the built-in `Windows.Media.Ocr` engine — no downloads, no external services
- Works against the installed Windows display / keyboard language packs (English out of the box, many others available in Windows Settings → Time &amp; Language → Language &amp; region)
- Result opens in a dark-themed window where you can review and `Copy all` — or Snapboard gives up gracefully with a tray toast if nothing was recognized

### Color picker (PicPick-style)

- **Hotkey:** `Ctrl+Shift+C` by default
- Fullscreen magnifier overlay with a crosshair and live **HEX / RGB / HSL** readout
- Click anywhere on any monitor to copy the color to the clipboard

### Pixel ruler (PicPick-style)

- **Hotkey:** `Ctrl+Shift+R` by default
- Borderless floating ruler, always on top
- **Horizontal or vertical** with clean dimension-preserving orientation flip
- **Click-and-drag to move**, drag the far edge to **resize the length**
- Proper resize cursors (`↔` / `↕`) in the resize zone, arrow cursor elsewhere
- **Opacity** presets (100 / 90 / 80 / 65 / 50 / 35 %) for overlaying on designs
- Dark-themed gear menu (right-click also opens it) — matches the rest of the app

### System-wide hotkeys

Every tool has its own configurable global hotkey:

| Tool                  | Default            |
| --------------------- | ------------------ |
| Region capture        | `PrtScn`           |
| Instant full-screen   | `Ctrl+PrtScn`      |
| Color picker          | `Ctrl+Shift+C`     |
| OCR on selection      | `Ctrl+Shift+O`     |
| Pixel ruler           | `Ctrl+Shift+R`     |

Snapboard **detects hotkey conflicts at startup** (e.g. if another app has already claimed `PrtScn`) and surfaces them three ways: a tray balloon, a red footer banner on the dashboard, and an inline warning inside the Settings dialog.

### Settings dashboard

- Rebind every hotkey via a custom recorder control (press the keys, done)
- Toggle **Capture cursor on screenshot**
- Toggle **Tray single-click = start capture** (off = tray opens the menu only)
- Toggle **Launch at Windows sign-in** — auto-starts to the tray (no dashboard flash), per-user `HKCU\...\Run` entry so no UAC prompt
- Choose **default output format** (PNG or JPEG + quality slider)
- Set a **custom save directory**
- Opt in to **Auto-save after capture** for one-click documentation flows
- Persists to `%APPDATA%\Snapboard\settings.json` — safe to sync or back up

### Dark theme everywhere

Native Windows title bars are set to immersive dark mode via the DWM API on all Snapboard windows (dashboard, Settings, OCR result, ruler). No flash of white on open, no mismatched menus.

## Snapboard vs. Lightshot / PicPick / Greenshot / ShareX

|                                          | Snapboard | Lightshot | PicPick | Greenshot | ShareX |
| ---------------------------------------- | :-------: | :-------: | :-----: | :-------: | :----: |
| Region capture                           |     ✓     |     ✓     |    ✓    |     ✓     |   ✓    |
| **Window capture (dropdown → clipboard + save dialog)** | **✓** | ✗ |  ✓   |    ✓    |   ✓    |
| **Scrolling capture (auto-scroll + auto-stitch)** | **✓** | ✗ |  ✓   |    ✗    |   ✓    |
| Annotation tools                         |     ✓     |     ✓     |    ✓    |     ✓     |   ✓    |
| **Sensitive-data blur tool**             |   **✓**   |     ✗     |    ✓    |     ✓     |   ✓    |
| OCR on selection                         |     ✓     |     ✗     |    ✗    |     ✗     |   ✓    |
| Color picker                             |     ✓     |     ✗     |    ✓    |     ✓     |   ✓    |
| Pixel ruler                              |     ✓     |     ✗     |    ✓    |     ✗     |   ✓    |
| **Pin screenshot to screen (Snipaste-style)** | **✓** | ✗     |    ✗    |     ✗     | **✗**  |
| **Reverse image search (Google / Bing)** |   **✓**   | ✓ *(Google only, paid upload)* | ✗ | ✗ | ✗ |
| **100 % offline / no cloud**             |   **✓**   |     ✗     |    ✓    |     ✓     |   ✓    |
| No account, no sign-up                   |     ✓     |     ✗     |    ✓    |     ✓     |   ✓    |
| Native dark theme                        |     ✓     |     ✗     |    —    |     —     |   —    |
| Dashboard app (not only tray)            |     ✓     |     ✗     |    ✓    |     ✗     |   ~    |
| Free for commercial use                  |     ✓     |     ✓     |    ✗    |     ✓     |   ✓    |
| Open source                              |   **✓**   |     ✗     |    ✗    |     ✓     |   ✓    |
| License                                  |    MIT    | Proprietary | Proprietary | GPL   |  GPL   |

**Snapboard now matches ShareX on every capture mode** (region, window, scrolling) *and* keeps the features ShareX never shipped: **pin-to-screen** (Snipaste-style) and **one-click reverse image search**. No second app, no paid tier, no workflow YAML to configure.

Snapboard is still deliberately scoped: it does *not* ship FTP/Imgur/Dropbox uploaders, screen recording, or workflow automation. If you want those, ShareX is still the king. Snapboard exists for people who want a **fast, focused, private** all-in-one capture toolkit with a clean dark UI.

## Quick start

1. Grab the latest release — or build from source (see below)
2. Run `Snapboard.exe`. It minimizes straight to the **system tray**; the dashboard opens once on first launch
3. Press **`PrtScn`** (or double-click the tray icon) anywhere in Windows to start a capture
4. Drag to select, annotate if you want, then `Enter` to copy or `Ctrl+S` to save

That's it. Full shortcut reference below.

## Keyboard shortcuts

### Global (system-wide)

| Action                       | Default shortcut   |
| ---------------------------- | ------------------ |
| Start region capture         | `PrtScn`           |
| Instant full-screen save     | `Ctrl+PrtScn`      |
| Color picker                 | `Ctrl+Shift+C`     |
| OCR on selection             | `Ctrl+Shift+O`     |
| Pixel ruler                  | `Ctrl+Shift+R`     |

All are **rebindable** under *Settings → Hotkeys*.

### Inside the capture window

| Action                        | Shortcut           |
| ----------------------------- | ------------------ |
| Copy to clipboard             | `Enter` / `Ctrl+C` |
| Save to file                  | `Ctrl+S`           |
| Undo last annotation          | `Ctrl+Z`           |
| Cancel                        | `Esc`              |

Toolbar buttons also expose **Pin**, **Search on Google**, and **Search on Bing**.

### Inside a pinned screenshot

| Action                         | Shortcut           |
| ------------------------------ | ------------------ |
| Copy pin to clipboard          | `Ctrl+C`           |
| Save pin to file               | `Ctrl+S`           |
| Reset zoom                     | `Ctrl+0`           |
| Zoom in / out                  | `Ctrl+=` / `Ctrl+-` / mouse wheel |
| Close pin                      | `Esc`              |

### Inside the pixel ruler

| Action                         | Shortcut           |
| ------------------------------ | ------------------ |
| Toggle orientation             | `Space`            |
| Force horizontal               | `H`                |
| Force vertical                 | `V`                |
| Close                          | `Esc`              |

## Settings

Open from the tray menu or the dashboard's **Settings** button. The file lives at:

```
%APPDATA%\Snapboard\settings.json
```

Example:

```json
{
  "CaptureHotkey": "PrintScreen",
  "InstantFullScreenHotkey": "Ctrl+PrintScreen",
  "ColorPickerHotkey": "Ctrl+Shift+C",
  "OcrHotkey": "Ctrl+Shift+O",
  "PixelRulerHotkey": "Ctrl+Shift+R",
  "CaptureCursor": false,
  "TrayClickCaptures": true,
  "DefaultFormat": "png",
  "JpegQuality": 92,
  "SaveDirectory": "",
  "AutoSaveAfterCapture": false
}
```

Auto-saves go to `Pictures\Snapboard\` unless `SaveDirectory` is set.

## How the Blur tool works

When the capture window opens, Snapboard pre-renders a downscale/upscale-blurred copy of the screenshot in memory. When you drag a rectangle with the Blur tool, that region is swapped with the corresponding slice of the blurred bitmap. Multiple blur rectangles can be layered, and each one is independently undoable.

See [`Snapboard/Helpers/BlurHelper.cs`](Snapboard/Helpers/BlurHelper.cs) and [`Snapboard/CaptureWindow.xaml.cs`](Snapboard/CaptureWindow.xaml.cs) → `CommitBlur`.

## How OCR works

Snapboard uses **`Windows.Media.Ocr`** (built into Windows 10/11) — no Tesseract download, no cloud API:

1. You press the OCR hotkey and drag a selection
2. The overlay **closes immediately** and hands the cropped bitmap off to a background thread
3. A tray toast says *"Reading text…"* while the OCR engine runs (with a 45 s cancellation timeout)
4. On success, a dark result window opens with the extracted text and a *Copy all* button
5. On no-text / engine-error / timeout, you get a tray toast with the specific reason

Critically the OCR runs **entirely off the UI thread**, so your desktop is never frozen while the engine is working.

See [`Snapboard/Ocr/OcrService.cs`](Snapboard/Ocr/OcrService.cs) and [`Snapboard/App.xaml.cs`](Snapboard/App.xaml.cs) → `StartOcrFromBitmap`.

## The pixel ruler

A PicPick-style floating ruler, always on top, borderless.

- Drag anywhere on the ruler body to **move** it
- Drag the far edge (right edge when horizontal, bottom edge when vertical) to **resize** its length
- The cursor switches to `↔` / `↕` only in the resize zone — arrow everywhere else
- Hover the ruler to get a live **"N px"** readout at the cursor position
- Click the gear (or right-click anywhere) for: *Horizontal / Vertical / Always on top / Opacity / Reset size / Close*

Orientation flips are a pure transposition: a horizontal `1200 × 72` ruler becomes a vertical `72 × 1200` ruler, and back again, preserving whatever length you've resized to.

See [`Snapboard/Ruler/PixelRulerWindow.xaml.cs`](Snapboard/Ruler/PixelRulerWindow.xaml.cs).

## The color picker

- Fullscreen overlay with a **magnifier** (shows nearby pixels at ~8×) and a crosshair
- Live **HEX / RGB / HSL** readout in a pill at the top of the screen
- Click to copy the hex value to the clipboard; `Esc` to cancel
- Works across all monitors in a multi-monitor setup

See [`Snapboard/ColorPicker/ColorPickerWindow.xaml.cs`](Snapboard/ColorPicker/ColorPickerWindow.xaml.cs).

## Build from source

Requires the **.NET 10 SDK** on Windows 10 (1903+) / Windows 11.

```powershell
# Clone, then from the repository root
dotnet build Snapboard\Snapboard.csproj -c Release
dotnet run  --project Snapboard -c Release
```

Or open `Snapboard.sln` in **Visual Studio 2022** (17.10+) and press F5.

### Single-file publish

```powershell
dotnet publish Snapboard -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true
```

The resulting `Snapboard.exe` will land in
`Snapboard\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\`.

### Self-contained (no .NET runtime required on the target PC)

```powershell
dotnet publish Snapboard -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

## Project layout

```
Snapboard/
  App.xaml(.cs)                  Tray icon, global hotkeys, OCR/capture dispatcher
  MainWindow.xaml(.cs)           Dashboard (Capture / Utilities / Status)
  CaptureWindow.xaml(.cs)        Fullscreen overlay, selection + annotations
  WindowCaptureDialog.xaml(.cs)  Dark-themed dropdown picker → clipboard + SaveFileDialog + toast
  SettingsWindow.xaml(.cs)       Hotkeys · Output · Capture behavior

  ScrollingCapture/
    ScrollingSelectorWindow.xaml(.cs)  Click-picker: highlights the scrollable window under the cursor
    ScrollingSessionWindow.xaml(.cs)   Auto-scroll loop (WM_MOUSEWHEEL + booster verify) → stitch → clipboard + SaveFileDialog

  ColorPicker/
    ColorPickerWindow.xaml(.cs)  Fullscreen magnifier + HEX/RGB/HSL readout

  Ocr/
    OcrSelectionWindow.xaml(.cs) Region selector for OCR (non-blocking)
    OcrResultWindow.xaml(.cs)    Dark result viewer + Copy all
    OcrService.cs                Thin wrapper around Windows.Media.Ocr

  Ruler/
    PixelRulerWindow.xaml(.cs)   PicPick-style floating ruler

  Settings/
    AppSettings.cs               Serializable settings model
    SettingsService.cs           JSON persistence under %APPDATA%\Snapboard
    HotkeySpec.cs                "Ctrl+Shift+R"-style parser → Win32 modifiers

  Helpers/
    HotkeyManager.cs             RegisterHotKey / UnregisterHotKey wrapper
    ScreenCapture.cs             GDI screen grab + WPF interop
    WindowEnumerator.cs          EnumWindows + PrintWindow + DWM cloak filtering
    ImageStitcher.cs             Vertical-overlap detection + frame stitching
    BlurHelper.cs                Fast approximate blur (downscale + upscale)
    BitmapSaver.cs               PNG/JPEG encode + auto-save path resolver
    DarkTitleBar.cs              DWM immersive dark mode helper

  Controls/
    HotkeyBox.cs                 Keyboard-recorder control for Settings

  Assets/                        App icon (multi-res .ico)
  app.manifest                   Per-monitor-v2 DPI awareness
```

## FAQ

**Does Snapboard upload my screenshots anywhere?**
No. There is no network code in the app — no Imgur, no Lightshot cloud, no telemetry. Block it from reaching the internet with Windows Firewall if you want to be sure.

**Which Windows versions are supported?**
Windows 10 version 1903 (May 2019 Update) and later, and Windows 11. The OCR feature uses Windows' built-in OCR engine which is also available on those versions.

**Can I use a hotkey that's already in use by another app (Screen Sketch, OneDrive, NVIDIA Overlay, etc.)?**
Snapboard will detect the conflict at startup and show a red banner in the dashboard plus a tray balloon listing the failed hotkey(s). Open *Settings → Hotkeys* and pick something else, or disable the conflicting app's hotkey.

**How do I add more OCR languages?**
Install the language in *Windows Settings → Time &amp; Language → Language &amp; region*, make sure the "Basic typing" / "Optical character recognition" feature is installed, then restart Snapboard. The OCR engine automatically picks the best match for what's on the screen.

**Where are screenshots saved?**
By default to `%USERPROFILE%\Pictures\Snapboard\`. You can change this in Settings.

**Does it support multiple monitors?**
The overlay windows (capture, color picker, OCR selection) work across all monitors. The pixel ruler also spans monitors. Mixed-DPI setups are handled via Per-Monitor-V2 awareness.

**Can I run Snapboard on startup?**
Yes — open *Settings → Startup* and tick **"Launch Snapboard when I sign in to Windows"**. Snapboard registers itself under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (per-user, no admin rights required) and is auto-launched with the `--autostart` flag so it boots silently to the tray — your capture hotkey is immediately available without a dashboard window flashing on every sign-in. The Windows installer also exposes the same option as a checkbox during setup.

## Roadmap

- [ ] **Pinned floating screenshots** — pin any capture as a top-most window
- [ ] **Whiteboard mode** — blank or screenshot-backed canvas for quick sketching
- [ ] **Multi-monitor virtual-desktop capture** in one shot
- [ ] Shape library — numbered callouts, speech bubbles
- [ ] Export directly to common targets (clipboard-as-image is already supported; add clipboard-as-file, drag-out)
- [ ] Localization (English-only today)

Suggestions &amp; issues welcome — open a GitHub issue or email [contact@flowdesk.tech](mailto:contact@flowdesk.tech).

## License

[MIT](LICENSE.md) © 2026 FlowDesk. Free for personal and commercial use.

---

<p align="center">
  <sub>
    Keywords: <em>Windows screenshot tool, Lightshot alternative, Greenshot alternative, ShareX alternative,
    free open-source screenshot app, screen annotation, blur sensitive information, OCR Windows,
    color picker Windows, pixel ruler, PicPick alternative, privacy-first screen capture, C#, WPF, .NET 10.</em>
  </sub>
</p>
