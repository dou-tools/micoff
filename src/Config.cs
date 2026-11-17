using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Win32;

namespace MicMute;

sealed class Config
{
    public List<string> Hotkeys { get; set; } = ["Ctrl+Alt+Shift+M"];
    public string DeviceName { get; set; } = "";
    public double BeepVolume { get; set; } = 0.35;
    public bool Overlay { get; set; } = true;

    public static string Folder { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "micmute");
    static string FilePath => Path.Combine(Folder, "config.json");
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static Config Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Config>(File.ReadAllText(FilePath)) ?? new Config();
        }
        catch { }
        var config = new Config();
        config.Save();
        return config;
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
    }

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunValue = "micmute";

    public static bool StartWithWindows
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(RunValue) is string;
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value) key.SetValue(RunValue, $"\"{Environment.ProcessPath}\"");
            else key.DeleteValue(RunValue, false);
        }
    }
}
