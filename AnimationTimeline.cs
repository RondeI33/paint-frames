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
    public int FramesPerSecond; // Zero inherits the setup default, including v0.2 XML files.
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
    // LCM(1..12): each supported FPS has an exact integral frame duration.
    public const long TimeUnitsPerSecond = 27720;
    sealed class Track
    {
        public AnimationTrack Settings;
        public int[][] Frames;
        public long Start;
        public long Duration;
        public long FrameDuration;
        public int FramesPerSecond;
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
        if (definition.FramesPerSecond < 1 || definition.FramesPerSecond > 12)
            throw new InvalidOperationException("Default FPS must be between 1 and 12.");
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
            track.FramesPerSecond = item.FramesPerSecond == 0 ? definition.FramesPerSecond : item.FramesPerSecond;
            if (track.FramesPerSecond < 1 || track.FramesPerSecond > 12)
                throw new InvalidOperationException(name + ": FPS must be 1-12, or 0 to use Default FPS.");
            track.FrameDuration = TimeUnitsPerSecond / track.FramesPerSecond;
            track.Duration = item.Cycles == 0 ? -1 : checked((long)track.Frames.Length * item.Cycles * track.FrameDuration);
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
        if (current < 0) return direction < 0 && repeat ? BoundaryAtOrBefore(Duration - 1) : 0;
        if (direction >= 0) return NormalizeTime(BoundaryAfter(current));
        long displayedFrame = BoundaryAtOrBefore(current);
        if (displayedFrame == 0) return repeat ? BoundaryAtOrBefore(Duration - 1) : 0;
        return BoundaryAtOrBefore(displayedFrame - 1);
    }

    long BoundaryAfter(long current)
    {
        long next = Int64.MaxValue;
        foreach (var track in tracks)
        {
            if (current < track.Start) { next = Math.Min(next, track.Start); continue; }
            if (track.Duration >= 0 && current >= track.Start + track.Duration) continue;
            long frame = (current - track.Start) / track.FrameDuration + 1;
            next = Math.Min(next, checked(track.Start + frame * track.FrameDuration));
        }
        return next == Int64.MaxValue ? current : next;
    }

    long BoundaryAtOrBefore(long current)
    {
        long previous = 0;
        foreach (var track in tracks)
        {
            if (current < track.Start) continue;
            long local = current - track.Start;
            if (track.Duration >= 0) local = Math.Min(local, track.Duration);
            previous = Math.Max(previous, track.Start + local / track.FrameDuration * track.FrameDuration);
        }
        return previous;
    }

    public long NormalizeTime(long time)
    {
        time = Math.Max(0, time);
        return repeat ? time % Duration : Duration < 0 ? time : Math.Min(time, Duration);
    }

    public double NextChangeMilliseconds(long current)
    {
        return Finished(current) ? Double.PositiveInfinity : (BoundaryAfter(current) - current) * 1000.0 / TimeUnitsPerSecond;
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
            long elapsedFrames = local / track.FrameDuration;
            int frame = ended ? track.Frames.Length - 1 : (int)(elapsedFrames % track.Frames.Length);
            foreach (int layer in track.Frames[frame]) visible[layer] = true;
            labels.Add(track.Settings.Name + (ended ? " (held)" : " " + (frame + 1) + "/" + track.Frames.Length + " @" + track.FramesPerSecond + "fps cycle " + (elapsedFrames / track.Frames.Length + 1) + "/" + (track.Duration < 0 ? "forever" : track.Settings.Cycles.ToString())));
        }
        description = Finished(tick) ? "Sequence finished" : String.Join(" | ", labels);
        return visible;
    }
}
