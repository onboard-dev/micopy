using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Micopy
{
    /// <summary>バックアップ元フォルダ1件分</summary>
    public class SourceEntry
    {
        public string Path;      // バックアップ元フォルダ
        public string DestName;  // バックアップ先での保存フォルダ名
        public bool Enabled = true;

        public SourceEntry Clone()
        {
            SourceEntry e = new SourceEntry();
            e.Path = Path; e.DestName = DestName; e.Enabled = Enabled;
            return e;
        }
    }

    /// <summary>アプリ設定(%APPDATA%\micopy\settings.xml に保存)</summary>
    public class AppSettings
    {
        public List<SourceEntry> Sources = new List<SourceEntry>();
        public string Destination = "";

        // ウィンドウ位置
        public string ScreenDevice = "";
        public Rectangle WindowBounds = Rectangle.Empty;
        public bool Maximized = false;

        // 前回結果
        public DateTime LastBackup = DateTime.MinValue;
        public string LastResult = "";

        public static string AppDataDir
        {
            get
            {
                return System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "micopy");
            }
        }

        public static string SettingsPath
        {
            get { return System.IO.Path.Combine(AppDataDir, "settings.xml"); }
        }

        public static string LogDir
        {
            get { return System.IO.Path.Combine(AppDataDir, "logs"); }
        }

        // ---------------- 保存・読込 ----------------

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                    return FromXml(XDocument.Load(SettingsPath), true);
            }
            catch { }
            return new AppSettings();
        }

        public void Save()
        {
            Directory.CreateDirectory(AppDataDir);
            string tmp = SettingsPath + ".tmp";
            ToXml(true).Save(tmp);
            if (File.Exists(SettingsPath)) File.Delete(SettingsPath);
            File.Move(tmp, SettingsPath);
        }

        /// <summary>設定のエクスポート(フォルダ登録とバックアップ先のみ)</summary>
        public void Export(string file)
        {
            ToXml(false).Save(file);
        }

        /// <summary>設定のインポート。形式が正しくなければ例外。</summary>
        public static AppSettings Import(string file)
        {
            XDocument doc = XDocument.Load(file);
            if (doc.Root == null || doc.Root.Name.LocalName != "micopy")
                throw new InvalidDataException("micopy の設定ファイルではありません。");
            return FromXml(doc, false);
        }

        private XDocument ToXml(bool includeLocal)
        {
            XElement root = new XElement("micopy", new XAttribute("version", "1"));
            root.Add(new XElement("Destination", Destination ?? ""));
            XElement src = new XElement("Sources");
            foreach (SourceEntry e in Sources)
            {
                src.Add(new XElement("Source",
                    new XAttribute("enabled", e.Enabled ? "true" : "false"),
                    new XAttribute("destName", e.DestName ?? ""),
                    e.Path ?? ""));
            }
            root.Add(src);
            if (includeLocal)
            {
                root.Add(new XElement("Window",
                    new XAttribute("screen", ScreenDevice ?? ""),
                    new XAttribute("x", WindowBounds.X),
                    new XAttribute("y", WindowBounds.Y),
                    new XAttribute("w", WindowBounds.Width),
                    new XAttribute("h", WindowBounds.Height),
                    new XAttribute("maximized", Maximized ? "true" : "false")));
                root.Add(new XElement("LastBackup",
                    new XAttribute("time", LastBackup == DateTime.MinValue ? "" :
                        LastBackup.ToString("o", CultureInfo.InvariantCulture)),
                    new XAttribute("result", LastResult ?? "")));
            }
            return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        }

        private static AppSettings FromXml(XDocument doc, bool includeLocal)
        {
            AppSettings s = new AppSettings();
            XElement root = doc.Root;
            XElement d = root.Element("Destination");
            s.Destination = d != null ? d.Value.Trim() : "";

            XElement src = root.Element("Sources");
            if (src != null)
            {
                foreach (XElement x in src.Elements("Source"))
                {
                    string p = x.Value.Trim();
                    if (p.Length == 0) continue;
                    SourceEntry e = new SourceEntry();
                    e.Path = p;
                    e.Enabled = Attr(x, "enabled", "true") != "false";
                    e.DestName = Attr(x, "destName", "");
                    if (e.DestName.Length == 0 || !IsValidDestName(e.DestName))
                        e.DestName = s.MakeUniqueDestName(SuggestDestName(p), null);
                    s.Sources.Add(e);
                }
            }

            if (includeLocal)
            {
                XElement w = root.Element("Window");
                if (w != null)
                {
                    s.ScreenDevice = Attr(w, "screen", "");
                    s.WindowBounds = new Rectangle(ToInt(Attr(w, "x", "0")), ToInt(Attr(w, "y", "0")),
                        ToInt(Attr(w, "w", "0")), ToInt(Attr(w, "h", "0")));
                    s.Maximized = Attr(w, "maximized", "false") == "true";
                }
                XElement lb = root.Element("LastBackup");
                if (lb != null)
                {
                    DateTime t;
                    if (DateTime.TryParse(Attr(lb, "time", ""), CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out t))
                        s.LastBackup = t;
                    s.LastResult = Attr(lb, "result", "");
                }
            }
            return s;
        }

        private static string Attr(XElement x, string name, string def)
        {
            XAttribute a = x.Attribute(name);
            return a != null ? a.Value : def;
        }

        private static int ToInt(string s)
        {
            int v; return int.TryParse(s, out v) ? v : 0;
        }

        // ---------------- 保存フォルダ名 ----------------

        /// <summary>フォルダパスから保存フォルダ名の候補を作る(C:\ → C_ドライブ など)</summary>
        public static string SuggestDestName(string path)
        {
            string p = (path ?? "").TrimEnd('\\', '/');
            string name = System.IO.Path.GetFileName(p);
            if (string.IsNullOrEmpty(name))
            {
                // ドライブ直下やUNCルート
                name = p.Replace(":", "").Replace("\\", "_").Trim('_');
                if (name.Length <= 1) name = name + "_ドライブ";
            }
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            name = name.Trim().TrimEnd('.');
            return name.Length == 0 ? "backup" : name;
        }

        public static bool IsValidDestName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0) return false;
            if (name == "." || name == ".." || name.EndsWith(".") || name.EndsWith(" ")) return false;
            return true;
        }

        /// <summary>他の登録と重ならない保存フォルダ名を返す</summary>
        public string MakeUniqueDestName(string baseName, SourceEntry except)
        {
            string name = baseName;
            int n = 2;
            while (Sources.Any(e => e != except &&
                   string.Equals(e.DestName, name, StringComparison.OrdinalIgnoreCase)))
            {
                name = baseName + " (" + n + ")";
                n++;
            }
            return name;
        }
    }
}
