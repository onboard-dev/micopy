using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Micopy
{
    /// <summary>EXEに埋め込んだカラー絵文字PNGを取り出す</summary>
    internal static class Emoji
    {
        private static readonly Dictionary<string, Bitmap> Cache = new Dictionary<string, Bitmap>();
        private static readonly Dictionary<string, Bitmap> Raw = new Dictionary<string, Bitmap>();

        public static Bitmap Get(string name, int logicalSize)
        {
            int size = Ui.S(logicalSize);
            string key = name + "|" + size;
            Bitmap bmp;
            if (Cache.TryGetValue(key, out bmp)) return bmp;

            Bitmap src = LoadRaw("micopy.emoji." + name + ".png");
            bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                if (src != null) g.DrawImage(src, new Rectangle(0, 0, size, size));
            }
            Cache[key] = bmp;
            return bmp;
        }

        public static Bitmap Logo(int logicalSize)
        {
            int size = Ui.S(logicalSize);
            Bitmap src = LoadRaw("micopy.logo.png");
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                if (src != null) g.DrawImage(src, new Rectangle(0, 0, size, size));
            }
            return bmp;
        }

        private static Icon _icon;
        public static Icon AppIcon
        {
            get
            {
                if (_icon == null)
                {
                    try
                    {
                        using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("micopy.ico"))
                            if (s != null) _icon = new Icon(s);
                    }
                    catch { }
                }
                return _icon;
            }
        }

        private static Bitmap LoadRaw(string res)
        {
            Bitmap b;
            if (Raw.TryGetValue(res, out b)) return b;
            try
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(res))
                    b = s != null ? new Bitmap(s) : null;
            }
            catch { b = null; }
            Raw[res] = b;
            return b;
        }
    }

    /// <summary>DPI・フォント・ボタン生成などの共通処理</summary>
    internal static class Ui
    {
        private static float _scale = 0;
        public static float Scale
        {
            get
            {
                if (_scale == 0)
                {
                    try { using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) _scale = g.DpiX / 96f; }
                    catch { _scale = 1f; }
                }
                return _scale;
            }
        }
        public static int S(int v) { return (int)Math.Round(v * Scale); }

        public static readonly Color Accent = Color.FromArgb(37, 99, 235);
        public static readonly Color AccentDark = Color.FromArgb(29, 78, 216);
        public static readonly Color Green = Color.FromArgb(16, 150, 100);
        public static readonly Color Back = Color.FromArgb(245, 247, 251);
        public static readonly Color CardBack = Color.White;
        public static readonly Color Border = Color.FromArgb(222, 226, 235);
        public static readonly Color SubText = Color.FromArgb(100, 110, 125);

        public static Font MakeFont(float pt, FontStyle style)
        {
            try
            {
                Font f = new Font("Yu Gothic UI", pt, style);
                if (f.Name == "Yu Gothic UI") return f;
            }
            catch { }
            return new Font("Meiryo UI", pt, style);
        }

        /// <summary>絵文字アイコン付きのボタン</summary>
        public static Button EmojiButton(string emoji, string text, int width, int height)
        {
            Button b = new Button();
            b.Text = " " + text;
            b.Image = Emoji.Get(emoji, 20);
            b.ImageAlign = ContentAlignment.MiddleLeft;
            b.TextImageRelation = TextImageRelation.ImageBeforeText;
            b.TextAlign = ContentAlignment.MiddleLeft;
            b.Size = new Size(S(width), S(height));
            b.Padding = new Padding(S(6), 0, S(4), 0);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Border;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(235, 241, 255);
            b.FlatAppearance.MouseDownBackColor = Color.FromArgb(220, 230, 255);
            b.BackColor = Color.White;
            b.Cursor = Cursors.Hand;
            b.UseVisualStyleBackColor = false;
            return b;
        }

        /// <summary>目立つ色付きボタン</summary>
        public static Button PrimaryButton(string emoji, string text, int width, int height, float fontPt)
        {
            Button b = EmojiButton(emoji, text, width, height);
            b.Image = Emoji.Get(emoji, 28);
            b.TextAlign = ContentAlignment.MiddleCenter;
            b.ImageAlign = ContentAlignment.MiddleCenter;
            b.Font = MakeFont(fontPt, FontStyle.Bold);
            b.BackColor = Accent;
            b.ForeColor = Color.White;
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = AccentDark;
            b.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 64, 175);
            return b;
        }

        /// <summary>絵文字+見出し文字のラベル</summary>
        public static Control Heading(string emoji, string text, float pt)
        {
            FlowLayoutPanel p = new FlowLayoutPanel();
            p.AutoSize = true;
            p.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            p.WrapContents = false;
            p.Margin = new Padding(0, 0, 0, S(4));
            p.BackColor = Color.Transparent;
            PictureBox pic = new PictureBox();
            pic.Image = Emoji.Get(emoji, 22);
            pic.SizeMode = PictureBoxSizeMode.AutoSize;
            pic.Margin = new Padding(0, S(2), S(6), 0);
            Label l = new Label();
            l.UseMnemonic = false;
            l.Text = text;
            l.AutoSize = true;
            l.Font = MakeFont(pt, FontStyle.Bold);
            l.Margin = new Padding(0, S(2), 0, 0);
            p.Controls.Add(pic);
            p.Controls.Add(l);
            return p;
        }
    }

    /// <summary>Windows Vista以降のモダンなフォルダ選択ダイアログ(失敗時は従来のダイアログ)</summary>
    internal static class FolderPicker
    {
        public static string Pick(IWin32Window owner, string title, string initial)
        {
            try
            {
                return PickModern(owner, title, initial);
            }
            catch
            {
                using (FolderBrowserDialog d = new FolderBrowserDialog())
                {
                    d.Description = title;
                    d.ShowNewFolderButton = true;
                    if (!string.IsNullOrEmpty(initial) && Directory.Exists(initial)) d.SelectedPath = initial;
                    return d.ShowDialog(owner) == DialogResult.OK ? d.SelectedPath : null;
                }
            }
        }

        private static string PickModern(IWin32Window owner, string title, string initial)
        {
            IFileDialog dlg = (IFileDialog)new FileOpenDialogRCW();
            try
            {
                uint opts;
                dlg.GetOptions(out opts);
                dlg.SetOptions(opts | FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST);
                dlg.SetTitle(title);
                if (!string.IsNullOrEmpty(initial) && Directory.Exists(initial))
                {
                    IShellItem folder;
                    Guid iid = typeof(IShellItem).GUID;
                    if (SHCreateItemFromParsingName(initial, IntPtr.Zero, ref iid, out folder) == 0 && folder != null)
                        dlg.SetFolder(folder);
                }
                int hr = dlg.Show(owner != null ? owner.Handle : IntPtr.Zero);
                if (hr == unchecked((int)0x800704C7)) return null; // キャンセル
                if (hr != 0) Marshal.ThrowExceptionForHR(hr);
                IShellItem item;
                dlg.GetResult(out item);
                string path;
                item.GetDisplayName(SIGDN_FILESYSPATH, out path);
                return path;
            }
            finally
            {
                Marshal.ReleaseComObject(dlg);
            }
        }

        private const uint FOS_PICKFOLDERS = 0x20;
        private const uint FOS_FORCEFILESYSTEM = 0x40;
        private const uint FOS_PATHMUSTEXIST = 0x800;
        private const uint SIGDN_FILESYSPATH = 0x80058000;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(string path, IntPtr pbc, ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItem item);

        [ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
        private class FileOpenDialogRCW { }

        [ComImport, Guid("42f85136-db7e-439c-85f1-e4075d135fc8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileDialog
        {
            [PreserveSig] int Show(IntPtr parent);
            void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
            void SetFileTypeIndex(uint iFileType);
            void GetFileTypeIndex(out uint piFileType);
            void Advise(IntPtr pfde, out uint pdwCookie);
            void Unadvise(uint dwCookie);
            void SetOptions(uint fos);
            void GetOptions(out uint pfos);
            void SetDefaultFolder(IShellItem psi);
            void SetFolder(IShellItem psi);
            void GetFolder(out IShellItem ppsi);
            void GetCurrentSelection(out IShellItem ppsi);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
            void GetResult(out IShellItem ppsi);
            void AddPlace(IShellItem psi, int fdap);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
            void Close(int hr);
            void SetClientGuid(ref Guid guid);
            void ClearClientData();
            void SetFilter(IntPtr pFilter);
        }

        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }
    }
}
