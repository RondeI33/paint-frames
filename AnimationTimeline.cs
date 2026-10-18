using System;
using System.Collections.Generic;
using System.Linq;

// Pure playback scheduling; this file has no dependency on Paint or UI Automation.
public sealed class AnimationTrack
{
    public bool Enabled = true;
    public string Name = "Track 1";
    public string Layers = "1";
    public int Cycles = 1; // Zero means forever.
    public string StartAfter = "";
    public bool HoldLast;
}

public sealed class AnimationDefinition
{
    public bool RepeatSequence = true;
    public int FramesPerSecond = 4;
    public List<AnimationTrack> Tracks = new List<AnimationTrack>();
}

sealed class CompiledTimeline
{
    sealed class Track
    {
        public AnimationTrack Settings;
        public int[][] Frames;
        public long Start;
        public long Duration;
        public int Visit;
    }
    readonly List<Track> tracks;
    readonly bool repeat;
    public readonly bool[] ControlledLayers;
    public readonly long Duration; // -1 means an infinite track is present.

    public CompiledTimeline(AnimationDefinition definition, int layerCount)
    {
        if (definition == null || definition.Tracks == null || definition.Tracks.Count > 128)
            throw new InvalidOperationException("Use an animation setup with at most 128 tracks.");
        tracks = new List<Track>();
        var byName = new Dictionary<string, Track>(StringComparer.OrdinalIgnoreCase);
        ControlledLayers = new bool[layerCount];
        foreach (var item in definition.Tracks)
        {
            if (item == null) throw new InvalidOperationException("The animation contains an invalid track.");
            if (!item.Enabled) continue;
            string name = (item.Name ?? "").Trim();
            if (name.Length == 0 || byName.ContainsKey(name))
                throw new InvalidOperationException("Every enabled track needs a different, nonempty name.");
            if (item.Cycles < 0 || item.Cycles > 1000000)
                throw new InvalidOperationException(name + ": cycles must be 0 (forever) or 1-1000000.");
            var track = new Track { Settings = item, Frames = ParseFrames(item.Layers, layerCount) };
            track.Duration = item.Cycles == 0 ? -1 : checked((long)track.Frames.Length * item.Cycles);
            byName.Add(name, track); tracks.Add(track);
            foreach (var frame in track.Frames) foreach (int layer in frame) ControlledLayers[layer] = true;
        }
        if (tracks.Count == 0) throw new InvalidOperationException("Enable at least one animation track.");
        foreach (var track in tracks) ResolveStart(track, byName);
        Duration = tracks.Any(x => x.Duration < 0) ? -1 : tracks.Max(x => checked(x.Start + x.Duration));
        repeat = definition.RepeatSequence && Duration > 0;
    }

    static void ResolveStart(Track track, Dictionary<string, Track> byName)
    {
        if (track.Visit == 2) return;
        if (track.Visit == 1) throw new InvalidOperationException("Start after links form a cycle. A track cannot eventually wait for itself.");
        track.Visit = 1;
        string after = (track.Settings.StartAfter ?? "").Trim();
        if (after.Length > 0)
        {
            Track parent;
            if (!byName.TryGetValue(after, out parent))
                throw new InvalidOperationException(track.Settings.Name + ": Start after must name an enabled track (" + after + " was not found).");
            ResolveStart(parent, byName);
            if (parent.Duration < 0)
                throw new InvalidOperationException(track.Settings.Name + " cannot start after " + after + " because that track loops forever. Give it a finite cycle count.");
            track.Start = checked(parent.Start + parent.Duration);
        }
        track.Visit = 2;
    }

    static int LayerNumber(string value, int count)
    {
        int number;
        if (!Int32.TryParse(value.Trim(), out number) || number < 1 || number > count)
            throw new InvalidOperationException("Layer '" + value + "' is outside the available range 1-" + count + ". Numbers refer to the top-to-bottom list.");
        return number - 1;
    }

    public static int[][] ParseFrames(string text, int layerCount)
    {
        if (String.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("Enter layers for each enabled track, such as 1-6 or 1+7,2+8.");
        if (text.Length > 100000) throw new InvalidOperationException("The frame list is too long.");
        var frames = new List<int[]>();
        foreach (string raw in text.Split(','))
        {
            string token = raw.Trim();
            if (token.Contains("+"))
                frames.Add(token.Split('+').Select(x => LayerNumber(x, layerCount)).Distinct().ToArray());
            else if (token.Contains("-"))
            {
                var range = token.Split('-');
                if (range.Length != 2) throw new InvalidOperationException("Use a range like 1-6, or separate frames with commas.");
                int first = LayerNumber(range[0], layerCount), last = LayerNumber(range[1], layerCount);
                int step = first <= last ? 1 : -1;
                for (int layer = first; ; layer += step)
                {
                    frames.Add(new[] { layer });
                    if (frames.Count > 10000) throw new InvalidOperationException("Use at most 10000 frames per track.");
                    if (layer == last) break;
                }
            }
            else frames.Add(new[] { LayerNumber(token, layerCount) });
            if (frames.Count > 10000) throw new InvalidOperationException("Use at most 10000 frames per track.");
        }
        return frames.ToArray();
    }

    public long Move(long current, int direction)
    {
        if (current < 0) return direction < 0 && repeat ? Duration - 1 : 0;
        long next = Math.Max(0, current + direction);
        if (repeat) return current == 0 && direction < 0 ? Duration - 1 : next % Duration;
        return Duration < 0 ? next : Math.Min(next, Duration);
    }

    public bool Finished(long tick) { return !repeat && Duration >= 0 && tick >= Duration; }

    public bool[] Visibility(long tick, bool[] original, out string description)
    {
        if (original.Length != ControlledLayers.Length) throw new ArgumentException("Layer count changed.");
        var visible = new bool[original.Length];
        for (int i = 0; i < visible.Length; i++) visible[i] = !ControlledLayers[i] && original[i];
        var labels = new List<string>();
        foreach (var track in tracks)
        {
            long local = tick - track.Start;
            if (local < 0) continue;
            bool ended = track.Duration >= 0 && local >= track.Duration;
            if (ended && !track.Settings.HoldLast) continue;
            int frame = ended ? track.Frames.Length - 1 : (int)(local % track.Frames.Length);
            foreach (int layer in track.Frames[frame]) visible[layer] = true;
            labels.Add(track.Settings.Name + (ended ? " (held)" : " " + (frame + 1) + "/" + track.Frames.Length + " cycle " + (local / track.Frames.Length + 1) + "/" + (track.Duration < 0 ? "forever" : track.Settings.Cycles.ToString())));
        }
        description = Finished(tick) ? "Sequence finished" : String.Join(" | ", labels);
        return visible;
    }
}
