## Layered animation

Animate several Paint layer tracks together, or switch between them after a chosen number of complete loops.

- New **Layered animation** tab with named tracks, layer/frame ranges, cycle counts, and **Start after** links.
- **Your example** preset: layers **1-6 three times**, then **1-3 once**, then **4-8 forever**. Requires eight Paint layers.
- Tracks can run together. Their visible layers are combined, including shared layers.
- **Hide** a completed track or **Hold last** to keep its last frame on screen.
- Compose a frame from several layers with `1+7,2+8,3+9`.
- Repeat the whole finite sequence, or stop when it ends. **0 cycles** loops a track forever.
- Save/load animation setups and playback speed as XML.
- Existing single-loop mode and exclusions remain available.

**Use it:** keep Paint's Layers panel open, open **Layered animation**, click **Your example**, adjust the layer numbers, then press **F8**. Layer numbers follow the top-to-bottom Paint list. **F6/F7** step backward/forward; **F9** restores original visibility.

Download **PaintFrames.exe** directly, or the Windows ZIP with instructions. Close the previous helper before starting the new version.

Validation: application compilation plus a focused scheduler/editor check covering exact repeat counts and handoffs, concurrent/composite frames, hold/hide behavior, backgrounds, wrapping/stopping, circular dependency rejection, and setup round-trip. A broad live Paint test was not performed.

Windows 11 with Paint Layers is required. ARM64 remains untested. The executable is unsigned. GitHub Actions packages the locally built v0.2 executable and verifies its checksum; it does not rebuild it. This is a preview helper, with no GIF/video export.
