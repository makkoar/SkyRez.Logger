namespace SkyRez.Logger;

public static class Logger
{
    private static bool isInitialized = false;
    public static bool IsInitialized => isInitialized;

    private static readonly object @lock = new();
    private static string? logFilePath;
    private static ELogLevel minLogLevel = ELogLevel.All;
    private static bool useExactLevels = false;

    private static string LogsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PasswordManager", "Logs");

    public static bool IsLoggingToFileEnabled => !string.IsNullOrEmpty(logFilePath);

    public static void Initialize(ELogLevel logLevel = ELogLevel.All, bool exact = false)
    {
        lock (@lock)
        {
            if (isInitialized) return;

            minLogLevel = logLevel;
            useExactLevels = exact;

            string logFileName = $"{DateTime.Now:dd.MM.yyyy_HH-mm-ss}.log";
            logFilePath = Path.Combine(LogsDirectory, logFileName);

            if (!Directory.Exists(LogsDirectory))
                _ = Directory.CreateDirectory(LogsDirectory);

            File.WriteAllText(logFilePath, $"--- Сессия логирования начата в {DateTime.Now} ---\r\n");
            isInitialized = true;

            List<string> logFiles = [.. Directory.GetFiles(LogsDirectory, "*.log").OrderByDescending(File.GetCreationTime)];
            if (logFiles.Count >= 5) _ = Parallel.ForEach(logFiles.Skip(4), oldLog =>
            {
                try { File.Delete(oldLog); }
                catch (Exception ex) { Error(ex, $"Ошибка при удалении старого лога: {oldLog}"); }
            });
        }
    }

    private static int CountFlags(ELogLevel level)
    {
        int count = 0;
        byte value = (byte)level;
        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }
        return count;
    }

    public static void Debug(string message, [CallerMemberName] string source = "") =>
        Log(ELogLevel.Debug, source, message);

    public static void Debug(StringBuilder message, [CallerMemberName] string source = "") =>
        Log(ELogLevel.Debug, source, message.ToString());

    public static void Verbose(string message, [CallerMemberName] string source = "") =>
        Log(ELogLevel.Verbose, source, message);

    public static void Verbose(StringBuilder message, [CallerMemberName] string source = "") =>
        Log(ELogLevel.Verbose, source, message.ToString());

    public static void Information(string message, [CallerMemberName] string source = "") =>
        Log(ELogLevel.Information, source, message);

    public static void Information(StringBuilder message, [CallerMemberName] string source = "") =>
        Log(ELogLevel.Information, source, message.ToString());

    public static void Warning(string message, [CallerMemberName] string source = "") =>
        Log(ELogLevel.Warning, source, message);

    public static void Warning(StringBuilder message, [CallerMemberName] string source = "") =>
        Log(ELogLevel.Warning, source, message.ToString());

    public static void Warning(Exception ex, string message, [CallerMemberName] string source = "") =>
        Log(ELogLevel.Warning, source, new StringBuilder()
            .AppendLine(message)
            .AppendLine("------------ ИСКЛЮЧЕНИЕ ------------")
            .AppendLine(ex.ToString())
            .Append("------------------------------------"));

    public static void Warning(Exception ex, StringBuilder message, [CallerMemberName] string source = "") =>
        Warning(ex, message.ToString(), source);

    public static void Error(string message, [CallerMemberName] string source = "") =>
        Log(ELogLevel.Error, source, message);

    public static void Error(StringBuilder message, [CallerMemberName] string source = "") =>
        Log(ELogLevel.Error, source, message.ToString());

    public static void Error(Exception ex, string message, [CallerMemberName] string source = "") =>
        Log(ELogLevel.Error, source, new StringBuilder()
            .AppendLine(message)
            .AppendLine("------------ ИСКЛЮЧЕНИЕ ------------")
            .AppendLine(ex.ToString())
            .Append("------------------------------------"));

    public static void Error(Exception ex, StringBuilder message, [CallerMemberName] string source = "") =>
        Error(ex, message.ToString(), source);

    private static void Log(ELogLevel level, string source, string message)
    {
        if (!isInitialized) return;

        int flagCount = CountFlags(minLogLevel);

        bool shouldLog = useExactLevels || flagCount > 1
            ? (minLogLevel & level) != 0
            : level >= minLogLevel;

        if (!shouldLog) return;

        lock (@lock)
        {
            string timestamp = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss.fff");
            string state = $"[{level.ToString().ToUpper()}]";
            string logEntry = $"[{timestamp}] {state} [{source}]: {message}\r\n";

            if (!string.IsNullOrEmpty(logFilePath))
                try { File.AppendAllText(logFilePath, logEntry); }
                catch { }
        }
    }

    private static void Log(ELogLevel level, string source, StringBuilder message) => Log(level, source, message.ToString());
}