using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Micopy
{
    /// <summary>バックアップ実行中の進行状況画面</summary>
    internal class ProgressForm : Form
    {
        private readonly BackupEngine _engine;
        private Thread _thread;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly Stopwatch _watch = new Stopwatch();
        private Stopwatch _copyWatch;
        private bool _closeRequested;
        private string _lastIcon = "";

        private readonly PictureBox _phasePic;
        private readonly Label _phase;
        private readonly ProgressBar _bar;
        private readonly Label _percent;
        private readonly Label _detail;
        private readonly Label _current;
        private readonly Label _sNew, _sUpd, _sDel, _sSame, _sErr;
        private readonly Button _stop, _log, _close;
        private readonly TextBox _errors;

        public BackupResult Result { get; private set; }

        public ProgressForm(BackupEngine engine)
        {
            _engine = engine;

            Text = "バックアップ中 - micopy";
            Icon = Emoji.AppIcon;
            Font = Ui.MakeFont(10f, FontStyle.Regular);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(Ui.S(600), Ui.S(330));
            Padding = new Padding(Ui.S(20), Ui.S(16), Ui.S(20), Ui.S(14));

            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.ColumnCount = 1;
            t.RowCount = 7;
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 7; i++) t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles[5] = new RowStyle(SizeType.Percent, 100);

            // フェーズ表示
            FlowLayoutPanel ph = new FlowLayoutPanel();
            ph.AutoSize = true;
            ph.WrapContents = false;
            ph.Margin = new Padding(0, 0, 0, Ui.S(10));
            _phasePic = new PictureBox();
            _phasePic.SizeMode = PictureBoxSizeMode.AutoSize;
            _phasePic.Image = Emoji.Get("hourglass", 36);
            _phasePic.Margin = new Padding(0, 0, Ui.S(10), 0);
            _phase = new Label();
            _phase.UseMnemonic = false;
            _phase.AutoSize = true;
            _phase.Font = Ui.MakeFont(14f, FontStyle.Bold);
            _phase.Text = "準備中…";
            _phase.MaximumSize = new Size(Ui.S(530), 0);
            _phase.Margin = new Padding(0, Ui.S(5), 0, 0);
            ph.Controls.Add(_phasePic);
            ph.Controls.Add(_phase);
            t.Controls.Add(ph, 0, 0);

            // プログレスバー
            _bar = new ProgressBar();
            _bar.Dock = DockStyle.Top;
            _bar.Height = Ui.S(26);
            _bar.Maximum = 1000;
            _bar.Style = ProgressBarStyle.Marquee;
            _bar.MarqueeAnimationSpeed = 30;
            _bar.Margin = new Padding(0, 0, 0, Ui.S(4));
            t.Controls.Add(_bar, 0, 1);

            TableLayoutPanel pr = new TableLayoutPanel();
            pr.ColumnCount = 2;
            pr.Dock = DockStyle.Top;
            pr.AutoSize = true;
            pr.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pr.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _percent = new Label();
            _percent.UseMnemonic = false;
            _percent.AutoSize = true;
            _percent.Font = Ui.MakeFont(11f, FontStyle.Bold);
            _percent.ForeColor = Ui.Accent;
            _detail = new Label();
            _detail.UseMnemonic = false;
            _detail.AutoSize = true;
            _detail.Anchor = AnchorStyles.Right;
            _detail.ForeColor = Ui.SubText;
            pr.Controls.Add(_percent, 0, 0);
            pr.Controls.Add(_detail, 1, 0);
            pr.Margin = new Padding(0, 0, 0, Ui.S(4));
            t.Controls.Add(pr, 0, 2);

            // 処理中のファイル
            _current = new Label();
            _current.UseMnemonic = false;
            _current.AutoSize = false;
            _current.AutoEllipsis = true;
            _current.Dock = DockStyle.Top;
            _current.Height = Ui.S(22);
            _current.ForeColor = Ui.SubText;
            _current.Margin = new Padding(0, 0, 0, Ui.S(10));
            t.Controls.Add(_current, 0, 3);

            // 件数
            FlowLayoutPanel st = new FlowLayoutPanel();
            st.AutoSize = true;
            st.WrapContents = true;
            st.Dock = DockStyle.Top;
            st.Margin = new Padding(0, 0, 0, Ui.S(8));
            _sNew = Stat(st, "sparkles", "新規");
            _sUpd = Stat(st, "edit", "更新");
            _sDel = Stat(st, "broom", "削除");
            _sSame = Stat(st, "skip", "変更なし");
            _sErr = Stat(st, "warning", "エラー");
            t.Controls.Add(st, 0, 4);

            // エラー一覧(完了後に表示)
            _errors = new TextBox();
            _errors.Multiline = true;
            _errors.ReadOnly = true;
            _errors.ScrollBars = ScrollBars.Both;
            _errors.WordWrap = false;
            _errors.Dock = DockStyle.Fill;
            _errors.Visible = false;
            _errors.BackColor = Color.FromArgb(255, 248, 240);
            _errors.Font = Ui.MakeFont(9f, FontStyle.Regular);
            t.Controls.Add(_errors, 0, 5);

            // ボタン
            FlowLayoutPanel bp = new FlowLayoutPanel();
            bp.FlowDirection = FlowDirection.RightToLeft;
            bp.AutoSize = true;
            bp.Dock = DockStyle.Fill;
            _stop = Ui.EmojiButton("stop", "中止", 110, 38);
            _stop.Click += delegate { AskCancel(); };
            _close = Ui.EmojiButton("door", "閉じる", 120, 38);
            _close.Visible = false;
            _close.Click += delegate { Close(); };
            _log = Ui.EmojiButton("memo", "ログを見る", 140, 38);
            _log.Visible = false;
            _log.Margin = new Padding(0, 0, Ui.S(8), 0);
            _log.Click += delegate { OpenLog(); };
            bp.Controls.Add(_stop);
            bp.Controls.Add(_close);
            bp.Controls.Add(_log);
            t.Controls.Add(bp, 0, 6);

            Controls.Add(t);

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 120;
            _timer.Tick += delegate { RefreshView(); };
        }

        private Label Stat(FlowLayoutPanel parent, string emoji, string caption)
        {
            PictureBox p = new PictureBox();
            p.Image = Emoji.Get(emoji, 18);
            p.SizeMode = PictureBoxSizeMode.AutoSize;
            p.Margin = new Padding(0, Ui.S(3), Ui.S(3), 0);
            Label l = new Label();
            l.UseMnemonic = false;
            l.AutoSize = true;
            l.Text = caption + " 0";
            l.Margin = new Padding(0, Ui.S(2), Ui.S(16), 0);
            l.Tag = caption;
            parent.Controls.Add(p);
            parent.Controls.Add(l);
            return l;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _watch.Start();
            _timer.Start();
            _thread = new Thread(delegate()
            {
                BackupResult r = _engine.Run();
                try { BeginInvoke(new Action(delegate { OnDone(r); })); } catch { }
            });
            _thread.IsBackground = true;
            _thread.Start();
        }

        private void SetPhaseIcon(string icon)
        {
            if (icon == _lastIcon) return;
            _lastIcon = icon;
            _phasePic.Image = Emoji.Get(icon, 36);
        }

        private void RefreshView()
        {
            BackupProgress p = _engine.Progress.Snapshot();
            if (_engine.IsCancelRequested)
            {
                _phase.Text = "中止しています…";
                SetPhaseIcon("stop");
            }
            else
            {
                _phase.Text = p.Phase;
                SetPhaseIcon(p.PhaseIcon);
            }

            if (p.Scanning)
            {
                if (_bar.Style != ProgressBarStyle.Marquee) _bar.Style = ProgressBarStyle.Marquee;
                _percent.Text = "確認済み " + p.ScannedFiles.ToString("#,0") + " ファイル";
                _detail.Text = "経過 " + BackupEngine.FormatTime(_watch.Elapsed);
            }
            else
            {
                if (_copyWatch == null) _copyWatch = Stopwatch.StartNew();
                if (_bar.Style != ProgressBarStyle.Continuous) _bar.Style = ProgressBarStyle.Continuous;
                double ratio = p.TotalUnits > 0 ? (double)p.DoneUnits / p.TotalUnits : 1.0;
                ratio = Math.Max(0, Math.Min(1, ratio));
                _bar.Value = (int)(ratio * 1000);
                _percent.Text = (ratio * 100).ToString("0") + " %";
                string eta = "";
                double sec = _copyWatch.Elapsed.TotalSeconds;
                if (ratio > 0.02 && sec > 3)
                {
                    TimeSpan left = TimeSpan.FromSeconds(sec / ratio - sec);
                    eta = "  ・ 残り 約" + BackupEngine.FormatTime(left);
                }
                int done = p.CopiedNew + p.CopiedUpdated;
                _detail.Text = "コピー " + done.ToString("#,0") + " / " + p.PlannedCopies.ToString("#,0") +
                               " 件 (" + BackupEngine.FormatBytes(p.BytesCopied) + ")" + eta;
            }
            _current.Text = p.CurrentItem;
            SetStat(_sNew, p.CopiedNew);
            SetStat(_sUpd, p.CopiedUpdated);
            SetStat(_sDel, p.DeletedFiles + p.DeletedDirs);
            SetStat(_sSame, p.Unchanged);
            SetStat(_sErr, p.Errors);
            _sErr.ForeColor = p.Errors > 0 ? Color.FromArgb(200, 60, 30) : ForeColor;
        }

        private static void SetStat(Label l, int v)
        {
            l.Text = (string)l.Tag + " " + v.ToString("#,0");
        }

        private void OnDone(BackupResult r)
        {
            Result = r;
            RefreshView();
            _timer.Stop();
            _bar.Style = ProgressBarStyle.Continuous;

            switch (r.Status)
            {
                case BackupStatus.Success:
                    SetPhaseIcon("party");
                    _bar.Value = 1000;
                    _percent.Text = "100 %";
                    break;
                case BackupStatus.CompletedWithErrors: SetPhaseIcon("warning"); _bar.Value = 1000; break;
                case BackupStatus.Cancelled: SetPhaseIcon("stop"); break;
                default: SetPhaseIcon("cross"); break;
            }
            _phase.Text = r.Status == BackupStatus.Failed ? "バックアップできませんでした" : r.Message;
            _detail.Text = "所要時間 " + BackupEngine.FormatTime(r.Elapsed) + "  ・ コピー " +
                           BackupEngine.FormatBytes(r.Final.BytesCopied);
            _current.Text = "";
            Text = "完了 - micopy";

            if (r.ErrorLines.Count > 0)
            {
                _errors.Text = string.Join("\r\n", r.ErrorLines.ToArray());
                _errors.Visible = true;
                ClientSize = new Size(ClientSize.Width, Ui.S(470));
            }
            _stop.Visible = false;
            _close.Visible = true;
            _log.Visible = !string.IsNullOrEmpty(r.LogPath);
            AcceptButton = _close;
            CancelButton = _close;
            _close.Focus();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            if (_closeRequested) Close();
        }

        private void AskCancel()
        {
            if (Result != null) return;
            if (EmojiBox.Show(this, "バックアップを中止しますか?\n(コピー済みのファイルはそのまま残ります)",
                    "question", EmojiBox.Buttons.YesNo, new[] { "中止する", "続ける" }) == DialogResult.Yes)
            {
                _engine.Cancel();
                _stop.Enabled = false;
            }
        }

        private void OpenLog()
        {
            if (Result == null || string.IsNullOrEmpty(Result.LogPath) || !File.Exists(Result.LogPath)) return;
            try { Process.Start("notepad.exe", "\"" + Result.LogPath + "\""); } catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (Result == null)
            {
                // 実行中は中止してから閉じる
                e.Cancel = true;
                if (!_engine.IsCancelRequested) AskCancel();
                if (_engine.IsCancelRequested) _closeRequested = true;
                return;
            }
            _timer.Stop();
            base.OnFormClosing(e);
        }
    }
}
