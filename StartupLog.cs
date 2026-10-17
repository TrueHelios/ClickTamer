using System.Text;

namespace ClickTamer;

internal static class StartupLog
{
    private static readonly object Sync = new();
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClickTamer");

    public static string LogPath => Path.Combine(LogDirectory, "startup.log");

    public static void Write(string message, Exception? exception = null)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(LogDirectory);
                var line = new StringBuilder()
                    .Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"))
                    .Append(" | ").Append(message);
                if (exception is not null) line.AppendLine().Append(exception);
                line.AppendLine();
                File.AppendAllText(LogPath, line.ToString(), new UTF8Encoding(false));
            }
        }
        catch
        {
            // Ignore logging errors; they should not stop the app.
        }
    }
}
