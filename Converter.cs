// Converter by mysik — конвертация видео, музыки и картинок перетаскиванием (на основе ffmpeg).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Mysik;
using IOPath = System.IO.Path;
using WF = System.Windows.Forms;

[assembly: AssemblyTitle("Converter")]
[assembly: AssemblyDescription("Converter by mysik — конвертация видео, музыки и картинок")]
[assembly: AssemblyCompany("mysik")]
[assembly: AssemblyProduct("Converter by mysik")]
[assembly: AssemblyCopyright("by mysik")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace Converter
{
    static class Program
    {
        [STAThread]
        static void Main() { Boot.Run("Converter", "ConverterByMysik_SingleInstance", () => new MainWindow()); }
    }

    enum Kind { Video, Audio, Image }
    enum ItemState { Ready, Running, Done, Failed, Cancelled }

    class Target
    {
        public string Id, Label, Ext;
        public Kind Out;
        public bool Lossless;
        public Target(string id, string label, string ext, Kind outKind, bool lossless) { Id = id; Label = label; Ext = ext; Out = outKind; Lossless = lossless; }
    }

    class Choice
    {
        public string Label, Sub;
        public int Value;
        public Choice(string label, string sub, int value) { Label = label; Sub = sub; Value = value; }
    }

    class FileItem
    {
        public string Path, OutPath;
        public Kind Kind;
        public long Size;
        public ItemState State;
        public Runner Run;
        public string Error;

        public Border Row;
        public TextBlock Status, ActionIcon;
        public ScaleTransform Bar;
        public Border BarFill;
        public Button Action;
    }

    static class Formats
    {
        static readonly HashSet<string> VideoExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".3gp", ".mpg", ".mpeg", ".ts", ".mts", ".m2ts", ".vob", ".ogv" };
        static readonly HashSet<string> AudioExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".mp3", ".wav", ".flac", ".aac", ".m4a", ".ogg", ".opus", ".wma", ".aiff", ".aif", ".amr", ".ac3", ".mka" };
        static readonly HashSet<string> ImageExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".jfif", ".png", ".bmp", ".gif", ".webp", ".tif", ".tiff", ".ico", ".heic", ".heif", ".avif" };

        public static Kind? KindOf(string path)
        {
            string e = IOPath.GetExtension(path);
            if (VideoExt.Contains(e)) return Kind.Video;
            if (AudioExt.Contains(e)) return Kind.Audio;
            if (ImageExt.Contains(e)) return Kind.Image;
            return null;
        }

        public static readonly Target[] All =
        {
            new Target("mp4", "MP4", ".mp4", Kind.Video, false),
            new Target("webm", "WebM", ".webm", Kind.Video, false),
            new Target("gif", "GIF", ".gif", Kind.Video, false),
            new Target("mp3", "MP3", ".mp3", Kind.Audio, false),
            new Target("m4a", "M4A", ".m4a", Kind.Audio, false),
            new Target("ogg", "OGG", ".ogg", Kind.Audio, false),
            new Target("opus", "Opus", ".opus", Kind.Audio, false),
            new Target("flac", "FLAC", ".flac", Kind.Audio, true),
            new Target("wav", "WAV", ".wav", Kind.Audio, true),
            new Target("jpg", "JPG", ".jpg", Kind.Image, false),
            new Target("png", "PNG", ".png", Kind.Image, true),
            new Target("webp", "WEBP", ".webp", Kind.Image, false),
            new Target("bmp", "BMP", ".bmp", Kind.Image, true),
            new Target("ico", "ICO", ".ico", Kind.Image, true)
        };

        // Во что можно превратить файл этого типа.
        public static bool Allowed(Kind from, Target t)
        {
            if (from == Kind.Video) return t.Id == "mp4" || t.Id == "webm" || t.Id == "gif" || t.Id == "mp3" || t.Id == "m4a" || t.Id == "wav" || t.Id == "flac";
            if (from == Kind.Audio) return t.Out == Kind.Audio;
            return t.Out == Kind.Image;
        }

        public static List<Choice> Quality(Target t)
        {
            if (t.Lossless) return new List<Choice>();
            if (t.Id == "gif") return new List<Choice> { new Choice("Плавно", "15 кадров/с", 15), new Choice("Обычно", "12 кадров/с", 12), new Choice("Легче", "8 кадров/с", 8) };
            if (t.Out == Kind.Video) return new List<Choice> { new Choice("Высокое", "почти без потерь", 0), new Choice("Среднее", "меньше файл", 1), new Choice("Сжать", "минимальный размер", 2) };
            if (t.Out == Kind.Audio)
            {
                if (t.Id == "opus") return new List<Choice> { new Choice("192", "кбит/с", 192), new Choice("128", "кбит/с", 128), new Choice("96", "кбит/с", 96) };
                return new List<Choice> { new Choice("320", "кбит/с", 320), new Choice("192", "кбит/с", 192), new Choice("128", "кбит/с", 128) };
            }
            return new List<Choice> { new Choice("Высокое", "", 0), new Choice("Среднее", "", 1), new Choice("Сжать", "", 2) };
        }

        public static List<Choice> Sizes(Target t)
        {
            if (t.Id == "gif") return new List<Choice> { new Choice("640", "px", 640), new Choice("480", "px", 480), new Choice("320", "px", 320) };
            if (t.Id == "ico") return new List<Choice>();
            if (t.Out == Kind.Video) return new List<Choice> { new Choice("Как есть", "", 0), new Choice("1080p", "", 1080), new Choice("720p", "", 720), new Choice("480p", "", 480) };
            if (t.Out == Kind.Image) return new List<Choice> { new Choice("Как есть", "", 0), new Choice("1920", "px", 1920), new Choice("1280", "px", 1280), new Choice("800", "px", 800) };
            return new List<Choice>();
        }

        // Аргументы ffmpeg для одного файла.
        public static string Args(string input, string output, Target t, int quality, int size)
        {
            var sb = new StringBuilder("-hide_banner -nostdin -y -i ");
            sb.Append(Runner.Q(input)).Append(' ');
            string longSide = size > 0
                ? "scale='if(gt(iw,ih),min(" + size + ",iw),-2)':'if(gt(iw,ih),-2,min(" + size + ",ih))'"
                : null;
            switch (t.Id)
            {
                case "mp4":
                    sb.Append("-c:v libx264 -preset veryfast -crf ").Append(new[] { 20, 23, 28 }[quality]);
                    sb.Append(" -pix_fmt yuv420p -c:a aac -b:a 160k -movflags +faststart -sn");
                    if (size > 0) sb.Append(" -vf ").Append(Runner.Q("scale=-2:'min(" + size + ",ih)'"));
                    break;
                case "webm":
                    sb.Append("-c:v libvpx-vp9 -b:v 0 -crf ").Append(new[] { 30, 34, 40 }[quality]);
                    sb.Append(" -deadline good -cpu-used 4 -row-mt 1 -c:a libopus -b:a 128k -sn");
                    if (size > 0) sb.Append(" -vf ").Append(Runner.Q("scale=-2:'min(" + size + ",ih)'"));
                    break;
                case "gif":
                    sb.Append("-an -loop 0 -vf ").Append(Runner.Q(
                        "fps=" + quality + ",scale='min(" + size + ",iw)':-1:flags=lanczos,split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=4"));
                    break;
                case "mp3": sb.Append("-vn -c:a libmp3lame -b:a ").Append(quality).Append("k -map_metadata 0 -id3v2_version 3"); break;
                case "m4a": sb.Append("-vn -c:a aac -b:a ").Append(quality).Append("k -map_metadata 0"); break;
                case "ogg": sb.Append("-vn -c:a libvorbis -q:a ").Append(quality >= 320 ? 9 : quality >= 192 ? 6 : 4).Append(" -map_metadata 0"); break;
                case "opus": sb.Append("-vn -c:a libopus -b:a ").Append(quality).Append("k -map_metadata 0"); break;
                case "flac": sb.Append("-vn -c:a flac -map_metadata 0"); break;
                case "wav": sb.Append("-vn -c:a pcm_s16le"); break;
                case "jpg":
                    sb.Append("-frames:v 1 -update 1 -q:v ").Append(new[] { 2, 4, 7 }[quality]);
                    if (longSide != null) sb.Append(" -vf ").Append(Runner.Q(longSide));
                    break;
                case "webp":
                    sb.Append("-frames:v 1 -update 1 -c:v libwebp -quality ").Append(new[] { 92, 82, 70 }[quality]);
                    if (longSide != null) sb.Append(" -vf ").Append(Runner.Q(longSide));
                    break;
                case "png":
                case "bmp":
                    sb.Append("-frames:v 1 -update 1");
                    if (longSide != null) sb.Append(" -vf ").Append(Runner.Q(longSide));
                    break;
                case "ico":
                    sb.Append("-frames:v 1 -update 1 -vf ").Append(Runner.Q("scale=256:256:force_original_aspect_ratio=decrease"));
                    break;
            }
            sb.Append(" -progress pipe:1 -nostats ").Append(Runner.Q(output));
            return sb.ToString();
        }
    }

    static class Ffmpeg
    {
        static readonly string Local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        public static readonly string OwnBin = IOPath.Combine(Local, "Converter", "bin");
        static readonly string LoaderBin = IOPath.Combine(Local, "VideoLoader", "bin");
        public const string Url = "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";

        // ffmpeg из Video Loader подходит — второй раз не качаем.
        public static string Bin
        {
            get
            {
                if (File.Exists(IOPath.Combine(LoaderBin, "ffmpeg.exe")) && File.Exists(IOPath.Combine(LoaderBin, "ffprobe.exe"))) return LoaderBin;
                return OwnBin;
            }
        }
        public static string Exe { get { return IOPath.Combine(Bin, "ffmpeg.exe"); } }
        public static string Probe { get { return IOPath.Combine(Bin, "ffprobe.exe"); } }
        public static bool Ready { get { return File.Exists(Exe) && File.Exists(Probe); } }
    }

    class MainWindow : ShellWindow
    {
        FrameworkElement stateEmpty, stateSetup, stateFiles, optionsPanel, qualityBlock, sizeBlock;
        Rectangle dropRect;
        TextBlock setupSub, filesTitle, filesSub, mixedHint, folderText, convertText, convertIcon, qualityCaption, sizeCaption;
        Border setupBar;
        StackPanel fileList;
        WrapPanel formatPanel, qualityPanel, sizePanel;
        Button addBtn, clearBtn, folderBtn, resetFolderBtn, convertBtn;

        readonly List<FileItem> items = new List<FileItem>();
        Target target;
        Choice quality, size;
        string outFolder; // null — рядом с исходными
        bool running, stopRequested, setupRunning;
        readonly string iniPath = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Converter", "settings.ini");
        readonly Dictionary<Kind, string> lastTarget = new Dictionary<Kind, string>();

        public MainWindow()
        {
            LoadUi("Converter by mysik", 440, 760);

            stateEmpty = F<FrameworkElement>("StateEmpty");
            stateSetup = F<FrameworkElement>("StateSetup");
            stateFiles = F<FrameworkElement>("StateFiles");
            optionsPanel = F<FrameworkElement>("OptionsPanel");
            qualityBlock = F<FrameworkElement>("QualityBlock");
            sizeBlock = F<FrameworkElement>("SizeBlock");
            dropRect = F<Rectangle>("DropRect");
            setupSub = F<TextBlock>("SetupSub");
            filesTitle = F<TextBlock>("FilesTitle");
            filesSub = F<TextBlock>("FilesSub");
            mixedHint = F<TextBlock>("MixedHint");
            folderText = F<TextBlock>("FolderText");
            convertText = F<TextBlock>("ConvertText");
            convertIcon = F<TextBlock>("ConvertIcon");
            qualityCaption = F<TextBlock>("QualityCaption");
            sizeCaption = F<TextBlock>("SizeCaption");
            setupBar = F<Border>("SetupBar");
            fileList = F<StackPanel>("FileList");
            formatPanel = F<WrapPanel>("FormatPanel");
            qualityPanel = F<WrapPanel>("QualityPanel");
            sizePanel = F<WrapPanel>("SizePanel");
            addBtn = F<Button>("AddBtn");
            clearBtn = F<Button>("ClearBtn");
            folderBtn = F<Button>("FolderBtn");
            resetFolderBtn = F<Button>("ResetFolderBtn");
            convertBtn = F<Button>("ConvertBtn");

            AllowDrop = true;
            Root.AllowDrop = true;
            DragEnter += (s, e) => { e.Effects = DragDropEffects.Copy; dropRect.Stroke = Br(CGreen); };
            DragOver += (s, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
            DragLeave += (s, e) => dropRect.Stroke = Br(Hex("#3A3F4B"));
            Drop += (s, e) =>
            {
                dropRect.Stroke = Br(Hex("#3A3F4B"));
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null) AddPaths(files);
            };

            stateEmpty.MouseLeftButtonUp += (s, e) => PickFiles();
            addBtn.Click += (s, e) => PickFiles();
            clearBtn.Click += (s, e) => { if (!running) { items.Clear(); fileList.Children.Clear(); Refresh(); } };
            folderBtn.Click += (s, e) => ChooseFolder();
            resetFolderBtn.Click += (s, e) => { outFolder = null; SaveSettings(); UpdateFolder(); };
            convertBtn.Click += async (s, e) =>
            {
                if (running) { stopRequested = true; foreach (var it in items) if (it.Run != null && it.State == ItemState.Running) it.Run.Kill(); return; }
                await ConvertAll();
            };

            LoadSettings();
        }

        public override void Init()
        {
            UpdateFolder();
            Refresh();
        }

        // -------------------------------------------------- настройки

        void LoadSettings()
        {
            try
            {
                if (!File.Exists(iniPath)) return;
                foreach (var line in File.ReadAllLines(iniPath, Encoding.UTF8))
                {
                    int i = line.IndexOf('=');
                    if (i <= 0) continue;
                    string k = line.Substring(0, i), v = line.Substring(i + 1);
                    if (k == "folder" && v.Length > 0 && Directory.Exists(v)) outFolder = v;
                    if (k.StartsWith("target.")) { Kind kind; if (Enum.TryParse(k.Substring(7), out kind)) lastTarget[kind] = v; }
                }
            }
            catch { }
        }

        void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(IOPath.GetDirectoryName(iniPath));
                var lines = new List<string> { "folder=" + (outFolder ?? "") };
                foreach (var kv in lastTarget) lines.Add("target." + kv.Key + "=" + kv.Value);
                File.WriteAllLines(iniPath, lines, Encoding.UTF8);
            }
            catch { }
        }

        void UpdateFolder()
        {
            folderText.Text = outFolder ?? "Рядом с исходными файлами";
            resetFolderBtn.Visibility = outFolder != null ? Visibility.Visible : Visibility.Collapsed;
        }

        void ChooseFolder()
        {
            using (var d = new WF.FolderBrowserDialog())
            {
                d.Description = "Куда сохранять готовые файлы";
                d.ShowNewFolderButton = true;
                if (outFolder != null) d.SelectedPath = outFolder;
                if (d.ShowDialog() == WF.DialogResult.OK && !string.IsNullOrEmpty(d.SelectedPath))
                {
                    outFolder = d.SelectedPath;
                    SaveSettings();
                    UpdateFolder();
                }
            }
        }

        // -------------------------------------------------- файлы

        void PickFiles()
        {
            if (running) return;
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Title = "Выбери файлы для конвертации",
                Filter = "Видео, музыка и картинки|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.flv;*.webm;*.m4v;*.3gp;*.mpg;*.mpeg;*.ts;*.mts;*.m2ts;*.vob;*.ogv;" +
                         "*.mp3;*.wav;*.flac;*.aac;*.m4a;*.ogg;*.opus;*.wma;*.aiff;*.aif;*.amr;*.ac3;*.mka;" +
                         "*.jpg;*.jpeg;*.jfif;*.png;*.bmp;*.gif;*.webp;*.tif;*.tiff;*.ico;*.heic;*.heif;*.avif|Все файлы|*.*"
            };
            if (dlg.ShowDialog(this) == true) AddPaths(dlg.FileNames);
        }

        void AddPaths(IEnumerable<string> paths)
        {
            if (running) { Toast("Дождись окончания конвертации"); return; }
            int skipped = 0, added = 0;
            var all = new List<string>();
            foreach (var p in paths)
            {
                try
                {
                    if (Directory.Exists(p)) all.AddRange(Directory.GetFiles(p, "*", SearchOption.AllDirectories).Take(500));
                    else all.Add(p);
                }
                catch { }
            }
            foreach (var p in all)
            {
                var k = Formats.KindOf(p);
                if (k == null) { skipped++; continue; }
                if (items.Any(i => string.Equals(i.Path, p, StringComparison.OrdinalIgnoreCase))) continue;
                var it = new FileItem { Path = p, Kind = k.Value, State = ItemState.Ready };
                try { it.Size = new FileInfo(p).Length; } catch { }
                BuildRow(it);
                items.Add(it);
                fileList.Children.Add(it.Row);
                added++;
            }
            if (skipped > 0) Toast(Plural(skipped, "файл пропущен", "файла пропущено", "файлов пропущено") + ": это не видео, музыка или картинка");
            if (added > 0) Refresh();
        }

        void BuildRow(FileItem it)
        {
            var row = new Border { Background = Br(CCard), CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 10, 8, 10), Margin = new Thickness(0, 0, 0, 8) };
            var g = Columns(Auto, Star, Auto);
            string glyph = it.Kind == Kind.Video ? "" : it.Kind == Kind.Audio ? "" : "";
            var ic = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(9), Background = Br(CCard2), Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            var gl = Glyph(glyph, 14, CSub);
            gl.HorizontalAlignment = HorizontalAlignment.Center;
            ic.Child = gl;
            g.Children.Add(ic);

            var mid = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var name = Txt(IOPath.GetFileName(it.Path), 13, CText, true);
            name.ToolTip = it.Path;
            mid.Children.Add(name);
            it.Status = Txt(IOPath.GetExtension(it.Path).TrimStart('.').ToUpperInvariant() + " · " + FmtBytes(it.Size), 11.5, CSub, false);
            it.Status.Margin = new Thickness(0, 2, 0, 6);
            mid.Children.Add(it.Status);
            var track = new Grid { Height = 3 };
            track.Children.Add(new Border { Background = Br(CLine), CornerRadius = new CornerRadius(2) });
            it.Bar = new ScaleTransform(0, 1);
            it.BarFill = new Border { Background = Br(CGreen), CornerRadius = new CornerRadius(2), RenderTransform = it.Bar, RenderTransformOrigin = new Point(0, 0.5) };
            track.Children.Add(it.BarFill);
            mid.Children.Add(track);
            g.Children.Add(At(mid, 1));

            it.ActionIcon = Glyph("", 11, CSub);
            it.Action = new Button { Style = S("IconBtn"), Content = it.ActionIcon, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, ToolTip = "Убрать" };
            it.Action.Click += (s, e) => OnItemAction(it);
            g.Children.Add(At(it.Action, 2));

            row.Child = g;
            it.Row = row;
        }

        void OnItemAction(FileItem it)
        {
            if (it.State == ItemState.Running) { if (it.Run != null) it.Run.Kill(); return; }
            if (it.State == ItemState.Done && it.OutPath != null)
            {
                try { Process.Start("explorer.exe", "/select,\"" + it.OutPath + "\""); } catch { }
                return;
            }
            if (running) return;
            items.Remove(it);
            fileList.Children.Remove(it.Row);
            Refresh();
        }

        // -------------------------------------------------- выбор формата

        void Refresh()
        {
            if (setupRunning) return;
            bool any = items.Count > 0;
            ShowOnly(any ? stateFiles : stateEmpty, stateEmpty, stateSetup, stateFiles);
            convertBtn.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
            if (!any) return;

            var kinds = items.Select(i => i.Kind).Distinct().ToList();
            int v = items.Count(i => i.Kind == Kind.Video), a = items.Count(i => i.Kind == Kind.Audio), im = items.Count(i => i.Kind == Kind.Image);
            filesTitle.Text = Plural(items.Count, "файл", "файла", "файлов");
            var parts = new List<string>();
            if (v > 0) parts.Add(Plural(v, "видео", "видео", "видео"));
            if (a > 0) parts.Add(Plural(a, "трек", "трека", "треков"));
            if (im > 0) parts.Add(Plural(im, "картинка", "картинки", "картинок"));
            filesSub.Text = string.Join(" · ", parts) + " · " + FmtBytes(items.Sum(i => i.Size));

            var allowed = Formats.All.Where(t => kinds.All(k => Formats.Allowed(k, t))).ToList();
            bool mixed = allowed.Count == 0;
            mixedHint.Visibility = mixed ? Visibility.Visible : Visibility.Collapsed;
            mixedHint.Text = "Картинки нельзя конвертировать вместе с видео или музыкой. Убери лишние файлы или конвертируй по очереди.";
            optionsPanel.Visibility = mixed ? Visibility.Collapsed : Visibility.Visible;
            if (mixed) { target = null; UpdateConvertButton(); return; }

            Kind main = kinds.Count == 1 ? kinds[0] : Kind.Audio;
            string remembered;
            Target pick = null;
            if (target != null && allowed.Contains(target)) pick = target;
            else if (lastTarget.TryGetValue(main, out remembered)) pick = allowed.FirstOrDefault(t => t.Id == remembered);
            if (pick == null) pick = allowed[0];

            BuildPills(formatPanel, "fmt", allowed.Select(t => new Choice(t.Label, "", Array.IndexOf(Formats.All, t))).ToList(),
                Array.IndexOf(Formats.All, pick), c => { target = Formats.All[c.Value]; lastTarget[main] = target.Id; SaveSettings(); BuildOptions(); });
            target = pick;
            BuildOptions();
        }

        void BuildOptions()
        {
            var q = Formats.Quality(target);
            qualityBlock.Visibility = q.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            qualityCaption.Text = target.Out == Kind.Audio ? "БИТРЕЙТ" : target.Id == "gif" ? "ПЛАВНОСТЬ" : "КАЧЕСТВО";
            quality = q.Count > 0 ? q[target.Out == Kind.Audio ? 1 : 0] : null;
            if (q.Count > 0) BuildPills(qualityPanel, "q", q, quality.Value, c => { quality = c; UpdateConvertButton(); });
            if (quality != null) quality = q.First(c => c.Value == quality.Value);

            var sz = Formats.Sizes(target);
            sizeBlock.Visibility = sz.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            sizeCaption.Text = target.Id == "gif" ? "ШИРИНА" : target.Out == Kind.Image ? "ДЛИННАЯ СТОРОНА" : "РАЗМЕР";
            size = sz.Count > 0 ? sz[target.Id == "gif" ? 1 : 0] : null;
            if (sz.Count > 0) BuildPills(sizePanel, "sz", sz, size.Value, c => { size = c; UpdateConvertButton(); });

            UpdateConvertButton();
        }

        void BuildPills(WrapPanel panel, string group, List<Choice> choices, int selectedValue, Action<Choice> onPick)
        {
            panel.Children.Clear();
            var style = S("Pill");
            foreach (var c in choices)
            {
                var choice = c;
                var sp = new StackPanel();
                sp.Children.Add(new TextBlock { Text = c.Label, FontWeight = FontWeights.SemiBold, FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center });
                if (!string.IsNullOrEmpty(c.Sub))
                    sp.Children.Add(new TextBlock { Text = c.Sub, FontSize = 10.5, Foreground = Br(CSub), HorizontalAlignment = HorizontalAlignment.Center });
                var rb = new RadioButton { Style = style, Content = sp, GroupName = group, IsChecked = c.Value == selectedValue };
                rb.Checked += (s, e) => onPick(choice);
                panel.Children.Add(rb);
            }
        }

        void UpdateConvertButton()
        {
            if (running) return;
            int ready = items.Count(i => i.State != ItemState.Done);
            convertBtn.IsEnabled = target != null && ready > 0;
            convertIcon.Text = "";
            convertText.Text = target == null ? "Выбери файлы одного типа"
                : ready == 0 ? "Всё готово"
                : "Конвертировать " + (ready == 1 ? "в " + target.Label : Plural(ready, "файл", "файла", "файлов") + " в " + target.Label);
        }

        // -------------------------------------------------- конвертация

        async Task<bool> EnsureFfmpeg()
        {
            if (Ffmpeg.Ready) return true;
            bool yes = await Ask("Нужно скачать ffmpeg",
                "Конвертацию делает ffmpeg — около 140 МБ, скачивается с GitHub один раз. Если стоит Video Loader by mysik, используется его ffmpeg.",
                "Скачать", "Отмена");
            if (!yes) return false;

            setupRunning = true;
            ShowOnly(stateSetup, stateEmpty, stateSetup, stateFiles);
            convertBtn.Visibility = Visibility.Collapsed;
            string error = null;
            string zip = IOPath.Combine(IOPath.GetTempPath(), "mysik-ffmpeg.zip");
            try
            {
                Directory.CreateDirectory(Ffmpeg.OwnBin);
                using (var wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent] = "Converter";
                    wc.DownloadProgressChanged += (s, e) =>
                    {
                        double total = e.TotalBytesToReceive;
                        setupSub.Text = total > 0 ? FmtBytes(e.BytesReceived) + " из " + FmtBytes(total) : FmtBytes(e.BytesReceived);
                        var st = setupBar.RenderTransform as ScaleTransform;
                        if (st != null && total > 0) st.ScaleX = e.BytesReceived / total;
                    };
                    setupSub.Text = "Подключаюсь…";
                    await wc.DownloadFileTaskAsync(new Uri(Ffmpeg.Url), zip);
                }
                setupSub.Text = "Распаковываю…";
                error = await Task.Run(() =>
                {
                    try
                    {
                        using (var za = ZipFile.OpenRead(zip))
                            foreach (var n in new[] { "ffmpeg.exe", "ffprobe.exe" })
                            {
                                var entry = za.Entries.FirstOrDefault(x => x.FullName.Replace('\\', '/').EndsWith("/" + n, StringComparison.OrdinalIgnoreCase));
                                if (entry == null) return "в архиве нет " + n;
                                entry.ExtractToFile(IOPath.Combine(Ffmpeg.OwnBin, n), true);
                            }
                        return null;
                    }
                    catch (Exception ex) { return ex.Message; }
                });
            }
            catch (Exception ex) { error = ex.Message; }
            try { File.Delete(zip); } catch { }
            setupRunning = false;
            Refresh();

            if (error != null || !Ffmpeg.Ready)
            {
                await Ask("Не удалось скачать ffmpeg", "Проверь интернет и попробуй снова. " + (error ?? ""), "Понятно", null);
                return false;
            }
            Toast("ffmpeg установлен");
            return true;
        }

        async Task ConvertAll()
        {
            if (target == null || running) return;
            if (!await EnsureFfmpeg()) return;

            running = true;
            stopRequested = false;
            convertIcon.Text = "";
            convertText.Text = "Остановить";
            addBtn.IsEnabled = clearBtn.IsEnabled = folderBtn.IsEnabled = false;
            optionsPanel.IsEnabled = false;

            var t = target;
            int q = quality != null ? quality.Value : 0;
            int sz = size != null ? size.Value : 0;
            int done = 0, failed = 0;
            long inBytes = 0, outBytes = 0;

            foreach (var it in items.Where(i => i.State != ItemState.Done).ToList())
            {
                if (stopRequested) break;
                bool ok = await ConvertOne(it, t, q, sz);
                if (ok) { done++; inBytes += it.Size; try { outBytes += new FileInfo(it.OutPath).Length; } catch { } }
                else if (it.State == ItemState.Failed) failed++;
            }

            running = false;
            addBtn.IsEnabled = clearBtn.IsEnabled = folderBtn.IsEnabled = true;
            optionsPanel.IsEnabled = true;
            UpdateConvertButton();

            if (stopRequested) { Toast("Остановлено"); return; }
            string msg = done > 0 ? "Готово: " + Plural(done, "файл", "файла", "файлов") : "Ничего не сконвертировалось";
            if (done > 0 && inBytes > 0 && outBytes < inBytes) msg += " · на " + (int)Math.Round(100 - outBytes * 100.0 / inBytes) + "% меньше";
            if (failed > 0) msg += " · ошибок: " + failed;
            Toast(msg);
        }

        async Task<bool> ConvertOne(FileItem it, Target t, int q, int sz)
        {
            it.State = ItemState.Running;
            it.Error = null;
            it.Status.Text = "Подготовка…";
            it.Status.Foreground = Br(CSub);
            it.BarFill.Background = Br(CGreen);
            it.Bar.ScaleX = 0;
            it.ActionIcon.Text = "";
            it.Action.ToolTip = "Остановить";

            string dir = outFolder ?? IOPath.GetDirectoryName(it.Path);
            it.OutPath = FreeName(dir, IOPath.GetFileNameWithoutExtension(it.Path), t.Ext);

            double duration = 0;
            if (it.Kind != Kind.Image)
            {
                var pr = await Runner.Capture(Ffmpeg.Probe, "-v error -show_entries format=duration -of default=nw=1:nk=1 " + Runner.Q(it.Path), true);
                duration = Num(pr.Value.Trim().Split('\n')[0].Trim());
            }

            var sw = Stopwatch.StartNew();
            var errors = new List<string>();
            it.Run = new Runner();
            int code;
            try
            {
                code = await it.Run.Run(Ffmpeg.Exe, Formats.Args(it.Path, it.OutPath, t, q, sz), line => UI(() =>
                {
                    if (line.StartsWith("out_time_us=") || line.StartsWith("out_time_ms="))
                    {
                        double us = Num(line.Substring(line.IndexOf('=') + 1));
                        if (duration > 0 && us > 0)
                        {
                            double p = Math.Min(1, us / 1e6 / duration);
                            it.Bar.ScaleX = p;
                            double el = sw.Elapsed.TotalSeconds;
                            string eta = p > 0.02 ? " · осталось " + FmtTime(el / p - el) : "";
                            it.Status.Text = (int)(p * 100) + "%" + eta;
                        }
                    }
                    else if (!line.Contains("=") && line.Trim().Length > 0)
                    {
                        lock (errors) { errors.Add(line.Trim()); if (errors.Count > 30) errors.RemoveAt(0); }
                    }
                }), true, null);
            }
            catch (Exception ex) { code = -1; errors.Add(ex.Message); }

            await Task.Delay(100);

            if (it.Run.Killed)
            {
                it.State = ItemState.Cancelled;
                TryDelete(it.OutPath);
                SetFinal(it, "Остановлено", CSub, "", "Убрать");
                return false;
            }
            if (code == 0 && File.Exists(it.OutPath))
            {
                it.State = ItemState.Done;
                long outSize = 0;
                try { outSize = new FileInfo(it.OutPath).Length; } catch { }
                string diff = "";
                if (it.Size > 0 && outSize > 0)
                {
                    int pct = (int)Math.Round((outSize - it.Size) * 100.0 / it.Size);
                    diff = pct < 0 ? " (−" + (-pct) + "%)" : pct > 0 ? " (+" + pct + "%)" : "";
                }
                SetFinal(it, "Готово · " + t.Label + " · " + FmtBytes(outSize) + diff, CGreen, "", "Показать в папке");
                it.Bar.ScaleX = 1;
                Log("Готово: " + it.OutPath);
                return true;
            }

            it.State = ItemState.Failed;
            TryDelete(it.OutPath);
            string err;
            lock (errors) err = Explain(errors);
            SetFinal(it, err, CRed, "", "Убрать");
            it.Bar.ScaleX = 1;
            lock (errors) Log("Ошибка " + IOPath.GetFileName(it.Path) + ": " + string.Join(" | ", errors.Skip(Math.Max(0, errors.Count - 3))));
            return false;
        }

        static string Explain(List<string> errors)
        {
            string all = string.Join("\n", errors).ToLowerInvariant();
            if (all.Contains("no such file")) return "Файл не найден";
            if (all.Contains("permission denied")) return "Нет доступа к папке — выбери другую";
            if (all.Contains("invalid data found") || all.Contains("could not find codec") || all.Contains("unknown format")) return "Файл повреждён или формат не поддерживается";
            if (all.Contains("does not contain any stream") || all.Contains("output file #0 does not contain") || all.Contains("matches no streams")) return "В файле нет звука или картинки для этого формата";
            if (all.Contains("no space left")) return "Закончилось место на диске";
            var last = errors.LastOrDefault(e => e.Length > 0);
            return last == null ? "Не получилось" : (last.Length > 120 ? last.Substring(0, 117) + "…" : last);
        }

        void SetFinal(FileItem it, string status, Color color, string glyph, string tip)
        {
            it.Status.Text = status;
            it.Status.Foreground = Br(color);
            it.BarFill.Background = Br(color == CSub ? CIdle : color);
            it.ActionIcon.Text = glyph;
            it.Action.ToolTip = tip;
        }

        static string FreeName(string dir, string name, string ext)
        {
            string p = IOPath.Combine(dir, name + ext);
            for (int i = 1; File.Exists(p) && i < 1000; i++) p = IOPath.Combine(dir, name + " (" + i + ")" + ext);
            return p;
        }

        static void TryDelete(string p)
        {
            try { if (p != null && File.Exists(p)) File.Delete(p); } catch { }
        }
    }
}
