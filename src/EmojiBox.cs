using System;
using System.Drawing;
using System.Windows.Forms;

namespace Micopy
{
    /// <summary>カラー絵文字付きのメッセージボックス</summary>
    internal class EmojiBox : Form
    {
        public enum Buttons { OK, OKCancel, YesNo, YesNoCancel }

        public static DialogResult Show(IWin32Window owner, string message, string emoji, Buttons buttons)
        {
            return Show(owner, message, emoji, buttons, null);
        }

        public static DialogResult Show(IWin32Window owner, string message, string emoji, Buttons buttons, string[] labels)
        {
            using (EmojiBox b = new EmojiBox(message, emoji, buttons, labels))
            {
                if (owner == null) b.StartPosition = FormStartPosition.CenterScreen;
                return b.ShowDialog(owner);
            }
        }

        private EmojiBox(string message, string emoji, Buttons buttons, string[] labels)
        {
            Text = "micopy";
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
            Padding = new Padding(Ui.S(20), Ui.S(18), Ui.S(20), Ui.S(14));

            TableLayoutPanel t = new TableLayoutPanel();
            t.AutoSize = true;
            t.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            t.ColumnCount = 2;
            t.RowCount = 2;
            t.Dock = DockStyle.Fill;

            PictureBox pic = new PictureBox();
            pic.Image = Emoji.Get(emoji, 44);
            pic.SizeMode = PictureBoxSizeMode.AutoSize;
            pic.Margin = new Padding(0, 0, Ui.S(16), Ui.S(10));
            t.Controls.Add(pic, 0, 0);

            Label l = new Label();

            l.UseMnemonic = false;
            l.Text = message;
            l.AutoSize = true;
            l.MaximumSize = new Size(Ui.S(460), 0);
            l.MinimumSize = new Size(Ui.S(340), Ui.S(44));
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.Margin = new Padding(0, 0, 0, Ui.S(16));
            t.Controls.Add(l, 1, 0);

            FlowLayoutPanel bp = new FlowLayoutPanel();
            bp.FlowDirection = FlowDirection.RightToLeft;
            bp.AutoSize = true;
            bp.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            bp.Dock = DockStyle.Fill;
            bp.Margin = new Padding(0);
            t.Controls.Add(bp, 0, 1);
            t.SetColumnSpan(bp, 2);

            // 右から順に追加
            switch (buttons)
            {
                case Buttons.OK:
                    AddBtn(bp, Lbl(labels, 0, "OK"), "check", DialogResult.OK, true, true);
                    break;
                case Buttons.OKCancel:
                    AddBtn(bp, Lbl(labels, 1, "キャンセル"), "cross", DialogResult.Cancel, false, true);
                    AddBtn(bp, Lbl(labels, 0, "OK"), "check", DialogResult.OK, true, false);
                    break;
                case Buttons.YesNo:
                    AddBtn(bp, Lbl(labels, 1, "いいえ"), "cross", DialogResult.No, false, true);
                    AddBtn(bp, Lbl(labels, 0, "はい"), "check", DialogResult.Yes, true, false);
                    break;
                case Buttons.YesNoCancel:
                    AddBtn(bp, Lbl(labels, 2, "キャンセル"), "door", DialogResult.Cancel, false, true);
                    AddBtn(bp, Lbl(labels, 1, "いいえ"), "cross", DialogResult.No, false, false);
                    AddBtn(bp, Lbl(labels, 0, "はい"), "check", DialogResult.Yes, true, false);
                    break;
            }
            Controls.Add(t);
        }

        private static string Lbl(string[] labels, int i, string def)
        {
            return labels != null && labels.Length > i && labels[i] != null ? labels[i] : def;
        }

        private Button _focusButton;

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // ActiveControl はフォームに配置された後でないと設定できないため、表示後にフォーカスする
            if (_focusButton != null) _focusButton.Focus();
        }

        private void AddBtn(FlowLayoutPanel p, string text, string emoji, DialogResult r, bool accept, bool cancel)
        {
            Button b = Ui.EmojiButton(emoji, text, 0, 36);
            b.AutoSize = true;
            b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            b.MinimumSize = new Size(Ui.S(100), Ui.S(36));
            b.TextAlign = ContentAlignment.MiddleCenter;
            b.DialogResult = r;
            b.Margin = new Padding(Ui.S(8), 0, 0, 0);
            p.Controls.Add(b);
            if (accept) { AcceptButton = b; _focusButton = b; }
            if (cancel) CancelButton = b;
        }
    }
}
