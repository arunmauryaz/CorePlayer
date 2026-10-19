# CORE PLAYER 🎬

A minimal, monochrome-first media player engineered with **.NET 8 (C#)** and **Avalonia UI**, powered by the **libmpv** rendering core. Built for unobtrusive chrome where controls never compete with the video content.

---

## 🚀 Quick Launch

Launch CORE PLAYER with **one click**:

👉 **Double-click [`run.bat`](file:///d:/CORE%20PLAYER/run.bat)** in the root folder.

Or from terminal:
```bash
.\run.bat
```

---

## 🎨 Design Philosophy (IINA / mpv Restraint)

- **Monochrome-First**: Pitch-black canvas (`#000000`) and neutral monochrome controls (`#E8E8E8` / `#8A8A8A`). The video content is the only color on screen.
- **Zero Decorative Noise**: No glows, no blur halos, no glassmorphism cards, and no marketing badges.
- **Chrome That Stays Invisible**: Dense, flat control row with a functional bottom scrim gradient that auto-hides during playback.
- **Monospace Timecodes**: Rendered in tabular figures (`Consolas` / `monospace`) to prevent number jitter.
- **Quiet Title Bar**: Clean icon + file name + window controls. All secondary tools (PiP, Pin) live in the control bar.
- **Active Drag Affordance**: A subtle 1px border appears only during an active file drag-over, never persistent.

---

## ✨ Features & Shortcuts

### 1. 🎯 Smart Auto-Resume
- Remembers playback timestamps per video file.
- Quiet prompt appears on load: *“Resume from `00:24:12`? [Resume] [Start Over]”*.
- Resets automatically when completed.

### 2. 🎛️ Gestures & Controls
- **Right Half Vertical Scroll**: Volume control (0% – 150%) with quiet HUD.
- **Left Half Vertical Scroll**: Brightness adjustment (-100% to +100%).
- **Double Click**: Fullscreen toggle.
- **Single Click / Space**: Play / Pause toggle.
- **Arrow Keys / Scrub**: Seek backward / forward 10s.

### 3. 📂 Playback Queue
- **Drawer**: Click the queue button `(Q)` on the bottom right.
- **Auto-Queue**: Opening any episode scans and queues the remaining episodes in the folder.
- **Shuffle & Repeat**: One-click toggle in the drawer header.

### 4. 💬 Audio & Subtitles
- Track selector menu `(T)` for multi-audio and embedded/external subtitles.
- Subtitle sync offset adjustments (+/- 0.1s).
- Direct drag-and-drop support for `.srt`, `.ass`, `.vtt` files.

---

## ⌨️ Keyboard Shortcuts Reference

| Shortcut | Action |
| :--- | :--- |
| `Space` | Play / Pause |
| `Left Arrow` / `Right Arrow` | Seek -10s / +10s |
| `Up Arrow` / `Down Arrow` | Volume Up / Down (+/- 5%) |
| `F` / `F11` / `Double Click` | Fullscreen Toggle |
| `M` | Mute / Unmute |
| `Q` | Open / Close Queue Drawer |
| `T` | Open Audio & Subtitle Tracks |
| `I` | Media & Codec Info |
| `S` | Take Instant Snapshot |
| `.` / `,` | Frame Step Forward / Backward |
| `Ctrl + Right` / `Ctrl + Left` | Next / Previous Video in Queue |
