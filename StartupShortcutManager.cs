using System.Runtime.InteropServices;
using System.Text;

namespace ClickTamer;

/// <summary>Creates and updates the current user's Startup shortcut.</summary>
internal static class StartupShortcutManager
{
    private const string ShortcutName = "ClickTamer.lnk";
    private static readonly Guid ShellLinkClsid = new("00021401-0000-0000-C000-000000000046");

    private static string StartupDirectory
    {
        get
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("Windows не вернула путь к папке автозагрузки текущего пользователя.");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    private static string StartupShortcut => Path.Combine(StartupDirectory, ShortcutName);

    /// <summary>Creates or removes the Startup shortcut.</summary>
    public static void SetEnabled(bool enabled, string currentExe)
    {
        string sourceExe = Path.GetFullPath(currentExe);
        if (!File.Exists(sourceExe))
            throw new FileNotFoundException("Не найден исполняемый файл ClickTamer.", sourceExe);

        if (!enabled)
        {
            DeleteIfExists(StartupShortcut);
            StartupLog.Write("Autostart disabled: removed the ClickTamer Startup shortcut.");
            return;
        }

        const string args = "--startup";
        string link = StartupShortcut;
        SaveAndVerifyShortcut(sourceExe, args, link);
        StartupLog.Write($"Startup shortcut saved and verified. Link='{link}', Target='{sourceExe}', Arguments='{args}'. No executable copy was made.");
    }

    private static void SaveAndVerifyShortcut(string executable, string arguments, string destination)
    {
        string temp = Path.Combine(Path.GetDirectoryName(destination)!,
            $"ClickTamer-{Environment.ProcessId}-{Guid.NewGuid():N}.tmp.lnk");
        try
        {
            WriteShellLink(temp, executable, arguments);
            VerifyShellLink(temp, executable, arguments);
            File.Move(temp, destination, overwrite: true);
            VerifyShellLink(destination, executable, arguments);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch (Exception ex) { StartupLog.Write("Could not remove temporary shortcut file.", ex); }
        }
    }

    private static void WriteShellLink(string path, string executable, string arguments)
    {
        object? shellObject = null;
        try
        {
            Type shellType = Type.GetTypeFromCLSID(ShellLinkClsid, throwOnError: true)
                ?? throw new COMException("Не найден компонент Windows Shell Link.");
            shellObject = Activator.CreateInstance(shellType)
                ?? throw new COMException("Не удалось создать COM-объект ярлыка Windows.");

            var link = (IShellLinkW)shellObject;
            link.SetPath(executable);
            link.SetArguments(arguments);
            link.SetWorkingDirectory(Path.GetDirectoryName(executable)!);
            link.SetDescription("ClickTamer — подавление щелчков ЦАПа");
            link.SetIconLocation(executable, 0);
            link.SetShowCmd(1); // The app applies its saved launch setting before showing the form.
            ((IPersistFile)shellObject).Save(path, false);
        }
        finally
        {
            ReleaseCom(shellObject);
        }
    }

    private static void VerifyShellLink(string path, string expectedExe, string expectedArgs)
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length < 100)
            throw new IOException("Windows не сохранила корректный ярлык ClickTamer в папке автозагрузки.");

        object? shellObject = null;
        try
        {
            Type shellType = Type.GetTypeFromCLSID(ShellLinkClsid, throwOnError: true)
                ?? throw new COMException("Не найден компонент проверки ярлыка Windows.");
            shellObject = Activator.CreateInstance(shellType)
                ?? throw new COMException("Не удалось открыть объект ярлыка Windows.");
            ((IPersistFile)shellObject).Load(path, 0); // STGM_READ
            var link = (IShellLinkW)shellObject;
            var pathBuffer = new StringBuilder(32768);
            var argsBuffer = new StringBuilder(4096);
            link.GetPath(pathBuffer, pathBuffer.Capacity, IntPtr.Zero, 0);
            link.GetArguments(argsBuffer, argsBuffer.Capacity);

            if (!PathsEqual(pathBuffer.ToString(), expectedExe))
                throw new IOException($"Проверка ярлыка не пройдена: цель '{pathBuffer}' вместо '{expectedExe}'.");
            if (!string.Equals(argsBuffer.ToString(), expectedArgs, StringComparison.Ordinal))
                throw new IOException($"Проверка ярлыка не пройдена: аргументы '{argsBuffer}' вместо '{expectedArgs}'.");
        }
        finally
        {
            ReleaseCom(shellObject);
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    private static void ReleaseCom(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            try { Marshal.FinalReleaseComObject(value); } catch { }
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        try { return string.Equals(Path.GetFullPath(left).TrimEnd('\\', '/'), Path.GetFullPath(right).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase); }
        catch { return string.Equals(left, right, StringComparison.OrdinalIgnoreCase); }
    }

    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int piShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [Guid("0000010B-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }
}
