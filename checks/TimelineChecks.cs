using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Xml.Serialization;

// Focused regression check. No Paint window or drawing is accessed.
static class TimelineChecks
{
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static long Time(double seconds) { return (long)Math.Round(seconds * CompiledTimeline.TimeUnitsPerSecond); }
    static void Visible(CompiledTimeline timeline, double seconds, bool[] original, params int[] expected)
    {
        long tick = Time(seconds);
        string description;
        var mask = timeline.Visibility(tick, original, out description);
        var actual = Enumerable.Range(0, mask.Length).Where(i => mask[i]).Select(i => i + 1);
        Require(actual.SequenceEqual(expected.OrderBy(i => i)), "Wrong visibility at tick " + tick + ": " + String.Join(",", actual));
    }
    [STAThread] static void Main(string[] args)
    {
        var sequence = new AnimationDefinition { FramesPerSecond = 1 };
        sequence.Tracks.Add(new AnimationTrack { Name = "A", Layers = "1-6", Cycles = 3 });
        sequence.Tracks.Add(new AnimationTrack { Name = "B", Layers = "1-3", Cycles = 1, StartAfter = "A" });
        sequence.Tracks.Add(new AnimationTrack { Name = "C", Layers = "4-8", Cycles = 0, StartAfter = "B" });
        var compiled = new CompiledTimeline(sequence, 9);
        var original = new bool[9]; original[0] = original[8] = true;
        Visible(compiled, 0, original, 1, 9); Visible(compiled, 17, original, 6, 9);
        Visible(compiled, 18, original, 1, 9); Visible(compiled, 20, original, 3, 9);
        Visible(compiled, 21, original, 4, 9); Visible(compiled, 25, original, 8, 9);
        Visible(compiled, 26, original, 4, 9);

        var together = new AnimationDefinition { FramesPerSecond = 1 };
        together.Tracks.Add(new AnimationTrack { Name = "Cel", Layers = "1+3,2+4", Cycles = 1, HoldLast = true });
        together.Tracks.Add(new AnimationTrack { Name = "Overlay", Layers = "3-4", Cycles = 0 });
        var combined = new CompiledTimeline(together, 5);
        Visible(combined, 0, new bool[5], 1, 3); Visible(combined, 1, new bool[5], 2, 4);
        Visible(combined, 2, new bool[5], 2, 3, 4);

        var finite = new AnimationDefinition { FramesPerSecond = 1 };
        finite.Tracks.Add(new AnimationTrack { Name = "Only", Layers = "2-1", Cycles = 2 });
        var looping = new CompiledTimeline(finite, 2);
        Require(looping.Move(Time(3), 1) == 0 && looping.Move(0, -1) == Time(3), "Whole sequence wrap failed");
        finite.RepeatSequence = false;
        var stopped = new CompiledTimeline(finite, 2);
        Require(stopped.Finished(stopped.Move(Time(3), 1)), "Finite sequence did not finish");
        Visible(stopped, 4, new bool[2]);
        bool rejected = false;
        finite.Tracks[0].StartAfter = "Only";
        try { new CompiledTimeline(finite, 2); } catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "Circular dependency was accepted");

        var serializer = new XmlSerializer(typeof(AnimationDefinition));
        var xml = new StringWriter(); serializer.Serialize(xml, sequence);
        var restored = (AnimationDefinition)serializer.Deserialize(new StringReader(xml.ToString()));
        Visible(new CompiledTimeline(restored, 9), 21, original, 4, 9);
        Console.WriteLine("PASS: repeat counts, handoffs, concurrent/composite frames, hold/hide, background preservation, wrap/stop, invalid dependency and setup round-trip.");

        var speeds = new AnimationDefinition();
        speeds.Tracks.Add(new AnimationTrack { Name = "Slow", Layers = "1-2", Cycles = 0, FramesPerSecond = 2 });
        speeds.Tracks.Add(new AnimationTrack { Name = "Fast", Layers = "3-6", Cycles = 0, FramesPerSecond = 8 });
        var varied = new CompiledTimeline(speeds, 6);
        Visible(varied, 0, new bool[6], 1, 3); Visible(varied, 0.125, new bool[6], 1, 4);
        Visible(varied, 0.375, new bool[6], 1, 6); Visible(varied, 0.5, new bool[6], 2, 3);
        Visible(varied, 0.625, new bool[6], 2, 4); Visible(varied, 1, new bool[6], 1, 3);
        Require(varied.Move(0, 1) == Time(0.125) && varied.Move(Time(0.2), -1) == 0, "Unequal-speed manual stepping failed");
        Require(Math.Abs(varied.NextChangeMilliseconds(Time(0.2)) - 50) < 0.001, "Next frame deadline is wrong");

        var handoff = new AnimationDefinition { RepeatSequence = false };
        handoff.Tracks.Add(new AnimationTrack { Name = "First", Layers = "1-3", Cycles = 3, FramesPerSecond = 2 });
        handoff.Tracks.Add(new AnimationTrack { Name = "Then", Layers = "4-5", Cycles = 1, FramesPerSecond = 8, StartAfter = "First" });
        var timed = new CompiledTimeline(handoff, 5);
        Visible(timed, 4.49, new bool[5], 3); Visible(timed, 4.5, new bool[5], 4);
        Visible(timed, 4.625, new bool[5], 5); Visible(timed, 4.75, new bool[5]);
        Require(timed.Duration == Time(4.75) && timed.Finished(Time(4.75)), "Handoff duration ignored track FPS");
        handoff.RepeatSequence = true;
        Require(new CompiledTimeline(handoff, 5).NormalizeTime(Time(4.75)) == 0, "Timed sequence wrap failed");

        var savedRates = new StringWriter(); serializer.Serialize(savedRates, speeds);
        var loadedRates = (AnimationDefinition)serializer.Deserialize(new StringReader(savedRates.ToString()));
        Require(loadedRates.Tracks[0].FramesPerSecond == 2 && loadedRates.Tracks[1].FramesPerSecond == 8, "Per-track FPS did not survive save/load");
        Visible(new CompiledTimeline(loadedRates, 6), 0.625, new bool[6], 2, 4);
        var legacy = new AnimationDefinition { FramesPerSecond = 6 };
        legacy.Tracks.Add(new AnimationTrack { Layers = "1-2", Cycles = 0 });
        var oldXml = new StringWriter(); serializer.Serialize(oldXml, legacy);
        var oldSetup = (AnimationDefinition)serializer.Deserialize(new StringReader(oldXml.ToString().Replace("<FramesPerSecond>0</FramesPerSecond>", "")));
        Visible(new CompiledTimeline(oldSetup, 2), 1.0 / 6, new bool[2], 2);
        speeds.Tracks[0].FramesPerSecond = 13; rejected = false;
        try { new CompiledTimeline(speeds, 6); } catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "Unsupported track FPS was accepted");
        Console.WriteLine("PASS: 2/8 FPS concurrency, elapsed-time deadlines, speed-aware handoffs, manual stepping, per-track FPS save/load, v0.2 setup compatibility and FPS validation.");

        // Construct and render just the helper controls, without showing a window.
        Application.EnableVisualStyles();
        using (var host = new Form { ClientSize = new Size(920, 388), Font = new Font("Segoe UI", 10) })
        using (var editor = new TrackEditor())
        {
            editor.CurrentSpeed = () => 4;
            host.Controls.Add(editor); host.CreateControl(); editor.CreateControl();
            editor.UpdateLayers(Enumerable.Range(1, 9).Select(i => i + ". Paint layer " + i).ToArray());
            typeof(TrackEditor).GetMethod("LoadExample", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editor, null);
            var actualExample = new CompiledTimeline(editor.ReadDefinition(), 9);
            Visible(actualExample, 4.5, original, 1, 9); Visible(actualExample, 5.25, original, 4, 9);
            var grid = (DataGridView)typeof(TrackEditor).GetField("grid", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
            grid.Rows[0].Cells["Fps"].Value = "2"; grid.Rows[1].Cells["Fps"].Value = "8";
            var edited = editor.ReadDefinition();
            Require(edited.Tracks[0].FramesPerSecond == 2 && edited.Tracks[1].FramesPerSecond == 8 && edited.Tracks[1].StartAfter == "Loop 1", "FPS column readback shifted other settings");
            host.PerformLayout(); editor.PerformLayout();
            if (args.Length > 0)
            {
                host.Show(); Application.DoEvents();
                using (var bitmap = new Bitmap(editor.Width, editor.Height)) { editor.DrawToBitmap(bitmap, editor.ClientRectangle); bitmap.Save(args[0]); }
                host.Close();
            }
            Console.WriteLine("PASS: editor construction, example preset and settings readback.");
        }
    }
}
