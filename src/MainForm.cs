using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Micopy
{
    internal class MainForm : Form
    {
        private AppSettings _settings;
        private bool _loading;
        private bool _busy;

        private ListView _list;
        private ImageList _images;
        private TextBox _dest;
        private PictureBox _statusPic;
        private Label _status;
        private Button _add, _edit, _del, _destBtn, _run, _import, _export;

        public MainForm()
        {
            _settings = AppSettings.Load();
            BuildUi();
            RestoreWindow();
            RefreshAll();
        }

        // ======================================================
        // 画面の組み立て
        // ======================================================
        private void BuildUi()
        {
            Text = "micopy " + AppVersion.Text + " - ワンクリック差分バックアップ";
            Icon = Emoji.AppIcon;
            Font = Ui.MakeFont(10f, FontStyle.Regular);
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Ui.Back;
            ClientSize = new Size(Ui.S(860), Ui.S(600));
            MinimumSize = new Size(Ui.S(700), Ui.S(520));
            KeyPreview = true;

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.Padding = new Padding(Ui.S(18), Ui.S(14), Ui.S(18), Ui.S(16));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            // ---- ヘッダー ----
            TableLayoutPanel header = new TableLayoutPanel();
            header.Dock = DockStyle.Top;
            header.AutoSize = true;
            header.ColumnCount = 3;
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.Margin = new Padding(0, 0, 0, Ui.S(12));

            PictureBox logo = new PictureBox();
            logo.Image = Emoji.Logo(52);
            logo.SizeMode = PictureBoxSizeMode.AutoSize;
            logo.Margin = new Padding(0, 0, Ui.S(12), 0);
            header.Controls.Add(logo, 0, 0);

            FlowLayoutPanel titles = new FlowLayoutPanel();
            titles.FlowDirection = FlowDirection.TopDown;
            titles.AutoSize = true;
            titles.WrapContents = false;
            Label title = new Label();
            title.UseMnemonic = false;
            title.Text = "micopy";
            title.AutoSize = true;
            title.Font = Ui.MakeFont(20f, FontStyle.Bold);
            title.ForeColor = Ui.AccentDark;
            title.Margin = new Padding(0);
            Label sub = new Label();
            sub.UseMnemonic = false;
            sub.Text = "登録したフォルダを、ワンクリックで差分バックアップ";
            sub.AutoSize = true;
            sub.ForeColor = Ui.SubText;
            sub.Margin = new Padding(Ui.S(2), 0, 0, 0);
            FlowLayoutPanel titleRow = new FlowLayoutPanel();
            titleRow.AutoSize = true;
            titleRow.WrapContents = false;
            titleRow.Margin = new Padding(0);
            Label ver = new Label();
            ver.UseMnemonic = false;
            ver.Text = "v" + AppVersion.Text;
            ver.AutoSize = true;
            ver.Font = Ui.MakeFont(10f, FontStyle.Bold);
            ver.ForeColor = Ui.SubText;
            ver.Margin = new Padding(Ui.S(4), Ui.S(16), 0, 0);
            titleRow.Controls.Add(title);
            titleRow.Controls.Add(ver);
            titles.Controls.Add(titleRow);
            titles.Controls.Add(sub);
            header.Controls.Add(titles, 1, 0);

            FlowLayoutPanel io = new FlowLayoutPanel();
            io.AutoSize = true;
            io.WrapContents = false;
            io.Anchor = AnchorStyles.Right;
            _import = Ui.EmojiButton("import", "インポート", 132, 36);
            _import.Click += delegate { ImportSettings(); };
            _import.Margin = new Padding(0);
            _export = Ui.EmojiButton("export", "エクスポート", 140, 36);
            _export.Click += delegate { ExportSettings(); };
            _export.Margin = new Padding(Ui.S(6), 0, 0, 0);
            io.Controls.Add(_import);
            io.Controls.Add(_export);
            header.Controls.Add(io, 2, 0);
            root.Controls.Add(header, 0, 0);

            // ---- バックアップ元カード ----
            Panel srcCard = Card();
            srcCard.Dock = DockStyle.Fill;
            TableLayoutPanel st = new TableLayoutPanel();
            st.Dock = DockStyle.Fill;
            st.ColumnCount = 2;
            st.RowCount = 3;
            st.BackColor = Color.White;
            st.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            st.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            st.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            st.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            st.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            Control h1 = Ui.Heading("folder", "バックアップするフォルダ", 11f);
            st.Controls.Add(h1, 0, 0);
            st.SetColumnSpan(h1, 2);

            _images = new ImageList();
            _images.ColorDepth = ColorDepth.Depth32Bit;
            _images.ImageSize = new Size(Ui.S(20), Ui.S(20));
            _images.Images.Add("ok", Emoji.Get("folder", 20));
            _images.Images.Add("missing", Emoji.Get("warning", 20));
            _images.Images.Add("off", Emoji.Get("noentry", 20));

            _list = new ListView();
            _list.Dock = DockStyle.Fill;
            _list.View = View.Details;
            _list.CheckBoxes = true;
            _list.FullRowSelect = true;
            _list.HideSelection = false;
            _list.MultiSelect = false;
            _list.SmallImageList = _images;
            _list.BorderStyle = BorderStyle.FixedSingle;
            _list.AllowDrop = true;
            _list.Columns.Add("バックアップするフォルダ", Ui.S(400));
            _list.Columns.Add("保存フォルダ名", Ui.S(160));
            _list.Columns.Add("状態", Ui.S(100));
            _list.Margin = new Padding(0, 0, Ui.S(10), 0);
            _list.ItemChecked += OnItemChecked;
            _list.SelectedIndexChanged += delegate { UpdateButtons(); };
            _list.DoubleClick += delegate { EditSelected(); };
            _list.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Delete) DeleteSelected(); };
            _list.DragEnter += OnDragEnter;
            _list.DragDrop += OnDragDrop;
            _list.Resize += delegate { FitColumns(); };
            st.Controls.Add(_list, 0, 1);

            FlowLayoutPanel side = new FlowLayoutPanel();
            side.FlowDirection = FlowDirection.TopDown;
            side.AutoSize = true;
            side.WrapContents = false;
            side.Margin = new Padding(0);
            _add = Ui.EmojiButton("add", "追加", 112, 40);
            _add.Click += delegate { AddFolder(); };
            _edit = Ui.EmojiButton("edit", "編集", 112, 40);
            _edit.Click += delegate { EditSelected(); };
            _del = Ui.EmojiButton("trash", "削除", 112, 40);
            _del.Click += delegate { DeleteSelected(); };
            foreach (Button b in new[] { _add, _edit, _del })
            {
                b.Margin = new Padding(0, 0, 0, Ui.S(8));
                side.Controls.Add(b);
            }
            st.Controls.Add(side, 1, 1);

            Label hint = new Label();

            hint.UseMnemonic = false;
            hint.Text = "フォルダをこの一覧にドラッグ&ドロップしても追加できます。チェックを外したフォルダはバックアップしません。";
            hint.AutoSize = true;
            hint.ForeColor = Ui.SubText;
            hint.Font = Ui.MakeFont(9f, FontStyle.Regular);
            hint.Margin = new Padding(0, Ui.S(6), 0, 0);
            st.Controls.Add(hint, 0, 2);
            st.SetColumnSpan(hint, 2);
            srcCard.Controls.Add(st);
            root.Controls.Add(srcCard, 0, 1);

            // ---- バックアップ先カード ----
            Panel dstCard = Card();
            dstCard.Dock = DockStyle.Top;
            dstCard.AutoSize = true;
            dstCard.Margin = new Padding(0, Ui.S(12), 0, Ui.S(14));
            TableLayoutPanel dt = new TableLayoutPanel();
            dt.Dock = DockStyle.Top;
            dt.AutoSize = true;
            dt.ColumnCount = 2;
            dt.BackColor = Color.White;
            dt.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            dt.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Control h2 = Ui.Heading("floppy", "バックアップ先(1か所)", 11f);
            dt.Controls.Add(h2, 0, 0);
            dt.SetColumnSpan(h2, 2);
            _dest = new TextBox();
            _dest.ReadOnly = true;
            _dest.BackColor = Color.FromArgb(250, 251, 253);
            _dest.Dock = DockStyle.Fill;
            _dest.Font = Ui.MakeFont(11f, FontStyle.Regular);
            _dest.Margin = new Padding(0, Ui.S(5), Ui.S(10), 0);
            dt.Controls.Add(_dest, 0, 1);
            _destBtn = Ui.EmojiButton("open_folder", "変更…", 112, 38);
            _destBtn.Margin = new Padding(0);
            _destBtn.Click += delegate { ChangeDestination(); };
            dt.Controls.Add(_destBtn, 1, 1);
            dstCard.Controls.Add(dt);
            root.Controls.Add(dstCard, 0, 2);

            // ---- 下部(状態+実行ボタン) ----
            TableLayoutPanel bottom = new TableLayoutPanel();
            bottom.Dock = DockStyle.Top;
            bottom.AutoSize = true;
            bottom.ColumnCount = 2;
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            FlowLayoutPanel sp = new FlowLayoutPanel();
            sp.AutoSize = true;
            sp.WrapContents = false;
            sp.Anchor = AnchorStyles.Left;
            _statusPic = new PictureBox();
            _statusPic.SizeMode = PictureBoxSizeMode.AutoSize;
            _statusPic.Margin = new Padding(0, 0, Ui.S(6), 0);
            _status = new Label();
            _status.UseMnemonic = false;
            _status.AutoSize = true;
            _status.Margin = new Padding(0, Ui.S(3), 0, 0);
            sp.Controls.Add(_statusPic);
            sp.Controls.Add(_status);
            bottom.Controls.Add(sp, 0, 0);
            _run = Ui.PrimaryButton("rocket", "バックアップ開始", 280, 60, 14f);
            _run.Click += delegate { RunBackup(); };
            _run.Margin = new Padding(0);
            bottom.Controls.Add(_run, 1, 0);
            root.Controls.Add(bottom, 0, 3);

            AcceptButton = null;
        }

        private static Panel Card()
        {
            Panel p = new Panel();
            p.BackColor = Color.White;
            p.Padding = new Padding(Ui.S(14), Ui.S(12), Ui.S(14), Ui.S(12));
            p.Margin = new Padding(0);
            p.Paint += delegate(object s, PaintEventArgs e)
            {
                Panel c = (Panel)s;
                using (Pen pen = new Pen(Ui.Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, c.Width - 1, c.Height - 1);
            };
            p.Resize += delegate(object s, EventArgs e) { ((Panel)s).Invalidate(); };
            return p;
        }

        private void FitColumns()
        {
            if (_list.Columns.Count < 3) return;
            int w = _list.ClientSize.Width - _list.Columns[1].Width - _list.Columns[2].Width - 2;
            _list.Columns[0].Width = Math.Max(Ui.S(200), w);
        }

        // ======================================================
        // 表示更新
        // ======================================================
        private void RefreshAll()
        {
            _loading = true;
            _list.BeginUpdate();
            string selPath = _list.SelectedItems.Count > 0 ? ((SourceEntry)_list.SelectedItems[0].Tag).Path : null;
            _list.Items.Clear();
            foreach (SourceEntry e in _settings.Sources)
            {
                bool exists = Directory.Exists(e.Path);
                ListViewItem it = new ListViewItem(e.Path);
                it.SubItems.Add(e.DestName);
                it.SubItems.Add(!e.Enabled ? "対象外" : exists ? "OK" : "見つかりません");
                it.ImageKey = !e.Enabled ? "off" : exists ? "ok" : "missing";
                it.Checked = e.Enabled;
                it.Tag = e;
                if (!e.Enabled) it.ForeColor = Color.Gray;
                else if (!exists) it.ForeColor = Color.FromArgb(190, 90, 0);
                if (e.Path == selPath) it.Selected = true;
                _list.Items.Add(it);
            }
            _list.EndUpdate();
            _loading = false;
            FitColumns();

            _dest.Text = string.IsNullOrEmpty(_settings.Destination) ? "(未設定)「変更…」でバックアップ先を選んでください" : _settings.Destination;
            _dest.ForeColor = string.IsNullOrEmpty(_settings.Destination) ? Color.FromArgb(190, 90, 0) : ForeColor;

            UpdateStatus();
            UpdateButtons();
        }

        private void UpdateStatus()
        {
            string icon, text;
            if (_settings.Sources.Count == 0)
            {
                icon = "bulb"; text = "まずは「追加」でバックアップしたいフォルダを登録しましょう。";
            }
            else if (string.IsNullOrEmpty(_settings.Destination))
            {
                icon = "bulb"; text = "次にバックアップ先を選びましょう。";
            }
            else if (_settings.LastBackup == DateTime.MinValue)
            {
                icon = "sparkles"; text = "準備OK!1回目はすべてのファイルをコピーします。";
            }
            else
            {
                string when = _settings.LastBackup.ToLocalTime().ToString("yyyy/MM/dd (ddd) HH:mm");
                switch (_settings.LastResult)
                {
                    case "Success": icon = "check"; text = "前回のバックアップ: " + when + "  完了"; break;
                    case "CompletedWithErrors": icon = "warning"; text = "前回のバックアップ: " + when + "  一部エラーあり"; break;
                    case "Cancelled": icon = "stop"; text = "前回のバックアップ: " + when + "  中止"; break;
                    default: icon = "cross"; text = "前回のバックアップ: " + when + "  失敗"; break;
                }
            }
            SetStatus(icon, text);
        }

        private void SetStatus(string icon, string text)
        {
            _statusPic.Image = Emoji.Get(icon, 22);
            _status.Text = text;
        }

        private void UpdateButtons()
        {
            bool sel = _list.SelectedItems.Count > 0;
            _edit.Enabled = sel && !_busy;
            _del.Enabled = sel && !_busy;
            _add.Enabled = !_busy;
            _destBtn.Enabled = !_busy;
            _import.Enabled = !_busy;
            _export.Enabled = !_busy;
            _run.Enabled = !_busy;
        }

        private SourceEntry Selected
        {
            get { return _list.SelectedItems.Count > 0 ? (SourceEntry)_list.SelectedItems[0].Tag : null; }
        }

        private void SaveSettings()
        {
            try { _settings.Save(); }
            catch (Exception ex)
            {
                EmojiBox.Show(this, "設定を保存できませんでした。\n" + ex.Message, "cross", EmojiBox.Buttons.OK);
            }
        }

        // ======================================================
        // フォルダの登録・編集・削除
        // ======================================================
        private void AddFolder()
        {
            string p = FolderPicker.Pick(this, "バックアップしたいフォルダを選んでください", null);
            if (p != null) AddPaths(new[] { p });
        }

        private void AddPaths(IEnumerable<string> paths)
        {
            List<string> messages = new List<string>();
            int added = 0;
            foreach (string raw in paths)
            {
                string p;
                try { p = Path.GetFullPath(raw); } catch { continue; }
                if (!Directory.Exists(p)) continue;
                if (_settings.Sources.Any(x => PathEquals(x.Path, p)))
                {
                    messages.Add("すでに登録されています: " + p);
                    continue;
                }
                if (!string.IsNullOrEmpty(_settings.Destination) &&
                    (IsInside(_settings.Destination, p) || IsInside(p, _settings.Destination)))
                {
                    messages.Add("バックアップ先と重なるため登録できません: " + p);
                    continue;
                }
                SourceEntry e = new SourceEntry();
                e.Path = p;
                e.DestName = _settings.MakeUniqueDestName(AppSettings.SuggestDestName(p), null);
                e.Enabled = true;
                _settings.Sources.Add(e);
                added++;
            }
            if (added > 0)
            {
                SaveSettings();
                RefreshAll();
                _list.Items[_list.Items.Count - 1].Selected = true;
                _list.EnsureVisible(_list.Items.Count - 1);
            }
            if (messages.Count > 0)
                EmojiBox.Show(this, string.Join("\n", messages.ToArray()), "info", EmojiBox.Buttons.OK);
        }

        private void EditSelected()
        {
            SourceEntry e = Selected;
            if (e == null || _busy) return;
            using (EditForm f = new EditForm(_settings, e))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                SourceEntry r = f.Result;

                if (!string.IsNullOrEmpty(_settings.Destination) &&
                    (IsInside(_settings.Destination, r.Path) || IsInside(r.Path, _settings.Destination)))
                {
                    EmojiBox.Show(this, "バックアップ先と重なるフォルダは登録できません。", "warning", EmojiBox.Buttons.OK);
                    return;
                }

                // 保存フォルダ名が変わったら、既存のバックアップフォルダの名前も変えるか確認
                if (!string.Equals(r.DestName, e.DestName, StringComparison.Ordinal) &&
                    !string.IsNullOrEmpty(_settings.Destination))
                {
                    string oldDir = Path.Combine(_settings.Destination, e.DestName);
                    string newDir = Path.Combine(_settings.Destination, r.DestName);
                    if (Directory.Exists(oldDir) && !Directory.Exists(newDir))
                    {
                        if (EmojiBox.Show(this, "バックアップ先にある既存のフォルダの名前も変更しますか?\n\n" +
                                oldDir + "\n  ↓\n" + newDir + "\n\n(「いいえ」の場合、次回は新しいフォルダに全部コピーします)",
                                "question", EmojiBox.Buttons.YesNo) == DialogResult.Yes)
                        {
                            try
                            {
                                if (string.Equals(r.DestName, e.DestName, StringComparison.OrdinalIgnoreCase))
                                {
                                    // 大文字小文字だけの変更は一時名を経由
                                    string tmp = oldDir + ".micopy_rename";
                                    Directory.Move(oldDir, tmp);
                                    Directory.Move(tmp, newDir);
                                }
                                else Directory.Move(oldDir, newDir);
                            }
                            catch (Exception ex)
                            {
                                EmojiBox.Show(this, "フォルダ名を変更できませんでした。\n" + ex.Message, "cross", EmojiBox.Buttons.OK);
                                return;
                            }
                        }
                    }
                }
                e.Path = r.Path;
                e.DestName = r.DestName;
                e.Enabled = r.Enabled;
                SaveSettings();
                RefreshAll();
            }
        }

        private void DeleteSelected()
        {
            SourceEntry e = Selected;
            if (e == null || _busy) return;
            if (EmojiBox.Show(this, "このフォルダの登録を削除しますか?\n\n" + e.Path +
                    "\n\n※ バックアップ元のフォルダ自体は削除されません。",
                    "trash", EmojiBox.Buttons.YesNo, new[] { "削除する", "やめる" }) != DialogResult.Yes)
                return;

            string backupDir = string.IsNullOrEmpty(_settings.Destination) ? null : Path.Combine(_settings.Destination, e.DestName);
            bool deleteBackup = false;
            if (backupDir != null && Directory.Exists(backupDir))
            {
                deleteBackup = EmojiBox.Show(this, "バックアップ先にあるこのフォルダのバックアップも削除しますか?\n\n" + backupDir,
                    "broom", EmojiBox.Buttons.YesNo, new[] { "バックアップも削除", "残しておく" }) == DialogResult.Yes;
            }

            _settings.Sources.Remove(e);
            SaveSettings();
            RefreshAll();

            if (deleteBackup)
            {
                _busy = true;
                UpdateButtons();
                SetStatus("broom", "バックアップを削除しています…");
                Cursor = Cursors.WaitCursor;
                Thread th = new Thread(delegate()
                {
                    string err = null;
                    try { DeleteTree(new DirectoryInfo(backupDir)); }
                    catch (Exception ex) { err = ex.Message; }
                    try
                    {
                        BeginInvoke(new Action(delegate
                    {
                        _busy = false;
                        Cursor = Cursors.Default;
                        UpdateButtons();
                        UpdateStatus();
                        if (err != null)
                            EmojiBox.Show(this, "バックアップの一部を削除できませんでした。\n" + err, "warning", EmojiBox.Buttons.OK);
                    }));
                    }
                    catch { }
                });
                th.IsBackground = true;
                th.Start();
            }
        }

        private static void DeleteTree(DirectoryInfo d)
        {
            if ((d.Attributes & FileAttributes.ReparsePoint) == 0)
            {
                foreach (FileInfo f in d.EnumerateFiles())
                {
                    if ((f.Attributes & FileAttributes.ReadOnly) != 0) f.Attributes = FileAttributes.Normal;
                    f.Delete();
                }
                foreach (DirectoryInfo c in d.EnumerateDirectories()) DeleteTree(c);
            }
            if ((d.Attributes & FileAttributes.ReadOnly) != 0) d.Attributes = FileAttributes.Directory;
            d.Delete(false);
        }

        private void OnItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (_loading) return;
            SourceEntry s = e.Item.Tag as SourceEntry;
            if (s == null || s.Enabled == e.Item.Checked) return;
            s.Enabled = e.Item.Checked;
            SaveSettings();
            BeginInvoke(new Action(RefreshAll));
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.None;
            if (_busy || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            string[] items = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (items != null && items.Any(Directory.Exists)) e.Effect = DragDropEffects.Link;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            string[] items = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (items == null) return;
            AddPaths(items.Where(Directory.Exists).ToArray());
        }

        // ======================================================
        // バックアップ先
        // ======================================================
        private void ChangeDestination()
        {
            string p = FolderPicker.Pick(this, "バックアップ先のフォルダを選んでください(外付けドライブなど)", _settings.Destination);
            if (p == null) return;
            foreach (SourceEntry e in _settings.Sources)
            {
                if (IsInside(p, e.Path) || IsInside(e.Path, p))
                {
                    EmojiBox.Show(this, "バックアップするフォルダと重なる場所は、バックアップ先にできません。\n\n" + e.Path,
                        "warning", EmojiBox.Buttons.OK);
                    return;
                }
            }
            if (!string.IsNullOrEmpty(_settings.Destination) && !PathEquals(_settings.Destination, p))
            {
                if (EmojiBox.Show(this, "バックアップ先を変更しますか?\n\n" + _settings.Destination + "\n  ↓\n" + p +
                        "\n\n新しいバックアップ先には、次回すべてのファイルをコピーします。\n(今までのバックアップはそのまま残ります)",
                        "question", EmojiBox.Buttons.YesNo) != DialogResult.Yes)
                    return;
                _settings.LastBackup = DateTime.MinValue;
                _settings.LastResult = "";
            }
            _settings.Destination = p;
            SaveSettings();
            RefreshAll();
        }

        // ======================================================
        // バックアップ実行
        // ======================================================
        private void RunBackup()
        {
            if (_busy) return;
            if (_settings.Sources.Count == 0)
            {
                EmojiBox.Show(this, "バックアップするフォルダが登録されていません。\n「追加」から登録してください。", "bulb", EmojiBox.Buttons.OK);
                return;
            }
            if (!_settings.Sources.Any(x => x.Enabled))
            {
                EmojiBox.Show(this, "チェックが入っているフォルダがありません。", "bulb", EmojiBox.Buttons.OK);
                return;
            }
            if (string.IsNullOrEmpty(_settings.Destination))
            {
                EmojiBox.Show(this, "バックアップ先が設定されていません。\n「変更…」から選んでください。", "bulb", EmojiBox.Buttons.OK);
                return;
            }
            string root = null;
            try { root = Path.GetPathRoot(Path.GetFullPath(_settings.Destination)); } catch { }
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                EmojiBox.Show(this, "バックアップ先のドライブが見つかりません。\n外付けドライブなどが接続されているか確認してください。\n\n" +
                    _settings.Destination, "warning", EmojiBox.Buttons.OK);
                return;
            }

            BackupEngine engine = new BackupEngine(_settings.Sources, _settings.Destination);
            BackupResult r;
            using (ProgressForm f = new ProgressForm(engine))
            {
                f.ShowDialog(this);
                r = f.Result;
            }
            if (r != null)
            {
                _settings.LastBackup = DateTime.UtcNow;
                _settings.LastResult = r.Status.ToString();
                SaveSettings();
            }
            RefreshAll();
        }

        // ======================================================
        // 設定のインポート・エクスポート
        // ======================================================
        private void ExportSettings()
        {
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Title = "設定のエクスポート";
                d.Filter = "micopy 設定ファイル (*.xml)|*.xml|すべてのファイル (*.*)|*.*";
                d.FileName = "micopy_設定_" + DateTime.Now.ToString("yyyyMMdd") + ".xml";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    _settings.Export(d.FileName);
                    EmojiBox.Show(this, "設定をエクスポートしました。\n\n" + d.FileName, "check", EmojiBox.Buttons.OK);
                }
                catch (Exception ex)
                {
                    EmojiBox.Show(this, "エクスポートできませんでした。\n" + ex.Message, "cross", EmojiBox.Buttons.OK);
                }
            }
        }

        private void ImportSettings()
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Title = "設定のインポート";
                d.Filter = "micopy 設定ファイル (*.xml)|*.xml|すべてのファイル (*.*)|*.*";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                AppSettings imp;
                try { imp = AppSettings.Import(d.FileName); }
                catch (Exception ex)
                {
                    EmojiBox.Show(this, "設定ファイルを読み込めませんでした。\n" + ex.Message, "cross", EmojiBox.Buttons.OK);
                    return;
                }
                string msg = "現在の設定を、次の内容で置き換えますか?\n\n" +
                             "・登録フォルダ: " + imp.Sources.Count + " 件\n" +
                             "・バックアップ先: " + (string.IsNullOrEmpty(imp.Destination) ? "(なし)" : imp.Destination);
                if (EmojiBox.Show(this, msg, "import", EmojiBox.Buttons.YesNo, new[] { "置き換える", "やめる" }) != DialogResult.Yes)
                    return;
                bool destChanged = !PathEquals(imp.Destination ?? "", _settings.Destination ?? "");
                _settings.Sources = imp.Sources;
                _settings.Destination = imp.Destination;
                if (destChanged) { _settings.LastBackup = DateTime.MinValue; _settings.LastResult = ""; }
                SaveSettings();
                RefreshAll();
                EmojiBox.Show(this, "設定をインポートしました。", "check", EmojiBox.Buttons.OK);
            }
        }

        // ======================================================
        // ウィンドウ位置(マルチディスプレイ対応)
        // ======================================================
        private void RestoreWindow()
        {
            StartPosition = FormStartPosition.CenterScreen;
            Rectangle b = _settings.WindowBounds;
            if (b.Width <= 0 || b.Height <= 0) return;

            Screen screen = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == _settings.ScreenDevice);
            if (screen == null)
                screen = Screen.AllScreens.FirstOrDefault(s => s.WorkingArea.IntersectsWith(b));
            if (screen == null) return; // 前回のディスプレイがない → プライマリの中央

            Rectangle wa = screen.WorkingArea;
            int w = Math.Min(Math.Max(b.Width, MinimumSize.Width), wa.Width);
            int h = Math.Min(Math.Max(b.Height, MinimumSize.Height), wa.Height);
            int x = b.X, y = b.Y;
            Rectangle r = new Rectangle(x, y, w, h);
            Rectangle inter = Rectangle.Intersect(r, wa);
            // ほとんど画面外ならそのディスプレイの中央へ
            if (inter.Width < Math.Min(w, Ui.S(200)) || inter.Height < Math.Min(h, Ui.S(100)) || r.Y < wa.Top)
            {
                x = wa.Left + (wa.Width - w) / 2;
                y = wa.Top + (wa.Height - h) / 2;
            }
            StartPosition = FormStartPosition.Manual;
            Bounds = new Rectangle(x, y, w, h);
            if (_settings.Maximized) WindowState = FormWindowState.Maximized;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_busy)
            {
                EmojiBox.Show(this, "処理中です。終わるまでお待ちください。", "hourglass", EmojiBox.Buttons.OK);
                e.Cancel = true;
                return;
            }
            Rectangle nb = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            _settings.WindowBounds = nb;
            _settings.Maximized = WindowState == FormWindowState.Maximized;
            Screen sc = WindowState == FormWindowState.Normal ? Screen.FromControl(this) : Screen.FromRectangle(nb);
            _settings.ScreenDevice = sc.DeviceName;
            SaveSettings();
            base.OnFormClosing(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F5) { RunBackup(); return true; }
            if (keyData == Keys.Insert) { AddFolder(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ======================================================
        private static string Norm(string p)
        {
            try { return Path.GetFullPath(p).TrimEnd('\\') + "\\"; }
            catch { return (p ?? "").TrimEnd('\\') + "\\"; }
        }

        private static bool PathEquals(string a, string b)
        {
            return string.Equals(Norm(a), Norm(b), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>path が folder と同じか、その中にあるか</summary>
        private static bool IsInside(string path, string folder)
        {
            return Norm(path).StartsWith(Norm(folder), StringComparison.OrdinalIgnoreCase);
        }
    }
}
