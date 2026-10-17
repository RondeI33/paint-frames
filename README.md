# Paint Frames

**Use Microsoft Paint layers as animation frames on Windows 11.**

Step through your drawing from top to bottom, play it in a loop, and exclude backgrounds or reference layers with a checkbox.

[Download PaintFrames.exe](https://github.com/RondeI33/paint-frames/releases/latest/download/PaintFrames.exe) · [Releases](https://github.com/RondeI33/paint-frames/releases) · [Report an issue](https://github.com/RondeI33/paint-frames/issues)

## Get started

1. Download **PaintFrames.exe** from the latest release. The executable runs without an installer or AutoHotkey.
2. Open a drawing in Microsoft Paint and open the **Layers** panel.
3. Run Paint Frames. In its checklist, leave your animation frames checked and uncheck any layers you want to exclude.
4. Click Paint and press **F7** to show the first checked layer.

| Key | Action |
| --- | --- |
| **F7** | Next frame; the first press starts at the top checked layer |
| **F6** | Previous frame |
| **F8** | Play or pause |
| **F9** | Stop and restore the original layer visibility |

Shortcuts apply while Paint is active. Some laptop keyboards require **Fn** with the function keys. The helper also has buttons for these actions, **First frame**, **Re-scan**, and a playback-speed control.

## Exclude backgrounds and reference layers

- **Checked:** the layer participates in the animation. One checked layer is shown at a time.
- **Unchecked:** the layer keeps its visibility from before the preview. Visible excluded layers stay visible; hidden excluded layers stay hidden.

For a permanent background, show that layer in Paint and uncheck it in the helper before starting. To change an excluded layer's visibility later, press **F9**, change its eye button in Paint, and start again.

Changing a checkbox pauses playback and restores the previous visibility. Press **F7** to begin with the new selection. The checklist follows Paint's top-to-bottom order, even when Paint's layer names count backwards.

Exclusions last for the current helper session and are not saved into the image. After adding, deleting, or reordering layers, use **Re-scan**. Restore first if you want to preserve the original visibility arrangement. Paint's separate background swatch is left unchanged.

## Compatibility and limits

- Requires **Windows 11** and **Microsoft Paint with Layers**. Keep the Layers panel open.
- Uses Windows Forms, Windows UI Automation, and .NET Framework 4.x. [Windows 11 includes .NET Framework 4.8 or 4.8.1](https://learn.microsoft.com/en-us/dotnet/framework/install/).
- The supplied executable is a managed **AnyCPU** application. It is portable, but universal compatibility with every Windows 11 PC or Paint version is not guaranteed. **ARM64 has not been tested.**
- This initial release has had limited testing on one Windows 11 installation. Paint updates can change the accessibility controls the helper uses.
- The executable is unsigned. A PC's application-control policy may restrict unsigned applications.
- Playback speed depends on how quickly Paint updates its layers. Switching to another app pauses playback.
- This is a live preview helper. It does **not export GIFs or videos**.

The helper changes layer visibility and selection; it does not save, merge, delete, or edit drawing pixels. The displayed frame is selected so you can draw on it. Closing the helper restores the original visibility and selection when those layers are still available.

## Source and optional local build

`PaintFrames.cs` contains the application source. `Build.cmd` is an optional local build command using the .NET Framework compiler included with Windows. It writes `PaintFrames.exe` in the repository root.

The published initial release uses the **existing executable** in `prebuilt/PaintFrames.exe`. The release workflow never runs the compiler.

## Releases through GitHub Actions

Pushing a version tag such as `v0.1.0` runs the Windows-only release workflow. It:

1. Verifies `prebuilt/PaintFrames.exe` against `prebuilt/SHA256SUMS.txt`.
2. Packages that same executable with the usage instructions.
3. Publishes the executable, a Windows ZIP, and SHA-256 checksums to GitHub Releases.

To publish another version, update the reviewed source and prebuilt binary, refresh its checksum and `RELEASE-NOTES.md`, commit, then push a new version tag. A workflow rerun can finish uploading assets to an existing draft release; it does not overwrite a published release.

The repository contains no macOS or Linux builds because this helper controls the Windows Paint application.
