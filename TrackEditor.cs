using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

sealed class TrackEditor : UserControl
{
    readonly DataGridView grid = new DataGridView();
    readonly CheckBox repeat = new CheckBox();
    readonly ListBox layerList = new ListBox();
    bool updating;
    public event Action ConfigurationChanged;
    public event Action PauseRequested;
    public event Action<int> SpeedLoaded;
    public Func<int> CurrentSpeed;

    public TrackEditor()
    {
        Dock = DockStyle.Fill;
        var hint = new Label { Dock = DockStyle.Top, Height = 53, Padding = new Padding(8), Text = "Layers: 1-6 = six frames; 1+7,2+8 = layered frames. FPS: 1-12 per track; 0 = Default FPS.\nStart after: blank = together; a track name = wait for its cycles to finish. Cycles: 0 = forever." };
        var split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel2, Size = new Size(930, 280), Panel2MinSize = 180, SplitterDistance = 700 };
        grid.Dock = DockStyle.Fill; grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; grid.RowHeadersVisible = false;
        grid.BackgroundColor = Color.White; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "On", FillWeight = 35 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Track", HeaderText = "Track name", FillWeight = 110 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Layers", HeaderText = "Layers / frames", FillWeight = 150 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Cycles", HeaderText = "Cycles", FillWeight = 60 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Fps", HeaderText = "FPS", FillWeight = 50 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "After", HeaderText = "Start after", FillWeight = 110 });
        var ending = new DataGridViewComboBoxColumn { Name = "End", HeaderText = "When done", FillWeight = 95 };
        ending.Items.AddRange("Hide", "Hold last"); grid.Columns.Add(ending);
        grid.CurrentCellDirtyStateChanged += delegate { if (grid.IsCurrentCellDirty && (grid.CurrentCell is DataGridViewCheckBoxCell || grid.CurrentCell is DataGridViewComboBoxCell)) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        grid.CellValueChanged += delegate { Changed(); };
        grid.CellBeginEdit += delegate { if (PauseRequested != null) PauseRequested(); };
        grid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };
        split.Panel1.Controls.Add(grid);
        layerList.Dock = DockStyle.Fill; layerList.HorizontalScrollbar = true;
        split.Panel2.Controls.Add(layerList);
        split.Panel2.Controls.Add(new Label { Text = "Paint layer numbers", Dock = DockStyle.Top, Height = 24 });
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 76, Padding = new Padding(4) };
        AddButton(bottom, "Add track", delegate { grid.Rows.Add(true, "Track " + (grid.Rows.Count + 1), "1", "1", "0", "", "Hide"); Changed(); });
        AddButton(bottom, "Remove", delegate { if (grid.CurrentRow != null) { grid.Rows.Remove(grid.CurrentRow); Changed(); } });
        AddButton(bottom, "Your example", LoadExample);
        AddButton(bottom, "Save setup", SaveSetup);
        AddButton(bottom, "Load setup", LoadSetup);
        repeat.Text = "Repeat whole sequence (when all tracks are finite)"; repeat.Width = 470; repeat.Checked = true;
        repeat.CheckedChanged += delegate { Changed(); }; bottom.Controls.Add(repeat);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 53));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        hint.Dock = DockStyle.Fill; split.Margin = Padding.Empty; bottom.Dock = DockStyle.Fill;
        layout.Controls.Add(hint, 0, 0); layout.Controls.Add(split, 0, 1); layout.Controls.Add(bottom, 0, 2);
        Controls.Add(layout);
    }

    void AddButton(Control parent, string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 29 };
        button.Click += delegate { if (PauseRequested != null) PauseRequested(); try { action(); } catch (Exception e) { MessageBox.Show(this, e.Message, "Animation setup", MessageBoxButtons.OK, MessageBoxIcon.Information); } };
        parent.Controls.Add(button);
    }
    void Changed() { if (!updating && ConfigurationChanged != null) ConfigurationChanged(); }

    public void UpdateLayers(string[] names)
    {
        layerList.Items.Clear(); if (names != null) layerList.Items.AddRange(names);
        if (grid.Rows.Count == 0 && names != null && names.Length > 0)
        {
            grid.Rows.Add(true, "Track 1", "1-" + names.Length, "0", "0", "", "Hide"); Changed();
        }
    }

    public AnimationDefinition ReadDefinition()
    {
        grid.EndEdit();
        var result = new AnimationDefinition { RepeatSequence = repeat.Checked, FramesPerSecond = CurrentSpeed == null ? 4 : CurrentSpeed() };
        foreach (DataGridViewRow row in grid.Rows)
        {
            int cycles;
            int rate;
            if (!Int32.TryParse(Convert.ToString(row.Cells["Cycles"].Value), out cycles)) throw new InvalidOperationException("Cycles must be a whole number; use 0 to loop forever.");
            if (!Int32.TryParse(Convert.ToString(row.Cells["Fps"].Value), out rate) || rate < 0 || rate > 12)
                throw new InvalidOperationException("Track FPS must be a whole number from 1 to 12, or 0 to use Default FPS.");
            result.Tracks.Add(new AnimationTrack {
                Enabled = Convert.ToBoolean(row.Cells["Enabled"].Value), Name = Convert.ToString(row.Cells["Track"].Value),
                Layers = Convert.ToString(row.Cells["Layers"].Value), Cycles = cycles, FramesPerSecond = rate,
                StartAfter = Convert.ToString(row.Cells["After"].Value), HoldLast = Convert.ToString(row.Cells["End"].Value) == "Hold last"
            });
        }
        return result;
    }
    void Apply(AnimationDefinition definition)
    {
        if (definition == null || definition.Tracks == null || definition.Tracks.Count > 128 || definition.Tracks.Contains(null)) throw new InvalidOperationException("Invalid animation setup.");
        updating = true;
        try
        {
            grid.Rows.Clear(); repeat.Checked = definition.RepeatSequence;
            foreach (var track in definition.Tracks) grid.Rows.Add(track.Enabled, track.Name, track.Layers, track.Cycles.ToString(), track.FramesPerSecond.ToString(), track.StartAfter, track.HoldLast ? "Hold last" : "Hide");
            if (SpeedLoaded != null) SpeedLoaded(Math.Max(1, Math.Min(12, definition.FramesPerSecond)));
        }
        finally { updating = false; }
        Changed();
    }
    void LoadExample()
    {
        Apply(new AnimationDefinition { Tracks = new List<AnimationTrack> {
            new AnimationTrack { Name = "Loop 1", Layers = "1-6", Cycles = 3 },
            new AnimationTrack { Name = "Loop 2", Layers = "1-3", Cycles = 1, StartAfter = "Loop 1" },
            new AnimationTrack { Name = "Loop 3", Layers = "4-8", Cycles = 0, StartAfter = "Loop 2" }
        }});
    }
    void SaveSetup()
    {
        var definition = ReadDefinition();
        using (var dialog = new SaveFileDialog { Filter = "Paint Frames setup (*.paintframes.xml)|*.paintframes.xml", FileName = "animation.paintframes.xml" })
            if (dialog.ShowDialog(this) == DialogResult.OK)
                using (var stream = File.Create(dialog.FileName)) new XmlSerializer(typeof(AnimationDefinition)).Serialize(stream, definition);
    }
    void LoadSetup()
    {
        using (var dialog = new OpenFileDialog { Filter = "Paint Frames setup (*.paintframes.xml)|*.paintframes.xml|XML files (*.xml)|*.xml" })
            if (dialog.ShowDialog(this) == DialogResult.OK)
                using (var reader = XmlReader.Create(dialog.FileName, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1000000 }))
                    Apply((AnimationDefinition)new XmlSerializer(typeof(AnimationDefinition)).Deserialize(reader));
    }
}
