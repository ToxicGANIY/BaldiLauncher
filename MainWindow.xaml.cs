using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace BaldiLauncher;

// ---------- формат config.json ----------
public class SaveData
{
    public string? Path { get; set; }
    public long Seconds { get; set; }
    public string? Played { get; set; }   // только для удобного чтения
}

public class GameSave
{
    public string? Selected { get; set; }                       // последняя выбранная версия
    public Dictionary<string, SaveData> Versions { get; set; } = new();
}

// Описание версии для списка игр: название + своё описание
public record Ver(string Label, string Description = "");

// ---------- загрузка картинок ----------
// Порядок: 1) файл в папке Assets рядом с exe (можно подменить своей картинкой),
//          2) картинка, встроенная в сам exe (папка Assets проекта, Build Action = Resource)
static class Images
{
    static readonly Dictionary<string, BitmapImage?> cache = new();
    static readonly string[] Exts = { ".png", ".jpg", ".jpeg" };

    public static BitmapImage? Load(string slug)
    {
        if (cache.TryGetValue(slug, out var cached)) return cached;
        var img = FromDisk(slug) ?? FromResource(slug);
        cache[slug] = img;
        return img;
    }

    static BitmapImage? FromDisk(string slug)
    {
        foreach (var ext in Exts)
        {
            var p = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", slug + ext);
            if (!File.Exists(p)) continue;
            try
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.UriSource = new Uri(p);
                img.EndInit();
                img.Freeze();
                return img;
            }
            catch { }
        }
        return null;
    }

    static BitmapImage? FromResource(string slug)
    {
        foreach (var ext in Exts)
        {
            try
            {
                var uri = new Uri($"pack://application:,,,/Assets/{slug}{ext}", UriKind.Absolute);
                var info = Application.GetResourceStream(uri);   // бросает исключение, если ресурса нет
                if (info == null) continue;
                using var stream = info.Stream;
                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.StreamSource = stream;
                img.EndInit();
                img.Freeze();
                return img;
            }
            catch { }
        }
        return null;
    }
}

// ---------- одна версия игры ----------
public class VersionEntry : INotifyPropertyChanged
{
    readonly string _bannerSlug;
    string? _path;
    long _seconds;

    public string Label { get; }
    public string Description { get; }
    public string Key => Label == "" ? "default" : Label;
    public bool FromSteam { get; set; }

    public VersionEntry(string gameSlug, Ver v)
    {
        Label = v.Label;
        Description = v.Description;
        _bannerSlug = v.Label == "" ? gameSlug : $"{gameSlug}-{v.Label}";   // например original-1.4.3
    }

    public BitmapImage? Banner => Images.Load(_bannerSlug);

    public string? Path
    {
        get => _path;
        set { _path = value; Notify(nameof(Path), nameof(IsInstalled), nameof(NotInstalled)); }
    }

    public long Seconds
    {
        get => _seconds;
        set { _seconds = value; Notify(nameof(Seconds), nameof(PlayTime)); }
    }

    public bool IsInstalled => !string.IsNullOrEmpty(Path) && File.Exists(Path);
    public bool NotInstalled => !IsInstalled;

    public string PlayTime
    {
        get
        {
            var m = Seconds / 60;
            return m < 60 ? $"Наиграно: {m} мин" : $"Наиграно: {m / 60} ч {m % 60} мин";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void Notify(params string[] names)
    {
        foreach (var n in names) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}

// ---------- игра (пункт левого списка) ----------
public class GameEntry : INotifyPropertyChanged
{
    readonly string _description;
    VersionEntry _selected;

    public string Name { get; }
    public string Group { get; }
    public string Slug { get; }                 // баннер: Assets/<Slug>.png|jpg
    public string? SteamKey { get; }            // для автопоиска в Steam
    public string WikiUrl { get; }
    public List<VersionEntry> Versions { get; }

    public GameEntry(string name, string group, string slug, string description,
                     string? steamKey = null, Ver[]? versions = null, string? wiki = null)
    {
        Name = name; Group = group; Slug = slug; _description = description; SteamKey = steamKey;
        // wiki: название статьи ("Baldi's Basics Plus") или полная ссылка (https://...).
        // Если не указано — открывается поиск по вики по названию игры.
        WikiUrl = wiki == null ? MainWindow.WikiSearch(name)
                : wiki.StartsWith("http") ? wiki
                : MainWindow.WikiPage(wiki);

        Versions = versions is { Length: > 0 }
            ? versions.Select(v => new VersionEntry(slug, v)).ToList()
            : new List<VersionEntry> { new VersionEntry(slug, new Ver("")) };

        _selected = Versions[^1];   // по умолчанию выбрана последняя версия
        foreach (var v in Versions) v.PropertyChanged += (_, _) => NotifyAll();
    }

    public bool HasVersions => Versions.Count > 1;

    public VersionEntry SelectedVersion
    {
        get => _selected;
        set
        {
            if (value == null) { Notify(nameof(SelectedVersion)); return; }  // вернуть выбор обратно
            if (value == _selected) return;
            _selected = value;
            Notify(nameof(SelectedVersion));
            NotifyAll();
        }
    }

    // --- данные выбранной версии (для правой части окна) ---
    public string? Path => _selected.Path;
    public bool IsInstalled => _selected.IsInstalled;
    public bool NotInstalled => _selected.NotInstalled;
    public string PlayTime => _selected.PlayTime;
    public string FullTitle => HasVersions ? $"{Name} {_selected.Label}" : Name;

    // описание и баннер: свои у версии, если нет — общие у игры
    public string Description => _selected.Description != "" ? _selected.Description : _description;
    public BitmapImage? Banner => _selected.Banner ?? Images.Load(Slug);

    // --- для левого списка: серым, если не установлена ни одна версия ---
    public bool NoneInstalled => !Versions.Any(v => v.IsInstalled);

    public event PropertyChangedEventHandler? PropertyChanged;
    void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    void NotifyAll()
    {
        foreach (var n in new[] { nameof(Path), nameof(IsInstalled), nameof(NotInstalled),
                                  nameof(PlayTime), nameof(FullTitle), nameof(NoneInstalled),
                                  nameof(Description), nameof(Banner) })
            Notify(n);
    }
}

public partial class MainWindow : Window
{
    // Конфиг лежит рядом с exe: config.json
    static readonly string SaveFile = System.IO.Path.Combine(AppContext.BaseDirectory, "config.json");

    static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    const string Wiki = "https://baldis-basics-in-education-and-learning.fandom.com/ru/wiki/";

    public static string WikiPage(string title) => Wiki + Uri.EscapeDataString(title.Replace(' ', '_'));
    public static string WikiSearch(string query) => Wiki + "Special:Search?query=" + Uri.EscapeDataString(query);

    const string Main = "ОСНОВНЫЕ ИГРЫ";
    const string Demos = "ДЕМО И БИЛДЫ";

    // ----------------------------------------------------------------------
    // СПИСОК ИГР. У каждой версии можно задать своё описание (второй параметр Ver).
    // Если описание версии пустое — показывается общее описание игры.
    // Картинка версии: Assets/<slug>-<версия>.png, например original-1.4.3.png
    // ----------------------------------------------------------------------
    readonly List<GameEntry> games = new()
    {
        new("Baldi's Basics: Оригинал", Main, "original",
            "Классическая инди-хоррор игра, стилизованная под обучающие игры 90-х годов. Игроку нужно собрать 7 блокнотов и сбежать из школы, решая математические задачи и скрываясь от Балди.",
            versions: new Ver[]
            {
                new("1.0",   "Релиз"),
                new("1.1",   "Исправили управление мышью, хулигана замедлили в появлении и убрали его возможность возникать прямо на игроке. Огромные глобусы в кабинете директора заменили партами, ключи переименовали в «Principal’s Keys»"),
                new("1.2",   "Настройка чувствительности мыши теперь сохраняется, скакалка стала чуть сложнее, NPC перестали застревать на Gotta Sweep"),
                new("1.3",   "Скорость Балди теперь растёт по кривой: первое ускорение сильнее, каждое следующее слабее, а максимальная скорость ниже прежней. Качество звука Балди снизили, а без выносливости игрок больше не замедляется"),
                new("1.3.1", "Не исправляет проблему с мышью"),
                new("1.3.2", "Проблемы с мышью исправлены"),
                new("1.4.3", ""),
            },
            wiki: "Baldi's Basics Classic"),

        new("Baldi's Basics Classic Remastered", Main, "classic",
            "Обновлённая версия классической Baldi's Basics.",
            steamKey: "baldisbasicsclassic",
            wiki: "Baldi's Basics Classic Remastered"),

        new("Baldi's Basics Plus", Main, "plus",
            "Стелс-рогалик в духе «Балди»: прячься от учителя на процедурно генерируемых уровнях.",
            steamKey: "baldisbasicsplus",
            wiki: "Baldi's Basics Plus"),

        new("Baldi's Basics Birthday Bash", Main, "birthday",
            "Праздничная версия Baldi's Basics. Укажи путь к .exe, чтобы запускать её отсюда.",
            wiki: "Baldi's Basics Birthday Bash"),

        new("Challenges Demo", Demos, "challenges",
            "Baldi's Basics — Challenges Demo. Укажи путь к .exe нужной версии.",
            versions: new Ver[] { new("1.0", ""), new("1.1", "") },
            wiki: "Baldi's Basics - Challenges Demo"),

        new("Kickstarter Exclusive Demo", Demos, "kickstarter",
            "Эксклюзивная демо-версия Baldi's Basics для участников Kickstarter.",
            wiki: "Baldi's Basics Kickstarter Exclusive Demo"),

        new("Full Game Public Demo", Demos, "publicdemo",
            "Публичная демо-версия полной игры Baldi's Basics.",
            wiki: "Baldi's Basics Full Game Public Demo"),

        new("Field Trip Demo: Camping", Demos, "camping",
            "Baldi's Basics — Field Trip demo: Camping. Укажи путь к .exe нужной версии.",
            versions: new Ver[] { new("1.0", ""), new("1.1", "") },
            wiki: "Baldi's Basics - Field Trip demo: Camping"),

        // wiki не указана — кнопка «Вики» откроет поиск по названию игры
        new("Baldi's Basics Plus Development Build", Demos, "plusdev",
            "Девелоперская сборка Baldi's Basics Plus."),
    };

    public MainWindow()
    {
        InitializeComponent();
        LoadLogo();
        LoadIcon();
        LoadSaved();
        ScanSteam();

        var view = new ListCollectionView(games);
        view.GroupDescriptions.Add(new PropertyGroupDescription("Group"));
        Sidebar.ItemsSource = view;
        Sidebar.SelectedItem = games[0];
    }

    // ---------- логотип: Assets/logo.png (или .jpg) ----------
    void LoadLogo()
    {
        var logoImage = FindName("LogoImage") as System.Windows.Controls.Image;
        var logoText = FindName("LogoText") as System.Windows.Controls.TextBlock;
        if (logoImage == null || logoText == null) return;

        var img = Images.Load("logo");
        if (img == null) return;
        logoImage.Source = img;
        logoImage.Visibility = Visibility.Visible;
        logoText.Visibility = Visibility.Collapsed;
    }

    // ---------- иконка окна: Assets/icon.ico|png (если нет — берётся logo) ----------
    // сначала файл рядом с exe, потом встроенный ресурс
    void LoadIcon()
    {
        foreach (var name in new[] { "icon", "logo" })
            foreach (var ext in new[] { ".ico", ".png", ".jpg", ".jpeg" })
            {
                var disk = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", name + ext);
                try
                {
                    var uri = File.Exists(disk)
                        ? new Uri(disk)
                        : new Uri($"pack://application:,,,/Assets/{name}{ext}", UriKind.Absolute);
                    Icon = BitmapFrame.Create(uri);
                    return;
                }
                catch { }
            }
    }

    // ---------- сохранение ----------
    void LoadSaved()
    {
        try
        {
            if (!File.Exists(SaveFile)) return;
            var dict = JsonSerializer.Deserialize<Dictionary<string, GameSave>>(File.ReadAllText(SaveFile));
            if (dict == null) return;

            foreach (var g in games)
            {
                if (!dict.TryGetValue(g.Name, out var gs) || gs.Versions == null) continue;

                foreach (var v in g.Versions)
                {
                    if (!gs.Versions.TryGetValue(v.Key, out var d)) continue;
                    v.Seconds = d.Seconds;
                    if (d.Path != null && File.Exists(d.Path)) v.Path = d.Path;
                }

                var sel = g.Versions.FirstOrDefault(v => v.Key == gs.Selected);
                if (sel != null) g.SelectedVersion = sel;
            }
        }
        catch { }
    }

    void Save()
    {
        try
        {
            var dict = games.ToDictionary(g => g.Name, g => new GameSave
            {
                Selected = g.SelectedVersion.Key,
                Versions = g.Versions.ToDictionary(v => v.Key, v => new SaveData
                {
                    Path = v.FromSteam ? null : v.Path,   // Steam-пути находятся заново при старте
                    Seconds = v.Seconds,
                    Played = v.PlayTime.Replace("Наиграно: ", "")
                })
            });
            File.WriteAllText(SaveFile, JsonSerializer.Serialize(dict, JsonOpts));
        }
        catch { }
    }

    // ---------- автопоиск Steam ----------
    static string Norm(string s) => Regex.Replace(s.ToLowerInvariant(), "[^a-z0-9]", "");

    static IEnumerable<string> SteamLibraries()
    {
        var steam = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
        if (string.IsNullOrEmpty(steam)) yield break;
        steam = steam.Replace('/', '\\');
        yield return steam;

        var vdf = System.IO.Path.Combine(steam, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf)) yield break;
        foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
            yield return m.Groups[1].Value.Replace("\\\\", "\\");
    }

    void ScanSteam()
    {
        foreach (var lib in SteamLibraries().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var apps = System.IO.Path.Combine(lib, "steamapps");
            if (!Directory.Exists(apps)) continue;

            foreach (var acf in Directory.GetFiles(apps, "appmanifest_*.acf"))
            {
                var text = File.ReadAllText(acf);
                var name = Regex.Match(text, "\"name\"\\s+\"([^\"]+)\"").Groups[1].Value;
                var dir = Regex.Match(text, "\"installdir\"\\s+\"([^\"]+)\"").Groups[1].Value;
                if (name == "" || dir == "") continue;

                var game = games.FirstOrDefault(g => g.SteamKey != null && !g.Versions[0].IsInstalled
                                                     && Norm(name).Contains(g.SteamKey));
                if (game == null) continue;

                var folder = System.IO.Path.Combine(apps, "common", dir);
                if (!Directory.Exists(folder)) continue;

                var exe = Directory.GetFiles(folder, "*.exe")
                    .FirstOrDefault(f => !System.IO.Path.GetFileName(f)
                        .StartsWith("UnityCrashHandler", StringComparison.OrdinalIgnoreCase));
                if (exe == null) continue;

                var v = game.Versions[0];
                v.FromSteam = true;
                v.Path = exe;
            }
        }
    }

    // ---------- события ----------
    GameEntry? Current => Sidebar.SelectedItem as GameEntry;

    // ---------- плавная смена баннера ----------
    System.Windows.Controls.Image _front = null!, _back = null!;
    BitmapImage? _currentBanner;
    bool _bannerInit;
    GameEntry? _watched;

    void Sidebar_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_watched != null) _watched.PropertyChanged -= OnGameChanged;
        _watched = Current;
        if (_watched == null) return;
        _watched.PropertyChanged += OnGameChanged;
        SetBanner(_watched.Banner);
    }

    void OnGameChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GameEntry.Banner) && sender is GameEntry g)
            SetBanner(g.Banner);   // сменилась версия
    }

    void SetBanner(BitmapImage? img)
    {
        if (!_bannerInit)
        {
            _front = BannerA; _back = BannerB; _bannerInit = true;
        }
        else if (ReferenceEquals(img, _currentBanner)) return;

        _currentBanner = img;
        var show = _back;
        var hide = _front;
        show.Source = img;

        var t = TimeSpan.FromMilliseconds(300);
        show.BeginAnimation(OpacityProperty, new DoubleAnimation(1, t));
        hide.BeginAnimation(OpacityProperty, new DoubleAnimation(0, t));
        _front = show;
        _back = hide;
    }

    void Rescan_Click(object sender, RoutedEventArgs e) => ScanSteam();

    void Play_Click(object sender, RoutedEventArgs e)
    {
        var v = Current?.SelectedVersion;
        if (v == null || !v.IsInstalled) return;
        try
        {
            var proc = Process.Start(new ProcessStartInfo(v.Path!)
            {
                WorkingDirectory = System.IO.Path.GetDirectoryName(v.Path!)!,
                UseShellExecute = true
            });
            if (proc == null) return;

            var started = DateTime.Now;
            proc.EnableRaisingEvents = true;
            proc.Exited += (_, _) => Dispatcher.Invoke(() =>
            {
                v.Seconds += (long)(DateTime.Now - started).TotalSeconds;
                Save();
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Не удалось запустить");
        }
    }

    void Add_Click(object sender, RoutedEventArgs e)
    {
        var g = Current;
        if (g == null) return;
        var v = g.SelectedVersion;

        var dlg = new OpenFileDialog
        {
            Title = $"Выбери .exe для: {g.FullTitle}",
            Filter = "Игра (*.exe)|*.exe"
        };
        if (dlg.ShowDialog() != true) return;

        v.FromSteam = false;
        v.Path = dlg.FileName;
        Save();
    }

    void Folder_Click(object sender, RoutedEventArgs e)
    {
        var p = Current?.SelectedVersion.Path;
        if (string.IsNullOrEmpty(p) || !File.Exists(p)) return;
        Process.Start("explorer.exe", $"/select,\"{p}\"");   // открывает папку и выделяет exe
    }

    void Wiki_Click(object sender, RoutedEventArgs e)
    {
        var url = Current?.WikiUrl;
        if (string.IsNullOrEmpty(url)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Не удалось открыть ссылку"); }
    }
}
