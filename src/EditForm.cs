using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Micopy
{
    /// <summary>登録フォルダの編集画面</summary>
    internal class EditForm : Form
    {
        private readonly AppSettings _settings;
        private readonly SourceEntry _original;
        private readonly TextBox _path;
        private readonly TextBox _name;
        private readonly CheckBox _enabled;
        private readonly Label _preview;

        public SourceEntry Result { get; private set; }

        public EditForm(AppSettings settings, SourceEntry entry)
        {
            _settings = settings;
            _original = entry;

            Text = "フォルダの編集 - micopy";
            Icon = Emoji.AppIcon;
            Font = Ui.MakeFont(10f, FontStyle.Regular);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            AutoScaleMode = AutoScaleMode.None;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(Ui.S(18));

            TableLayoutPanel t = new TableLayoutPanel();
            t.AutoSize = true;
            t.ColumnCount = 2;
            t.Dock = DockStyle.Fill;

            t.Controls.Add(Ui.Heading("folder", "バックアップするフォルダ", 10.5f), 0, 0);
            t.SetColumnSpan(t.GetControlFromPosition(0, 0), 2);

            _path = new TextBox();
            _path.Width = Ui.S(420);
            _path.Text = entry.Path;
            _path.Margin = new Padding(0, Ui.S(4), Ui.S(8), Ui.S(14));
            t.Controls.Add(_path, 0, 1);
            Button browse = Ui.EmojiButton("open_folder", "参照…", 100, 32);
            browse.Margin = new Padding(0, 0, 0, Ui.S(14));
            browse.Click += delegate
            {
                string p = FolderPicker.Pick(this, "バックアップするフォルダを選んでください", _path.Text);
                if (p != null)
                {
                    bool autoName = _name.Text == AppSettings.SuggestDestName(_path.Text) || _name.Text.Length == 0;
                    _path.Text = p;
                    if (autoName) _name.Text = _settings.MakeUniqueDestName(AppSettings.SuggestDestName(p), _original);
                }
            };
            t.Controls.Add(browse, 1, 1);

            Control h2 = Ui.Heading("floppy", "バックアップ先での保存フォルダ名", 10.5f);
            t.Controls.Add(h2, 0, 2);
            t.SetColumnSpan(h2, 2);

            _name = new TextBox();
            _name.Width = Ui.S(420);
            _name.Text = entry.DestName;
            _name.Margin = new Padding(0, Ui.S(4), Ui.S(8), Ui.S(2));
            t.Controls.Add(_name, 0, 3);

            _preview = new Label();

            _preview.UseMnemonic = false;
            _preview.AutoSize = true;
            _preview.ForeColor = Ui.SubText;
            _preview.MaximumSize = new Size(Ui.S(540), 0);
            _preview.Margin = new Padding(0, Ui.S(2), 0, Ui.S(14));
            t.Controls.Add(_preview, 0, 4);
            t.SetColumnSpan(_preview, 2);
            _name.TextChanged += delegate { UpdatePreview(); };
            UpdatePreview();

            _enabled = new CheckBox();
            _enabled.Text = "このフォルダをバックアップする(チェックを外すと一時的に対象外)";
            _enabled.AutoSize = true;
            _enabled.Checked = entry.Enabled;
            _enabled.Margin = new Padding(0, 0, 0, Ui.S(16));
            t.Controls.Add(_enabled, 0, 5);
            t.SetColumnSpan(_enabled, 2);

            FlowLayoutPanel bp = new FlowLayoutPanel();
            bp.FlowDirection = FlowDirection.RightToLeft;
            bp.AutoSize = true;
            bp.Dock = DockStyle.Fill;
            Button cancel = Ui.EmojiButton("cross", "キャンセル", 130, 36);
            cancel.DialogResult = DialogResult.Cancel;
            Button ok = Ui.EmojiButton("check", "保存", 110, 36);
            ok.Margin = new Padding(0, 0, Ui.S(8), 0);
            ok.Click += OnOk;
            bp.Controls.Add(cancel);
            bp.Controls.Add(ok);
            t.Controls.Add(bp, 0, 6);
            t.SetColumnSpan(bp, 2);

            AcceptButton = ok;
            CancelButton = cancel;
            Controls.Add(t);
        }

        private void UpdatePreview()
        {
            string dest = string.IsNullOrEmpty(_settings.Destination) ? "(バックアップ先)" : _settings.Destination;
            _preview.Text = "保存場所: " + Path.Combine(dest, _name.Text.Trim());
        }

        private void OnOk(object sender, EventArgs e)
        {
            string path = _path.Text.Trim();
            string name = _name.Text.Trim();
            if (path.Length == 0)
            {
                EmojiBox.Show(this, "フォルダを指定してください。", "warning", EmojiBox.Buttons.OK);
                return;
            }
            if (!Directory.Exists(path))
            {
                if (EmojiBox.Show(this, "このフォルダは今は見つかりません。\n" + path +
                        "\n\nこのまま登録しますか?\n(見つからない間はバックアップ時にスキップされます)",
                        "question", EmojiBox.Buttons.YesNo) != DialogResult.Yes)
                    return;
            }
            if (!AppSettings.IsValidDestName(name))
            {
                EmojiBox.Show(this, "保存フォルダ名に使えない文字が含まれています。\n( \\ / : * ? \" < > | など)",
                    "warning", EmojiBox.Buttons.OK);
                return;
            }
            if (_settings.Sources.Any(x => x != _original &&
                    string.Equals(x.DestName, name, StringComparison.OrdinalIgnoreCase)))
            {
                EmojiBox.Show(this, "保存フォルダ名「" + name + "」は他のフォルダで使われています。\n別の名前にしてください。",
                    "warning", EmojiBox.Buttons.OK);
                return;
            }
            SourceEntry r = new SourceEntry();
            r.Path = path;
            r.DestName = name;
            r.Enabled = _enabled.Checked;
            Result = r;
            DialogResult = DialogResult.OK;
        }
    }
}
