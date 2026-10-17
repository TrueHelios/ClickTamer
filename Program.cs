using System.Threading;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ClickTamer;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        bool startedByAutostart = HasArg(args, "--startup");
        StartupLog.Write($"Process start. Path='{Application.ExecutablePath}', Args='{string.Join(" ", args)}', AutoStart={startedByAutostart}");

        try
        {
            using var mutex = new Mutex(true, @"Local\ClickTamerAppV1", out bool createdNew);
            if (!createdNew)
            {
                StartupLog.Write("Another ClickTamer instance is already running; this instance exits.");
                return;
            }

            ApplicationConfiguration.Initialize();
            ConfigureInitialColorMode();
            var form = new MainForm(startedByAutostart);

            if (form.StartHiddenAtLaunch)
            {
                // Assigning MainForm here can make WinForms show it automatically.
                StartupLog.Write("Starting in tray-only mode with no main form assigned to the application context.");
                using var appContext = new TrayOnlyApplicationContext(form);
                Application.Run(appContext);
            }
            else
            {
                Application.Run(form);
            }
            StartupLog.Write("MainForm message loop exited normally.");
        }
        catch (Exception ex)
        {
            StartupLog.Write("Fatal startup/runtime exception", ex);
            if (!startedByAutostart)
            {
                try
                {
                    MessageBox.Show(
                        "ClickTamer не удалось запустить. Подробности записаны в журнал:\n" + StartupLog.LogPath + "\n\n" + ex.Message,
                        "ClickTamer — ошибка запуска",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                catch { }
            }
            Environment.ExitCode = 1;
        }
    }

    private static void ConfigureInitialColorMode()
    {
        // WinForms reads its native color mode when controls are created.
        bool dark = SystemThemePreference.IsDark;
        string appConfig = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClickTamer", "config.json");
        try
        {
            if (File.Exists(appConfig))
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(appConfig));
                JsonElement root = doc.RootElement;
                bool manualTheme = root.TryGetProperty("ThemeSelectedManually", out JsonElement manualValue)
                    && manualValue.ValueKind == JsonValueKind.True;
                bool hasDarkTheme = root.TryGetProperty("DarkTheme", out JsonElement themeValue)
                    && themeValue.ValueKind is JsonValueKind.True or JsonValueKind.False;

                if (manualTheme && hasDarkTheme)
                    dark = themeValue.GetBoolean();
            }
        }
        catch (Exception ex)
        {
            StartupLog.Write("Could not read saved theme before WinForms initialization; using the system theme.", ex);
        }

        Application.SetColorMode(dark && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)
            ? SystemColorMode.Dark
            : SystemColorMode.Classic);
    }

    private static bool HasArg(string[] args, string expected) =>
        args.Any(arg => string.Equals(arg, expected, StringComparison.OrdinalIgnoreCase));
}

internal static class SystemThemePreference
{
    public static bool IsDark
    {
        get
        {
            try
            {
                object? value = Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "AppsUseLightTheme",
                    1);
                return value is int setting && setting == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
