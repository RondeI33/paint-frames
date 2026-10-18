## Different FPS for each animation track

Animate slow and fast Paint layers together. Each track now has its own **FPS** column.

- Set each track to **1-12 FPS**, or **0** to follow **Default FPS**.
- Run a slow track at **2 FPS** alongside another at **8 FPS**.
- Loop counts and **Start after** handoffs respect each track's speed.
- Elapsed-time scheduling keeps independent tracks in sync. Paint may skip intermediate frames when it cannot keep up.
- **F6/F7** step between scheduled frame changes across the tracks.
- Save/load preserves per-track FPS. Existing v0.2 setup files inherit their saved default speed automatically.
- Single-loop mode, multi-layer frames, exclusions, and Hide/Hold last remain available.

**Use it:** open **Layered animation**, edit the **FPS** column for each track, and press **F8**. Leave **Start after** blank for tracks that should play together. Keep Paint's Layers panel open; **F9** restores original visibility.

Download **PaintFrames.exe** directly, or the Windows ZIP with instructions. Close the previous helper before starting the new version.

Validation: application compilation and focused scheduler/editor checks, including 2/8 FPS concurrency, elapsed-time deadlines, speed-aware handoffs, manual stepping, saved FPS values, and v0.2 setup compatibility. A full live Paint playback test was not performed.

Windows 11 with Paint Layers is required. ARM64 remains untested. The executable is unsigned. GitHub Actions packages the locally built v0.3 executable and verifies its checksum; it does not rebuild it. This is a preview helper, with no GIF/video export.
