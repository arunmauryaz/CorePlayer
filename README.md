<div align="center">

# 🎬 CorePlayer

**Modern, Cross-Platform Hardware-Accelerated Desktop Media Player**

[![Release](https://img.shields.io/github/v/release/arunmauryaz/CorePlayer?style=for-the-badge&color=38BDF8&label=Latest%20Release)](https://github.com/arunmauryaz/CorePlayer/releases)
[![Windows](https://img.shields.io/badge/Windows-x64-0078D6?style=for-the-badge&logo=windows)](https://github.com/arunmauryaz/CorePlayer/releases)
[![macOS](https://img.shields.io/badge/macOS-Apple%20Silicon%20%7C%20Intel-000000?style=for-the-badge&logo=apple)](https://github.com/arunmauryaz/CorePlayer/releases)
[![Linux](https://img.shields.io/badge/Linux-x64-FCC624?style=for-the-badge&logo=linux&logoColor=black)](https://github.com/arunmauryaz/CorePlayer/releases)
[![Framework](https://img.shields.io/badge/.NET%208-C%23%2012-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
[![Avalonia UI](https://img.shields.io/badge/Avalonia%20UI-11.2-8B5CF6?style=for-the-badge)](https://avaloniaui.net/)
[![Engine](https://img.shields.io/badge/Core%20Engine-libmpv-orange?style=for-the-badge)](https://mpv.io/)
[![License](https://img.shields.io/badge/License-MIT-blue?style=for-the-badge)](LICENSE)

<br/>

**CorePlayer** is a minimal, precision-engineered media player built with **.NET 8** and **Avalonia UI**, powered by the industry-standard **libmpv** native multimedia core. Designed around a **Studio Obsidian & Frosted Silver** aesthetic, controls stay quietly out of your way and seamlessly vanish during playback so your content takes center stage. Available across **Windows**, **macOS**, and **Linux**.

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

### 🎞️ Netflix-Style Timeline Hover Preview
- **Instant Live Previews:** Hovering over the seek bar displays a crisp thumbnail of the exact scene with formatted timecode.
- **Frame-Accurate Seeking:** Clicking on the timeline seeks precisely to the exact frame displayed in the preview.
- **Resource Optimized:** Thumbnails are generated on-demand using an isolated background worker. Instantly dismissed on mouse leave with zero persistent disk accumulation.

### 📱 Right-Side Options Drawer
- Clean, full-height slide-out panel accessible from the bottom-right hamburger menu.
- **Multi-Level Navigation:** Drill down into sub-pages with a smooth `< Back` navigation header:
  - **Aspect Ratio Sub-Menu:** Fit to Window, 16:9 Widescreen, 4:3 Standard TV, 2.35:1 Cinematic, and 21:9 Ultra-Wide.
  - **Playback Speed Sub-Menu:** Presets from `0.5x` (slow motion) to `2.0x` (double speed) with live checkmark indicators.
  - **Video Rotation:** Instant 90° clockwise orientation cycling.
  - **Settings & Audio Options:** Configurable Snapshot directory, Night Mode compression, Always-on-Top pinning, Shuffle, and Repeat.

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

Pre-built binaries for all major platforms are available on **[Releases](https://github.com/arunmauryaz/CorePlayer/releases)**:

### 🪟 Windows (x64)
1. Download **`CorePlayer-v1.1.0-win-x64.zip`**.
2. Extract the archive anywhere on your PC.
3. Launch **`CorePlayer.exe`**.
> *Note: Windows packages bundle native `libmpv-2.dll` out-of-the-box.*

### 🐧 Linux (x64)
1. Install `libmpv` via your package manager:
   - Ubuntu / Debian: `sudo apt install libmpv2` (or `libmpv1`)
   - Arch Linux: `sudo pacman -S mpv`
   - Fedora: `sudo dnf install mpv-libs`
2. Download **`CorePlayer-v1.1.0-linux-x64.tar.gz`**.
3. Extract and run:
   ```bash
   tar -xvf CorePlayer-v1.1.0-linux-x64.tar.gz
   ./CorePlayer
   ```

### 🍎 macOS (Apple Silicon & Intel)
1. Install `mpv` via Homebrew:
   ```bash
   brew install mpv
   ```
2. Download **`CorePlayer-v1.1.0-osx-arm64.zip`** (M1/M2/M3/M4) or **`CorePlayer-v1.1.0-osx-x64.zip`** (Intel).
3. Extract and run:
   ```bash
   ./CorePlayer
   ```

---

## 🏗️ Technical Architecture

CorePlayer follows clean architectural boundaries:
- **Presentation Layer:** Avalonia UI 11.2 with custom compiled XAML bindings (`AvaloniaUseCompiledBindingsByDefault`).
- **MVVM Foundation:** `CommunityToolkit.Mvvm` with decoupled relay commands and observable properties.
- **Engine Core:** `libmpv` cross-platform resolution (`.dll`, `.so`, `.dylib`) via `HanumanInstitute.LibMpv` with an asynchronous 60 FPS state sync dispatcher.
- **Hardware Pipeline:** Direct GPU decoding via `auto-safe` (D3D11VA / DXVA2 / NVDEC / VAAPI / VideoToolbox).

> 📘 *For complete architecture diagrams and subsystem specifications, refer to [`docs/PROJECT_CONTEXT_AND_PLANS.md`](docs/PROJECT_CONTEXT_AND_PLANS.md).*

---

## 🛠️ Build & Test

To build the release binary locally:
```powershell
# Restore and build in Release mode
dotnet build -c Release

# Execute test suite (45 unit tests)
dotnet test -c Release
```

---

## 📄 License & Credits

- Distributed under the **MIT License**.
- Media playback engine powered by the [mpv player](https://mpv.io) project.
- Modern cross-platform XAML engine powered by [Avalonia UI](https://avaloniaui.net).
