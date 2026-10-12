using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Micopy
{
    public enum BackupStatus { Success, CompletedWithErrors, Cancelled, Failed }

    /// <summary>UIスレッドから定期的に読み取る進行状況(lock(this) で保護)</summary>
    public class BackupProgress
    {
        public string Phase = "準備中";
        public string PhaseIcon = "hourglass";
        public bool Scanning = true;
        public string CurrentItem = "";
        public long TotalUnits = 0;
        public long DoneUnits = 0;
        public int ScannedFiles = 0;
        public int CopiedNew = 0;
        public int CopiedUpdated = 0;
        public int Unchanged = 0;
        public int DeletedFiles = 0;
        public int DeletedDirs = 0;
        public int Errors = 0;
        public long BytesCopied = 0;
        public int PlannedCopies = 0;
        public int PlannedDeletes = 0;

        public BackupProgress Snapshot()
        {
            lock (this) { return (BackupProgress)MemberwiseClone(); }
        }
    }

    public class BackupResult
    {
        public BackupStatus Status;
        public string Message = "";
        public string LogPath = "";
        public TimeSpan Elapsed;
        public BackupProgress Final;
        public List<string> ErrorLines = new List<string>();
    }

    /// <summary>
    /// 差分バックアップ(ミラー)エンジン。
    /// バックアップ先\保存フォルダ名 をバックアップ元と同じ内容に揃える。
    /// ・新しいファイル / サイズかタイムスタンプが違うファイル → コピー
    /// ・バックアップ元にないファイル/フォルダ → バックアップ先から削除
    /// ・バックアップ元フォルダ自体が見つからない場合は安全のため何もしない
    /// </summary>
    public class BackupEngine
    {
        private enum OpKind { CreateDir, CopyNew, CopyUpdate, DeleteFile, DeleteDir }

        private class Op
        {
            public OpKind Kind;
            public string Src;
            public string Dst;
            public long Size;
            public FileAttributes Attr;
            public DateTime CreationUtc;
            public DateTime WriteUtc;
        }

        private const long OpWeight = 8192;      // 削除やフォルダ作成1件あたりの進捗重み
        private const int BufferSize = 1 << 20;  // 1MB
        private const string TempSuffix = ".micopy_tmp";

        private readonly List<SourceEntry> _entries;
        private readonly string _destRoot;
        private volatile bool _cancel;
        private readonly List<string> _log = new List<string>();
        private readonly List<string> _errors = new List<string>();

        public readonly BackupProgress Progress = new BackupProgress();

        public BackupEngine(IEnumerable<SourceEntry> entries, string destRoot)
        {
            _entries = entries.Where(e => e.Enabled).Select(e => e.Clone()).ToList();
            _destRoot = destRoot;
        }

        public void Cancel() { _cancel = true; }
        public bool IsCancelRequested { get { return _cancel; } }

        // ======================================================
        public BackupResult Run()
        {
            Stopwatch sw = Stopwatch.StartNew();
            BackupResult result = new BackupResult();
            Log("===== micopy バックアップ開始 " + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + " =====");
            Log("バックアップ先: " + _destRoot);
            try
            {
                string destRoot = Path.GetFullPath(_destRoot);
                if (!Directory.Exists(destRoot))
                {
                    try { Directory.CreateDirectory(destRoot); }
                    catch (Exception ex)
                    {
                        throw new IOException("バックアップ先を作成できません。\nドライブが接続されているか確認してください。\n(" + ex.Message + ")");
                    }
                }

                // ---------- 1. 走査して計画を作る ----------
                SetPhase("変更点を調べています…", "search", true);
                List<Op> ops = new List<Op>();
                foreach (SourceEntry e in _entries)
                {
                    if (_cancel) break;
                    ScanEntry(e, destRoot, ops);
                }

                // 削除を先に(空き容量確保)、その後フォルダ作成とコピー
                List<Op> ordered = ops.Where(o => o.Kind == OpKind.DeleteFile || o.Kind == OpKind.DeleteDir)
                    .Concat(ops.Where(o => o.Kind == OpKind.CreateDir || o.Kind == OpKind.CopyNew || o.Kind == OpKind.CopyUpdate))
                    .ToList();

                long total = 0;
                foreach (Op o in ordered) total += OpWeight + (IsCopy(o) ? o.Size : 0);
                lock (Progress)
                {
                    Progress.TotalUnits = Math.Max(total, 1);
                    Progress.PlannedCopies = ordered.Count(IsCopy);
                    Progress.PlannedDeletes = ordered.Count(o => o.Kind == OpKind.DeleteFile || o.Kind == OpKind.DeleteDir);
                }
                Log(string.Format("計画: コピー {0} 件 / 削除 {1} 件 / 変更なし {2} 件",
                    Progress.PlannedCopies, Progress.PlannedDeletes, Progress.Unchanged));

                // ---------- 2. 実行 ----------
                if (!_cancel)
                {
                    SetPhase("バックアップしています…", "package", false);
                    foreach (Op o in ordered)
                    {
                        if (_cancel) break;
                        Execute(o);
                    }
                }

                if (_cancel)
                {
                    result.Status = BackupStatus.Cancelled;
                    result.Message = "バックアップを中止しました。";
                }
                else if (Progress.Errors > 0)
                {
                    result.Status = BackupStatus.CompletedWithErrors;
                    result.Message = "完了しました(エラー " + Progress.Errors + " 件)";
                }
                else
                {
                    result.Status = BackupStatus.Success;
                    result.Message = "バックアップが完了しました!";
                }
            }
            catch (Exception ex)
            {
                result.Status = BackupStatus.Failed;
                result.Message = ex.Message;
                Error("致命的エラー: " + ex.Message);
            }

            sw.Stop();
            result.Elapsed = sw.Elapsed;
            lock (Progress)
            {
                Progress.CurrentItem = "";
                Progress.DoneUnits = Progress.TotalUnits;
            }
            result.Final = Progress.Snapshot();
            Log(string.Format("結果: {0} / 新規 {1} / 更新 {2} / 削除 ファイル{3}・フォルダ{4} / 変更なし {5} / エラー {6} / {7}",
                result.Message, result.Final.CopiedNew, result.Final.CopiedUpdated,
                result.Final.DeletedFiles, result.Final.DeletedDirs, result.Final.Unchanged,
                result.Final.Errors, FormatTime(result.Elapsed)));
            result.ErrorLines.AddRange(_errors);
            result.LogPath = WriteLog();
            return result;
        }

        // ======================================================
        // 走査
        // ======================================================
        private void ScanEntry(SourceEntry entry, string destRoot, List<Op> ops)
        {
            string src;
            try { src = Path.GetFullPath(entry.Path); }
            catch (Exception ex) { Error("パスが不正です: " + entry.Path + " (" + ex.Message + ")"); return; }

            string dst = Path.Combine(destRoot, entry.DestName);
            Log("--- " + src + "  →  " + dst);

            if (!Directory.Exists(src))
            {
                // 外付けドライブ未接続などで丸ごと消してしまわないよう、スキップする
                Error("バックアップ元が見つからないためスキップしました(バックアップ先は変更しません): " + src);
                return;
            }
            if (IsSameOrInside(destRoot, src) || IsSameOrInside(src, dst))
            {
                Error("バックアップ元とバックアップ先が重なっているためスキップしました: " + src);
                return;
            }
            if (File.Exists(dst))
            {
                Error("保存先に同名のファイルがあるためスキップしました: " + dst);
                return;
            }

            bool dstExists = Directory.Exists(dst);
            if (!dstExists) ops.Add(new Op { Kind = OpKind.CreateDir, Src = src, Dst = dst });

            Stack<Tuple<string, string, bool>> stack = new Stack<Tuple<string, string, bool>>();
            stack.Push(Tuple.Create(src, dst, dstExists));

            while (stack.Count > 0)
            {
                if (_cancel) return;
                Tuple<string, string, bool> cur = stack.Pop();
                string sDir = cur.Item1, dDir = cur.Item2;
                bool dHas = cur.Item3;

                lock (Progress) Progress.CurrentItem = sDir;

                // バックアップ元の中身
                Dictionary<string, FileInfo> sFiles = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, DirectoryInfo> sDirs = new Dictionary<string, DirectoryInfo>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    foreach (FileSystemInfo fi in new DirectoryInfo(sDir).EnumerateFileSystemInfos())
                    {
                        DirectoryInfo di = fi as DirectoryInfo;
                        if (di != null)
                        {
                            // ジャンクション/シンボリックリンクは無限ループ防止のため対象外
                            if ((di.Attributes & FileAttributes.ReparsePoint) != 0)
                            {
                                Log("リンクのためスキップ: " + di.FullName);
                                continue;
                            }
                            sDirs[di.Name] = di;
                        }
                        else sFiles[fi.Name] = (FileInfo)fi;
                    }
                }
                catch (Exception ex)
                {
                    // 読めないフォルダはバックアップ先の内容を消さずにそのまま残す
                    Error("フォルダを読み取れません(このフォルダは変更しません): " + sDir + " (" + ex.Message + ")");
                    continue;
                }

                // バックアップ先の中身
                Dictionary<string, FileInfo> dFiles = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, DirectoryInfo> dDirs = new Dictionary<string, DirectoryInfo>(StringComparer.OrdinalIgnoreCase);
                if (dHas)
                {
                    try
                    {
                        foreach (FileSystemInfo fi in new DirectoryInfo(dDir).EnumerateFileSystemInfos())
                        {
                            DirectoryInfo di = fi as DirectoryInfo;
                            if (di != null) dDirs[di.Name] = di;
                            else dFiles[fi.Name] = (FileInfo)fi;
                        }
                    }
                    catch (Exception ex)
                    {
                        Error("バックアップ先フォルダを読み取れません: " + dDir + " (" + ex.Message + ")");
                        continue;
                    }
                }

                // 元にないもの → 削除
                foreach (DirectoryInfo d in dDirs.Values)
                    if (!sDirs.ContainsKey(d.Name))
                        ops.Add(new Op { Kind = OpKind.DeleteDir, Dst = d.FullName });
                foreach (FileInfo f in dFiles.Values)
                    if (!sFiles.ContainsKey(f.Name))
                        ops.Add(new Op { Kind = OpKind.DeleteFile, Dst = f.FullName });

                // ファイル比較
                foreach (FileInfo s in sFiles.Values)
                {
                    lock (Progress) Progress.ScannedFiles++;
                    FileInfo d;
                    string target = Path.Combine(dDir, s.Name);
                    if (!dFiles.TryGetValue(s.Name, out d))
                    {
                        ops.Add(MakeCopy(OpKind.CopyNew, s, target));
                    }
                    else if (NeedsUpdate(s, d))
                    {
                        ops.Add(MakeCopy(OpKind.CopyUpdate, s, target));
                    }
                    else
                    {
                        lock (Progress) Progress.Unchanged++;
                    }
                }

                // サブフォルダ
                foreach (DirectoryInfo s in sDirs.Values.OrderByDescending(x => x.Name, StringComparer.OrdinalIgnoreCase))
                {
                    string target = Path.Combine(dDir, s.Name);
                    bool exists = dDirs.ContainsKey(s.Name);
                    if (!exists) ops.Add(new Op { Kind = OpKind.CreateDir, Src = s.FullName, Dst = target });
                    stack.Push(Tuple.Create(s.FullName, target, exists));
                }
            }
        }

        private static Op MakeCopy(OpKind kind, FileInfo s, string target)
        {
            Op o = new Op { Kind = kind, Src = s.FullName, Dst = target };
            try
            {
                o.Size = s.Length;
                o.Attr = s.Attributes;
                o.CreationUtc = s.CreationTimeUtc;
                o.WriteUtc = s.LastWriteTimeUtc;
            }
            catch { }
            return o;
        }

        private static bool NeedsUpdate(FileInfo s, FileInfo d)
        {
            if (s.Length != d.Length) return true;
            // FAT/exFAT は2秒単位なので2秒までの差は同一とみなす
            double diff = Math.Abs((s.LastWriteTimeUtc - d.LastWriteTimeUtc).TotalSeconds);
            return diff > 2.0;
        }

        private static bool IsCopy(Op o) { return o.Kind == OpKind.CopyNew || o.Kind == OpKind.CopyUpdate; }

        // ======================================================
        // 実行
        // ======================================================
        private void Execute(Op o)
        {
            lock (Progress) Progress.CurrentItem = o.Src ?? o.Dst;
            long before = Progress.DoneUnits;
            try
            {
                switch (o.Kind)
                {
                    case OpKind.CreateDir:
                        Directory.CreateDirectory(o.Dst);
                        break;
                    case OpKind.DeleteFile:
                        ForceDeleteFile(o.Dst);
                        lock (Progress) Progress.DeletedFiles++;
                        Log("削除: " + o.Dst);
                        break;
                    case OpKind.DeleteDir:
                        ForceDeleteDir(o.Dst);
                        lock (Progress) Progress.DeletedDirs++;
                        Log("削除(フォルダ): " + o.Dst);
                        break;
                    case OpKind.CopyNew:
                    case OpKind.CopyUpdate:
                        CopyFile(o);
                        if (_cancel) return;
                        lock (Progress)
                        {
                            if (o.Kind == OpKind.CopyNew) Progress.CopiedNew++; else Progress.CopiedUpdated++;
                        }
                        Log((o.Kind == OpKind.CopyNew ? "新規: " : "更新: ") + o.Src);
                        break;
                }
            }
            catch (Exception ex)
            {
                Error((IsCopy(o) ? "コピー失敗: " + o.Src : "処理失敗: " + o.Dst) + " (" + ex.Message + ")");
            }
            finally
            {
                lock (Progress)
                {
                    // 失敗してもその分は進める
                    Progress.DoneUnits = before + OpWeight + (IsCopy(o) ? o.Size : 0);
                }
            }
        }

        private void CopyFile(Op o)
        {
            string dir = Path.GetDirectoryName(o.Dst);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string tmp = o.Dst + TempSuffix;
            long baseUnits;
            lock (Progress) baseUnits = Progress.DoneUnits;
            bool ok = false;
            try
            {
                byte[] buf = new byte[BufferSize];
                using (FileStream fs = new FileStream(o.Src, FileMode.Open, FileAccess.Read,
                           FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.SequentialScan))
                using (FileStream fd = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 4096))
                {
                    long done = 0;
                    int n;
                    while ((n = fs.Read(buf, 0, buf.Length)) > 0)
                    {
                        if (_cancel) return;
                        fd.Write(buf, 0, n);
                        done += n;
                        lock (Progress)
                        {
                            Progress.DoneUnits = baseUnits + Math.Min(done, o.Size);
                            Progress.BytesCopied += n;
                        }
                    }
                }
                if (File.Exists(o.Dst)) ForceDeleteFile(o.Dst);
                File.Move(tmp, o.Dst);
                ok = true;

                // タイムスタンプ → 属性 の順(読み取り専用だと時刻を変更できないため)
                File.SetCreationTimeUtc(o.Dst, o.CreationUtc);
                File.SetLastWriteTimeUtc(o.Dst, o.WriteUtc);
                FileAttributes a = o.Attr & (FileAttributes.ReadOnly | FileAttributes.Hidden |
                                             FileAttributes.System | FileAttributes.Archive);
                if (a != 0) File.SetAttributes(o.Dst, a);
            }
            finally
            {
                if (!ok)
                {
                    try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                }
            }
        }

        private static void ForceDeleteFile(string path)
        {
            FileAttributes a = File.GetAttributes(path);
            if ((a & (FileAttributes.ReadOnly | FileAttributes.System | FileAttributes.Hidden)) != 0)
                File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
        }

        private static void ForceDeleteDir(string path)
        {
            DirectoryInfo root = new DirectoryInfo(path);
            if ((root.Attributes & FileAttributes.ReparsePoint) == 0)
            {
                foreach (FileInfo f in root.EnumerateFiles())
                {
                    if ((f.Attributes & FileAttributes.ReadOnly) != 0) f.Attributes = FileAttributes.Normal;
                    f.Delete();
                }
                foreach (DirectoryInfo d in root.EnumerateDirectories())
                    ForceDeleteDir(d.FullName);
            }
            if ((root.Attributes & FileAttributes.ReadOnly) != 0) root.Attributes = FileAttributes.Directory;
            root.Delete(false);
        }

        // ======================================================
        // 補助
        // ======================================================
        private static bool IsSameOrInside(string path, string folder)
        {
            char sep = Path.DirectorySeparatorChar;
            string p = Path.GetFullPath(path).TrimEnd(sep) + sep;
            string f = Path.GetFullPath(folder).TrimEnd(sep) + sep;
            return p.StartsWith(f, StringComparison.OrdinalIgnoreCase);
        }

        private void SetPhase(string phase, string icon, bool scanning)
        {
            lock (Progress) { Progress.Phase = phase; Progress.PhaseIcon = icon; Progress.Scanning = scanning; }
        }

        private void Log(string s)
        {
            lock (_log) _log.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + s);
        }

        private void Error(string s)
        {
            lock (Progress) Progress.Errors++;
            lock (_log) _errors.Add(s);
            Log("[エラー] " + s);
        }

        public static string FormatTime(TimeSpan t)
        {
            if (t.TotalHours >= 1) return string.Format("{0}時間{1}分{2}秒", (int)t.TotalHours, t.Minutes, t.Seconds);
            if (t.TotalMinutes >= 1) return string.Format("{0}分{1}秒", t.Minutes, t.Seconds);
            return string.Format("{0:0.0}秒", t.TotalSeconds);
        }

        public static string FormatBytes(long b)
        {
            string[] u = { "B", "KB", "MB", "GB", "TB" };
            double v = b; int i = 0;
            while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
            return i == 0 ? b + " B" : v.ToString("0.0") + " " + u[i];
        }

        private string WriteLog()
        {
            try
            {
                Directory.CreateDirectory(AppSettings.LogDir);
                string path = Path.Combine(AppSettings.LogDir, "micopy_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log");
                lock (_log) File.WriteAllLines(path, _log, new UTF8Encoding(true));
                // 古いログは30件まで
                foreach (FileInfo old in new DirectoryInfo(AppSettings.LogDir).GetFiles("micopy_*.log")
                             .OrderByDescending(f => f.Name).Skip(30))
                {
                    try { old.Delete(); } catch { }
                }
                return path;
            }
            catch { return ""; }
        }
    }
}
