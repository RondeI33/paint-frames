# Paint Frames

**Use Microsoft Paint layers as animation frames on Windows 11.**

Step through your drawing, combine independent animation tracks, and chain loops together for layered cartoon animation. Keep backgrounds and reference layers visible while the animated layers change.

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

## Layered animation (new in v0.2)

Open the **Layered animation** tab. Each row describes a track. Paint layer numbers are shown on the right: **1 is the top Paint layer**, regardless of its name.

Click **Your example** to fill in this sequence (requires at least eight Paint layers):

| Track name | Layers / frames | Cycles | Start after | When done |
| --- | --- | --- | --- | --- |
| Loop 1 | `1-6` | `3` | *(blank)* | Hide |
| Loop 2 | `1-3` | `1` | Loop 1 | Hide |
| Loop 3 | `4-8` | `0` | Loop 2 | Hide |

This shows layers 1-6 three complete times, switches to 1-3 once, then plays 4-8 forever. An outgoing track set to **Hide** disappears when the next starts. Press **F8** to play, or **F7/F6** to inspect the sequence step by step.

- **Play together:** leave **Start after** blank for multiple tracks. Each cycles independently at the shared playback speed; their visible layers are combined.
- **Chain tracks:** enter another enabled track's name in **Start after**. The new track begins after that track completes all its cycles. Multiple tracks may start after the same track.
- **Cycles:** a positive number repeats that track that many times; **0** loops forever. A track cannot start after one that never finishes.
- **When done:** **Hide** clears the finished track; **Hold last** keeps its last frame visible alongside later tracks.
- **Multiple layers per frame:** `1+7,2+8,3+9` makes three frames, each showing two layers together. `1-6` is a sequential range; `6-1` plays backwards. Commas separate frames or ranges.
- **Repeat whole sequence:** restarts the complete sequence when all tracks are finite. With it unchecked, playback stops after the final cycle and applies each track's Hide/Hold last choice. An infinite track keeps the sequence running.
- **Backgrounds/exclusions:** layers not used by any enabled track keep their original visibility. The Single loop checklist applies only to Single loop mode.
- **Shared layers:** if two active tracks use the same Paint layer, it stays visible while either needs it. Paint's stacking order determines compositing.

**Save setup / Load setup** stores the tracks, their timing, whole-sequence repeat setting, and playback speed in a `.paintframes.xml` file. It stores layer positions, not artwork. Use the same layer order when loading it again. Editing a track pauses playback; the next step restores the old preview and starts the new settings from the beginning. Keep your Paint document open when restoring.

## Compatibility and limits


- Requires **Windows 11** and **Microsoft Paint with Layers**. Keep the Layers panel open.
- Uses Windows Forms, Windows UI Automation, and .NET Framework 4.x. [Windows 11 includes .NET Framework 4.8 or 4.8.1](https://learn.microsoft.com/en-us/dotnet/framework/install/).
- The supplied executable is a managed **AnyCPU** application. It is portable, but universal compatibility with every Windows 11 PC or Paint version is not guaranteed. **ARM64 has not been tested.**
- Paint integration has had limited testing on one Windows 11 installation. v0.2 adds a focused scheduler/editor smoke check, not a broad live Paint compatibility test. Paint updates can change its accessibility controls.
- The executable is unsigned. A PC's application-control policy may restrict unsigned applications.
- Playback speed depends on how quickly Paint updates its layers. Switching to another app pauses playback.
- This is a live preview helper. It does **not export GIFs or videos**.

The helper changes layer visibility and selection; it does not save, merge, delete, or edit drawing pixels. The displayed frame is selected so you can draw on it. Closing the helper restores the original visibility and selection when those layers are still available.

## Source and optional local build

`PaintFrames.cs` contains the Paint integration, `AnimationTimeline.cs` handles scheduling, and `TrackEditor.cs` contains the track editor. `Build.cmd` uses the .NET Framework compiler included with Windows and writes `PaintFrames.exe` in the repository root. A small focused check lives in `checks/TimelineChecks.cs`.

The release workflow packages the reviewed executable in `prebuilt/PaintFrames.exe` and never runs the compiler. v0.2 includes a newly built executable for the new animation features.

## Releases through GitHub Actions

Pushing a version tag such as `v0.2.0` runs the Windows-only release workflow. It:

1. Verifies `prebuilt/PaintFrames.exe` against `prebuilt/SHA256SUMS.txt`.
2. Packages that same executable with the usage instructions.
3. Publishes the executable, a Windows ZIP, and SHA-256 checksums to GitHub Releases.

To publish another version, update the reviewed source and prebuilt binary, refresh its checksum and `RELEASE-NOTES.md`, commit, then push a new version tag. A workflow rerun can finish uploading assets to an existing draft release; it does not overwrite a published release.

The repository contains no macOS or Linux builds because this helper controls the Windows Paint application.
