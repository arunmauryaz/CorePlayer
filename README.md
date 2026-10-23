<div align="center">

# 🎬 CorePlayer

**Modern, Hardware-Accelerated Desktop Media Player for Windows**

[![Release](https://img.shields.io/github/v/release/arunmauryaz/CorePlayer?style=for-the-badge&color=38BDF8&label=Latest%20Release)](https://github.com/arunmauryaz/CorePlayer/releases)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-0078D6?style=for-the-badge&logo=windows)](https://github.com/arunmauryaz/CorePlayer)
[![Framework](https://img.shields.io/badge/.NET%208-C%23%2012-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
[![Avalonia UI](https://img.shields.io/badge/Avalonia%20UI-11.2-8B5CF6?style=for-the-badge)](https://avaloniaui.net/)
[![Engine](https://img.shields.io/badge/Core%20Engine-libmpv-orange?style=for-the-badge)](https://mpv.io/)
[![License](https://img.shields.io/badge/License-MIT-blue?style=for-the-badge)](LICENSE)

<br/>

**CorePlayer** is a minimal, precision-engineered media player built with **.NET 8** and **Avalonia UI**, powered by the industry-standard **libmpv** native multimedia core. Designed around a **Studio Obsidian & Frosted Silver** aesthetic, controls stay quietly out of your way and seamlessly vanish during playback so your content takes center stage.

</div>

---

## 📖 Overview & Philosophy

Most modern media players are weighed down by heavy browser engines, telemetry, sluggish web UI, or overly busy chrome. **CorePlayer** was designed from the ground up to restore desktop elegance and raw playback performance:

- **Unobtrusive Immersion:** A pure pitch-black canvas (`#000000`) where video is the only focal point. Both top and bottom controls automatically fade away during playback, activating only when you move the cursor.
- **Studio Obsidian Palette:** Replaces garish neon gradients with deep-charcoal glass (`#121316`), frosted silver controls (`#CBD5E1`), and refined ice-blue precision accents (`#38BDF8`).
- **Zero Airspace Jitter:** Hardware-accelerated OpenGL surface integration directly inside the Avalonia visual tree eliminates Win32 window clipping and airspace bugs.
- **Predictable Ergonomics:** Permanent top-right window controls (Always-on-Top Pin, Minimize, Maximize/Restore, and Close) that never shift or glitch between windowed and fullscreen modes.

---

## ⚡ Core Capabilities

### 🚀 Direct GPU Multimedia Engine
- Powered by `libmpv` (v0.36) with native Direct3D11 and OpenGL hardware decoders (`d3d11va`, `nvdec`, `dxva2`).
- Butter-smooth playback across all formats: **4K/8K HDR**, HEVC (H.265), AVC (H.264), AV1, VP9, MKV, MP4, MOV, WEBM, AVI, TS, and FLV.
- High-fidelity audio engine with dialogue boost, dynamic peak normalization, and pitch-compensated speed scaling from `0.5x` up to `2.0x`.

### 📱 Right-Side Options Drawer
- Clean, full-height slide-out panel accessible from the bottom-right hamburger menu.
- **Multi-Level Navigation:** Drill down into sub-pages with a smooth `< Back` navigation header:
  - **Aspect Ratio Sub-Menu:** Fit to Window, 16:9 Widescreen, 4:3 Standard TV, 2.35:1 Cinematic, and 21:9 Ultra-Wide.
  - **Playback Speed Sub-Menu:** Presets from `0.5x` (slow motion) to `2.0x` (double speed) with live checkmark indicators.
  - **Video Rotation:** Instant 90° clockwise orientation cycling.
  - **Settings & Audio Options:** Dynamic Night Mode compression, Always-on-Top pinning, Shuffle, and Repeat.

### 🔊 Auto-Hiding Interactive Volume
- Collapsible volume bar stays completely hidden to keep the interface minimal.
- Hovering over the volume button or rolling the mouse wheel over the audio zone smoothly expands the sound bar.
- Supports volume boost up to **150%** for quiet recordings.

### 🔄 Smart Auto-Resume
- Remembers timestamps on a per-file basis across app restarts.
- Videos automatically resume from the exact frame where you left off without nagging confirmation popups.

### 🎛️ Dual Dubs & Subtitle Management
- Bottom-right corner popover dedicated to multi-language audio dub tracks and subtitle streams.
- Full support for internal embedded tracks (MKV/MP4) and external drag-and-drop subtitle files (`.srt`, `.ass`, `.vtt`).
- Millisecond-precision subtitle delay sync adjustment (+/- 100ms).

---

## ⌨️ Keyboard & Gesture Cheatsheet

| Key / Gesture | Function |
| :--- | :--- |
| `Space` | Play / Pause playback |
| `Left Arrow` / `Right Arrow` | Seek backward / forward 5 seconds |
| `Shift + Left` / `Shift + Right` | Seek backward / forward 30 seconds |
| `Up Arrow` / `Down Arrow` | Volume Up / Down (+/- 5%) |
| `Mouse Wheel` *(over Volume)* | Instant volume adjust with automatic slider reveal |
| `M` | Mute / Unmute audio |
| `F` or `Double Click` | Toggle Fullscreen mode |
| `Esc` | Exit fullscreen or dismiss active drawer/popover |
| `C` | Toggle Audio Streams & Subtitle Manager |
| `Shift + N` / `Shift + P` | Next / Previous video in Playlist Queue |
| `Ctrl + S` | Capture crystal-clear snapshot to Pictures folder |
| `Ctrl + O` | Open file dialog |
| `Ctrl + F` | Open folder / batch playlist |
| `.` / `,` | Frame-step forward / backward |

---

## 📥 Download & Installation

### Option 1: Standalone Windows Package (Portable)
Download the standalone executable directly from **[Releases](https://github.com/arunmauryaz/CorePlayer/releases)**:
1. Download **`CorePlayer-v1.0.0-win-x64.zip`**.
2. Extract the archive anywhere on your PC.
3. Launch **`CorePlayer.exe`**.

> *Note: This package is completely self-contained. It includes the required .NET 8 runtime and native `libmpv-2.dll`—no external installations needed.*

### Option 2: Run from Source
Clone the repository and launch directly:
```powershell
git clone https://github.com/arunmauryaz/CorePlayer.git
cd CorePlayer
.\run.bat
```

---

## 🏗️ Technical Architecture

CorePlayer follows clean architectural boundaries:
- **Presentation Layer:** Avalonia UI 11.2 with custom compiled XAML bindings (`AvaloniaUseCompiledBindingsByDefault`).
- **MVVM Foundation:** `CommunityToolkit.Mvvm` with decoupled relay commands and observable properties.
- **Engine Core:** `libmpv-2.dll` via `HanumanInstitute.LibMpv` with an asynchronous 60 FPS state sync dispatcher.
- **Hardware Pipeline:** Direct GPU decoding via `auto-safe` (D3D11VA / DXVA2 / NVDEC / VAAPI).

> 📘 *For complete architecture diagrams and subsystem specifications, refer to [`docs/PROJECT_CONTEXT_AND_PLANS.md`](docs/PROJECT_CONTEXT_AND_PLANS.md).*

---

## 🛠️ Build & Test

To build the release binary locally:
```powershell
# Restore and build in Release mode
dotnet build -c Release

# Execute test suite (36 unit tests)
dotnet test -c Release
```

---

## 📄 License & Credits

- Distributed under the **MIT License**.
- Media playback engine powered by the [mpv player](https://mpv.io) project.
- Modern cross-platform XAML engine powered by [Avalonia UI](https://avaloniaui.net).
