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
    static void Visible(CompiledTimeline timeline, long tick, bool[] original, params int[] expected)
    {
        string description;
        var mask = timeline.Visibility(tick, original, out description);
        var actual = Enumerable.Range(0, mask.Length).Where(i => mask[i]).Select(i => i + 1);
        Require(actual.SequenceEqual(expected.OrderBy(i => i)), "Wrong visibility at tick " + tick + ": " + String.Join(",", actual));
    }
    [STAThread] static void Main(string[] args)
    {
        var sequence = new AnimationDefinition();
        sequence.Tracks.Add(new AnimationTrack { Name = "A", Layers = "1-6", Cycles = 3 });
        sequence.Tracks.Add(new AnimationTrack { Name = "B", Layers = "1-3", Cycles = 1, StartAfter = "A" });
        sequence.Tracks.Add(new AnimationTrack { Name = "C", Layers = "4-8", Cycles = 0, StartAfter = "B" });
        var compiled = new CompiledTimeline(sequence, 9);
        var original = new bool[9]; original[0] = original[8] = true;
        Visible(compiled, 0, original, 1, 9); Visible(compiled, 17, original, 6, 9);
        Visible(compiled, 18, original, 1, 9); Visible(compiled, 20, original, 3, 9);
        Visible(compiled, 21, original, 4, 9); Visible(compiled, 25, original, 8, 9);
        Visible(compiled, 26, original, 4, 9);

        var together = new AnimationDefinition();
        together.Tracks.Add(new AnimationTrack { Name = "Cel", Layers = "1+3,2+4", Cycles = 1, HoldLast = true });
        together.Tracks.Add(new AnimationTrack { Name = "Overlay", Layers = "3-4", Cycles = 0 });
        var combined = new CompiledTimeline(together, 5);
        Visible(combined, 0, new bool[5], 1, 3); Visible(combined, 1, new bool[5], 2, 4);
        Visible(combined, 2, new bool[5], 2, 3, 4);

        var finite = new AnimationDefinition();
        finite.Tracks.Add(new AnimationTrack { Name = "Only", Layers = "2-1", Cycles = 2 });
        var looping = new CompiledTimeline(finite, 2);
        Require(looping.Move(3, 1) == 0 && looping.Move(0, -1) == 3, "Whole sequence wrap failed");
        finite.RepeatSequence = false;
        var stopped = new CompiledTimeline(finite, 2);
        Require(stopped.Finished(stopped.Move(3, 1)), "Finite sequence did not finish");
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

        // Construct and render just the helper controls, without showing a window.
        Application.EnableVisualStyles();
        using (var host = new Form { ClientSize = new Size(920, 388) })
        using (var editor = new TrackEditor())
        {
            editor.CurrentSpeed = () => 4;
            host.Controls.Add(editor); host.CreateControl(); editor.CreateControl();
            editor.UpdateLayers(Enumerable.Range(1, 9).Select(i => i + ". Paint layer " + i).ToArray());
            typeof(TrackEditor).GetMethod("LoadExample", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editor, null);
            var actualExample = new CompiledTimeline(editor.ReadDefinition(), 9);
            Visible(actualExample, 18, original, 1, 9); Visible(actualExample, 21, original, 4, 9);
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
