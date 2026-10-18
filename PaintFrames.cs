// Microsoft Paint frame-by-frame layer helper. No third-party dependencies.
// Compile with the included Build.cmd and the .NET Framework compiler in Windows.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyVersion("0.3.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("0.3.0.0")]

static class Native
{
    public delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
    public delegate bool WindowProc(IntPtr hwnd, IntPtr data);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool EnumWindows(WindowProc proc, IntPtr data);
    [DllImport("user32.dll")] public static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr module, uint thread);
    [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] public static extern IntPtr GetModuleHandle(string name);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);

    public static bool IsPaint(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return false;
        uint pid; GetWindowThreadProcessId(hwnd, out pid);
        try { using (var process = Process.GetProcessById((int)pid)) return process.ProcessName.Equals("mspaint", StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }
}

sealed class FrameEngine
{
    sealed class Layer
    {
        public AutomationElement Element;
        public TogglePattern Toggle;
        public string Id;
        public bool Visible { get { return Toggle.Current.ToggleState == ToggleState.On; } }
        public void SetVisible(bool visible)
        {
            if (Visible == visible) return;
            Toggle.Toggle();
            for (int i = 0; i < 30; i++) { if (Visible == visible) return; Thread.Sleep(10); }
            throw new InvalidOperationException("Paint did not change a layer's visibility. Close any Paint dialog and try again.");
        }
    }
    IntPtr window;
    string signature;
    bool[] original;
    int originalSelected = -1;
    int current = -1;
    CompiledTimeline timeline;
    long timelineTick = -1;
    public bool HasSession { get { return original != null; } }
    public bool HasFrame { get { return current >= 0 || timelineTick >= 0; } }
    public bool Completed { get { return timeline != null && timeline.Finished(timelineTick); } }
    public long PlaybackPosition { get { return Math.Max(0, timelineTick); } }
    public double NextChangeMilliseconds { get { return timeline == null ? 0 : timeline.NextChangeMilliseconds(timelineTick); } }
    public string[] FrameIds { get; private set; }
    public string[] FrameNames { get; private set; }

    List<Layer> Read(IntPtr hwnd)
    {
        if (!Native.IsPaint(hwnd)) throw new InvalidOperationException("Open Microsoft Paint and its Layers panel first.");
        var root = AutomationElement.FromHandle(hwnd);
        if (!root.Current.IsEnabled) throw new InvalidOperationException("Close the open Paint dialog first.");
        var list = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "layersList"));
        if (list == null || list.Current.IsOffscreen) throw new InvalidOperationException("Open the Layers panel in Paint, then try again.");
        var rows = list.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem));
        var layers = new List<Layer>();
        foreach (AutomationElement row in rows)
        {
            var button = row.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "HideShowLayerButton"));
            object toggle;
            if (button == null || !button.TryGetCurrentPattern(TogglePattern.Pattern, out toggle))
                throw new InvalidOperationException("A layer's visibility control is unavailable. Expand the Layers panel and try again.");
            layers.Add(new Layer { Element = row, Toggle = (TogglePattern)toggle, Id = String.Join(".", row.GetRuntimeId()) });
        }
        if (layers.Count == 0) throw new InvalidOperationException("No paint layers found. Open the Layers panel first.");
        return layers; // UI Automation child order is the displayed top-to-bottom order.
    }

    static string Signature(List<Layer> layers) { return String.Join("|", layers.Select(x => x.Id)); }
    static bool Selected(Layer layer)
    {
        object selection;
        return layer.Element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out selection) && ((SelectionItemPattern)selection).Current.IsSelected;
    }
    static void Select(Layer layer)
    {
        object selection;
        if (layer.Element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out selection)) ((SelectionItemPattern)selection).Select();
    }

    void Start(IntPtr hwnd, List<Layer> layers)
    {
        window = hwnd; signature = Signature(layers);
        original = layers.Select(x => x.Visible).ToArray();
        originalSelected = layers.FindIndex(Selected); current = -1;
        timeline = null; timelineTick = -1;
        LoadOptions(layers);
    }

    void LoadOptions(List<Layer> layers)
    {
        FrameIds = layers.Select(x => x.Id).ToArray();
        FrameNames = layers.Select((x, i) => (i + 1) + ". " + x.Element.Current.Name + (i == 0 ? "  (top)" : i == layers.Count - 1 ? "  (bottom)" : "")).ToArray();
    }

    public string Execute(IntPtr hwnd, string action, HashSet<string> excluded, AnimationDefinition definition, long playbackTime = 0)
    {
        if (action == "restore") return Restore();
        var layers = Read(hwnd);
        if (action == "scan")
        {
            if (HasSession && window == hwnd && signature == Signature(layers)) Restore();
            else if (HasSession && window != hwnd) Restore();
            original = null; current = -1; timeline = null; timelineTick = -1;
            LoadOptions(Read(hwnd));
            return "Found " + layers.Count + " layers. Uncheck any you want to exclude.";
        }
        if (!HasSession) Start(hwnd, layers);
        if (window != hwnd) throw new InvalidOperationException("Preview belongs to another Paint window. Restore it before switching documents.");
        if (signature != Signature(layers)) throw new InvalidOperationException("Layers changed. Click Re-scan to use their new order.");
        if (definition != null)
        {
            if (timeline == null) timeline = new CompiledTimeline(definition, layers.Count);
            long nextTick = action == "first" ? 0 : action == "playback" ? timeline.NormalizeTime(playbackTime) : timeline.Move(timelineTick, action == "previous" ? -1 : 1);
            string description;
            bool[] visible = timeline.Visibility(nextTick, original, out description);
            // Visibility is a union: one track never hides another track's current frame.
            for (int i = 0; i < layers.Count; i++) if (visible[i]) layers[i].SetVisible(true);
            for (int i = 0; i < layers.Count; i++) if (!visible[i]) layers[i].SetVisible(false);
            int selected = Array.FindIndex(visible, value => value);
            for (int i = 0; i < layers.Count; i++) if (visible[i] && timeline.ControlledLayers[i]) { selected = i; break; }
            if (selected >= 0) Select(layers[selected]);
            timelineTick = nextTick;
            return (timelineTick / (double)CompiledTimeline.TimeUnitsPerSecond).ToString("0.000") + "s: " + description;
        }
        var frames = Enumerable.Range(0, layers.Count).Where(i => !excluded.Contains(layers[i].Id)).ToList();
        if (frames.Count == 0) throw new InvalidOperationException("Check at least one layer to use as an animation frame.");
        int position = frames.IndexOf(current);
        int nextPosition = action == "first" ? 0 : action == "previous" ? (position < 0 ? frames.Count - 1 : (position + frames.Count - 1) % frames.Count) : (position + 1) % frames.Count;
        int next = frames[nextPosition];
        // Show the target first, then hide every other drawing layer. Background is separate.
        layers[next].SetVisible(true);
        for (int i = 0; i < layers.Count; i++) if (i != next) layers[i].SetVisible(excluded.Contains(layers[i].Id) && original[i]);
        Select(layers[next]);
        current = next;
        return "Frame " + (nextPosition + 1) + " / " + frames.Count + "   |   Layer " + (current + 1) + " of " + layers.Count;
    }

    public string Restore()
    {
        if (!HasSession) return "Nothing to restore yet. F7 starts at the top layer.";
        var layers = Read(window);
        if (Signature(layers) != signature) throw new InvalidOperationException("Layers changed, so the old visibility cannot be restored. Click Re-scan to start a new preview.");
        for (int i = 0; i < layers.Count; i++) layers[i].SetVisible(original[i]);
        if (originalSelected >= 0) Select(layers[originalSelected]);
        original = null; current = -1; timeline = null; timelineTick = -1;
        return "Original layer visibility restored. F7 starts again.";
    }
}

sealed class FrameWindow : Form
{
    readonly FrameEngine engine = new FrameEngine();
    readonly Label status = new Label();
    readonly Button play = new Button();
    readonly NumericUpDown fps = new NumericUpDown();
    readonly CheckedListBox choices = new CheckedListBox();
    readonly List<string> choiceIds = new List<string>();
    readonly TabControl modes = new TabControl();
    readonly TrackEditor trackEditor = new TrackEditor();
    readonly System.Windows.Forms.Timer clock = new System.Windows.Forms.Timer();
    readonly HashSet<int> held = new HashSet<int>();
    readonly Stopwatch playbackClock = new Stopwatch();
    readonly Native.HookProc callback;
    IntPtr hook, paint;
    bool busy, playing, closeAllowed, syncingChoices, configurationDirty;
    DateTime lastFrame = DateTime.MinValue;
    long playbackOrigin;
    double nextPlaybackChange;

    public FrameWindow()
    {
        Text = "Paint Frames 0.3 - Per-track FPS"; ClientSize = new Size(960, 690);
        Font = new Font("Segoe UI", 10); FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(246, 247, 250);
        var title = new Label { Text = "Paint Frames - layered animation", Location = new Point(20, 18), Size = new Size(700, 30), Font = new Font("Segoe UI", 15, FontStyle.Bold) };
        Controls.Add(title);
        Controls.Add(new Label { Text = "Keep Paint's Layers panel open. Shortcuts work in Paint.", Location = new Point(20, 58), Size = new Size(465, 25) });
        modes.SetBounds(20, 92, 920, 388);
        var single = new TabPage("Single loop"); var layered = new TabPage("Layered animation");
        modes.TabPages.Add(single); modes.TabPages.Add(layered); Controls.Add(modes);
        single.Controls.Add(new Label { Text = "Use as animation frames (top to bottom):", Location = new Point(12, 12), Size = new Size(600, 25), Font = new Font("Segoe UI", 10, FontStyle.Bold) });
        choices.SetBounds(12, 44, 875, 230); choices.CheckOnClick = true; choices.IntegralHeight = false;
        choices.ItemCheck += async delegate
        {
            if (syncingChoices) return;
            Pause();
            if (engine.HasSession && !await Run("restore")) return;
            status.Text = "Frame selection updated. Press F7 to start from the top.";
        };
        single.Controls.Add(choices);
        single.Controls.Add(new Label { Text = "Unchecked layers keep their visibility from before playback.\nLeave a background visible, then uncheck it here to keep it on.", Location = new Point(12, 286), Size = new Size(850, 50) });
        layered.Controls.Add(trackEditor);
        trackEditor.CurrentSpeed = () => (int)fps.Value;
        trackEditor.SpeedLoaded += value => fps.Value = value;
        trackEditor.PauseRequested += Pause;
        trackEditor.ConfigurationChanged += SettingsChanged;
        modes.SelectedIndexChanged += delegate { SettingsChanged(); };
        AddButton("Previous  F6", 20, 495, 145, "previous");
        AddButton("Next  F7", 186, 495, 145, "next");
        play.Text = "Play  F8"; play.SetBounds(352, 495, 145, 38);
        play.Click += async delegate { await TogglePlayback(); }; Controls.Add(play);
        AddButton("First frame", 20, 543, 145, "first");
        AddButton("Restore  F9", 186, 543, 145, "restore");
        AddButton("Re-scan", 352, 543, 145, "scan");
        Controls.Add(new Label { Text = "Default FPS", Location = new Point(20, 597), Size = new Size(120, 25) });
        fps.SetBounds(145, 594, 55, 28); fps.Minimum = 1; fps.Maximum = 12; fps.Value = 4; Controls.Add(fps);
        fps.ValueChanged += delegate { if (modes.SelectedIndex == 1) SettingsChanged(); };
        Controls.Add(new Label { Text = "Single loop / tracks with FPS = 0. Paint may be slower.", Location = new Point(210, 597), Size = new Size(700, 25) });
        status.SetBounds(20, 634, 920, 50); status.Text = "Click Paint, then press F7 to begin."; Controls.Add(status);
        callback = Keyboard;
        hook = Native.SetWindowsHookEx(13, callback, Native.GetModuleHandle(null), 0);
        if (hook == IntPtr.Zero) status.Text = "Keyboard shortcuts unavailable. Use the buttons instead.";
        clock.Interval = 15;
        clock.Tick += async delegate
        {
            IntPtr front = Native.GetForegroundWindow();
            if (Native.IsPaint(front) && !playing) paint = front;
            if (playing)
            {
                // Pause when the user switches away from the preview's Paint window.
                if (front != paint && front != Handle) { Pause(); status.Text += " - paused"; }
                else if (!busy && modes.SelectedIndex == 1)
                {
                    double elapsed = playbackClock.Elapsed.TotalMilliseconds;
                    if (elapsed >= nextPlaybackChange)
                    {
                        long time = playbackOrigin + (long)(elapsed * CompiledTimeline.TimeUnitsPerSecond / 1000.0);
                        await Run("playback", time);
                        nextPlaybackChange = elapsed + engine.NextChangeMilliseconds;
                    }
                }
                else if (!busy && (DateTime.UtcNow - lastFrame).TotalMilliseconds >= 1000.0 / (double)fps.Value) await Run("next");
            }
        };
        clock.Start();
        Shown += async delegate { await Run("scan"); };
        FormClosing += OnClosing;
        FormClosed += delegate { clock.Dispose(); if (hook != IntPtr.Zero) Native.UnhookWindowsHookEx(hook); };
    }

    void AddButton(string text, int x, int y, int width, string action)
    {
        var button = new Button { Text = text }; button.SetBounds(x, y, width, 38);
        button.Click += async delegate { Pause(); await Run(action); }; Controls.Add(button);
    }
    IntPtr FindPaint()
    {
        if (Native.IsPaint(paint)) return paint;
        var candidates = new List<IntPtr>();
        Native.EnumWindows(delegate(IntPtr hwnd, IntPtr data) { if (Native.IsWindowVisible(hwnd) && Native.IsPaint(hwnd)) candidates.Add(hwnd); return true; }, IntPtr.Zero);
        if (candidates.Count != 1) throw new InvalidOperationException("Click the Paint window you want to animate, then press F7.");
        paint = candidates[0]; return paint;
    }
    IntPtr Keyboard(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0)
        {
            int key = Marshal.ReadInt32(data); int kind = message.ToInt32();
            bool down = kind == 0x100 || kind == 0x104;
            bool up = kind == 0x101 || kind == 0x105;
            if (key >= (int)Keys.F6 && key <= (int)Keys.F9)
            {
                if (up && held.Remove(key)) return (IntPtr)1;
                IntPtr front = Native.GetForegroundWindow();
                bool modified = (Native.GetAsyncKeyState(0x10) & 0x8000) != 0 || (Native.GetAsyncKeyState(0x11) & 0x8000) != 0 || (Native.GetAsyncKeyState(0x12) & 0x8000) != 0;
                if (down && !modified && Native.IsPaint(front))
                {
                    if (held.Add(key)) BeginInvoke(new Action(async delegate
                    {
                        if (playing && paint != front) Pause();
                        paint = front;
                        if (key == (int)Keys.F8) await TogglePlayback();
                        else { Pause(); await Run(key == (int)Keys.F6 ? "previous" : key == (int)Keys.F7 ? "next" : "restore"); }
                    }));
                    return (IntPtr)1;
                }
            }
        }
        return Native.CallNextHookEx(hook, code, message, data);
    }
    void Pause() { playing = false; playbackClock.Stop(); play.Text = "Play  F8"; }
    void SettingsChanged()
    {
        Pause(); configurationDirty = true;
        status.Text = "Settings changed. The next step restarts the preview. Unlisted layers keep their original visibility.";
    }
    async Task TogglePlayback()
    {
        if (playing) { Pause(); return; }
        if (busy) return;
        if ((!engine.HasFrame || engine.Completed || configurationDirty) && !await Run("first")) return;
        playbackOrigin = engine.PlaybackPosition;
        nextPlaybackChange = engine.NextChangeMilliseconds;
        playbackClock.Restart();
        playing = true; lastFrame = DateTime.UtcNow; play.Text = "Pause  F8";
    }
    async Task<bool> Run(string action, long playbackTime = 0)
    {
        if (busy) return false;
        busy = true; modes.Enabled = false;
        try
        {
            bool frameAction = action == "next" || action == "previous" || action == "first" || action == "playback";
            AnimationDefinition definition = frameAction && modes.SelectedIndex == 1 ? trackEditor.ReadDefinition() : null;
            IntPtr target = action == "restore" ? IntPtr.Zero : FindPaint();
            var excluded = new HashSet<string>();
            for (int i = 0; i < choiceIds.Count; i++) if (!choices.GetItemChecked(i)) excluded.Add(choiceIds[i]);
            if (frameAction && configurationDirty)
            {
                if (engine.HasSession) await Task.Run(() => engine.Restore());
                configurationDirty = false;
            }
            status.Text = await Task.Run(() => engine.Execute(target, action, excluded, definition, playbackTime));
            RefreshChoices();
            lastFrame = DateTime.UtcNow;
            if (engine.Completed) Pause();
            return true;
        }
        catch (Exception error) { Pause(); status.Text = error.Message; return false; }
        finally { busy = false; modes.Enabled = true; }
    }
    void RefreshChoices()
    {
        if (engine.FrameIds == null || choiceIds.SequenceEqual(engine.FrameIds)) return;
        var checks = new Dictionary<string, bool>();
        for (int i = 0; i < choiceIds.Count; i++) checks[choiceIds[i]] = choices.GetItemChecked(i);
        syncingChoices = true;
        try
        {
            choices.Items.Clear(); choiceIds.Clear();
            for (int i = 0; i < engine.FrameIds.Length; i++)
            {
                string id = engine.FrameIds[i]; choiceIds.Add(id);
                choices.Items.Add(engine.FrameNames[i], !checks.ContainsKey(id) || checks[id]);
            }
        }
        finally { syncingChoices = false; }
        trackEditor.UpdateLayers(engine.FrameNames);
    }
    async void OnClosing(object sender, FormClosingEventArgs e)
    {
        if (closeAllowed) return;
        e.Cancel = true; Pause();
        while (busy) await Task.Delay(50);
        if (engine.HasSession && !await Run("restore"))
        {
            var choice = MessageBox.Show(this, "Could not restore visibility automatically:\n\n" + status.Text + "\n\nExit anyway? Your drawing pixels are unchanged.", "Paint Frames", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (choice != DialogResult.Yes) return;
        }
        closeAllowed = true; Close();
    }
}

static class Program
{
    [STAThread] static void Main()
    {
        bool created;
        using (var mutex = new Mutex(true, "Local\\PaintFramesHelper-v1", out created))
        {
            if (!created) { MessageBox.Show("Paint Frames is already running. Use F6-F9 in Paint.", "Paint Frames"); return; }
            Native.SetProcessDPIAware(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FrameWindow());
        }
    }
}
