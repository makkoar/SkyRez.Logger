namespace SkyRez.Logger;

/// <summary>Статический класс для логирования сообщений различных уровней в файл и консоль.</summary>
/// <remarks>Использует потокобезопасность и поддерживает различные уровни логирования.<br/>Сохраняет заданное количество последних логов, удаляя более старые.</remarks>
public static class Logger
{
    /// <summary>Флаг, указывающий, инициализирован ли логгер.</summary>
    /// <value>True, если логгер инициализирован, иначе false.</value>
    private static bool isInitialized = false;

    /// <summary>Показывает, инициализирован ли логгер.</summary>
    /// <value>True, если логгер инициализирован, иначе false.</value>
    public static bool IsInitialized => isInitialized;

#if NET9_0_OR_GREATER
    /// <summary>Объект блокировки для обеспечения потокобезопасности.</summary>
    /// <value>Экземпляр <see cref="Lock"/> для синхронизации потоков.</value>
    private static readonly Lock @lock = new();
#else
    /// <summary>Объект блокировки для обеспечения потокобезопасности.</summary>
    /// <value>Экземпляр <see cref="object"/> для синхронизации потоков.</value>
    private static readonly object @lock = new();
#endif

    /// <summary>Путь к текущему файлу лога.</summary>
    /// <value>Строка с абсолютным путем к файлу лога или null, если логирование в файл не настроено.</value>
    private static string? logFilePath;

    /// <summary>Минимальный уровень логирования.</summary>
    /// <value>Значение из <see cref="ELogLevel"/>, определяющее минимальный уровень сообщений для логирования.</value>
    private static ELogLevel minLogLevel = ELogLevel.All;

    /// <summary>Флаг, указывающий, использовать ли точное сравнение уровней логирования.</summary>
    /// <value>True для точного сравнения, иначе false.</value>
    private static bool useExactLevels = false;

    /// <summary>Пользовательская директория для логов.</summary>
    /// <value>Строка с путем к директории или null, если используется директория по умолчанию.</value>
    private static string? customLogsDirectory;

    /// <summary>Количество сохраняемых логов.</summary>
    /// <value>Максимальное количество лог-файлов, которые будут храниться.<br/>По умолчанию 5.</value>
    private static uint maxLogFiles = 5;

    /// <summary>Директория, в которую сохраняются логи.</summary>
    /// <value>Путь к директории логов.</value>
    private static string LogsDirectory =>
        customLogsDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppDomain.CurrentDomain.FriendlyName, "Logs");

    /// <summary>Показывает, включено ли логирование в файл.</summary>
    /// <value>True, если логирование в файл включено, иначе false.</value>
    public static bool IsLoggingToFileEnabled => !string.IsNullOrEmpty(logFilePath);

    /// <summary>Инициализирует логгер с заданным уровнем логирования и режимом сравнения уровней.</summary>
    /// <param name="logLevel">Минимальный уровень логирования.</param>
    /// <param name="exact">Флаг точного сравнения уровней.</param>
    /// <param name="maxLogs">Максимальное количество сохраняемых логов.<br/>Если равно 0, используется значение по умолчанию.</param>
    /// <remarks>Использует директорию по умолчанию для логов.</remarks>
    public static void Initialize(ELogLevel logLevel = ELogLevel.All, bool exact = false, uint maxLogs = 5u) =>
        InitializeInternal(logLevel, exact, null, maxLogs);

    /// <summary>Инициализирует логгер с указанием директории логов, уровня и режима сравнения.</summary>
    /// <param name="logsDirectory">Путь к директории для логов.</param>
    /// <param name="logLevel">Минимальный уровень логирования.</param>
    /// <param name="exact">Флаг точного сравнения уровней.</param>
    /// <param name="maxLogs">Максимальное количество сохраняемых логов.<br/>Если равно 0, используется значение по умолчанию.</param>
    public static void Initialize(string logsDirectory, ELogLevel logLevel = ELogLevel.All, bool exact = false, uint maxLogs = 5u) =>
        InitializeInternal(logLevel, exact, logsDirectory, maxLogs);

    /// <summary>Внутренняя инициализация логгера с настройкой директории, уровня, режима сравнения и количества логов.</summary>
    /// <param name="logLevel">Минимальный уровень логирования.</param>
    /// <param name="exact">Флаг точного сравнения уровней.</param>
    /// <param name="logsDirectory">Путь к директории для логов или null для значения по умолчанию.</param>
    /// <param name="maxLogs">Максимальное количество сохраняемых логов.<br/>Если равно 0, используется значение по умолчанию.</param>
    /// <exception cref="Exception">Может возникнуть при ошибке создания директории или записи в файл.</exception>
    private static void InitializeInternal(ELogLevel logLevel, bool exact, string? logsDirectory, uint maxLogs)
    {
        lock (@lock)
        {
            if (isInitialized) return;

            minLogLevel = logLevel;
            useExactLevels = exact;
            customLogsDirectory = logsDirectory;
            maxLogFiles = maxLogs is 0u ? 5u : maxLogs;

            string logFileName = $"{DateTime.Now:dd.MM.yyyy_HH-mm-ss}.log";
            logFilePath = Path.Combine(LogsDirectory, logFileName);

            if (!Directory.Exists(LogsDirectory))
                _ = Directory.CreateDirectory(LogsDirectory);

            File.WriteAllText(logFilePath, $"--- Сессия логирования начата в {DateTime.Now} ---\r\n");
            isInitialized = true;

            List<string> logFiles = [.. Directory.GetFiles(LogsDirectory, "*.log").OrderByDescending(File.GetCreationTime)];
            if ((uint)logFiles.Count >= maxLogFiles)
                _ = Parallel.ForEach(logFiles.Skip((int)maxLogFiles - 1), oldLog =>
                {
                    try { File.Delete(oldLog); }
                    catch (Exception ex) { Error(ex, $"Ошибка при удалении старого лога: {oldLog}"); }
                });
        }
    }

    /// <summary>Подсчитывает количество установленных флагов в уровне логирования.</summary>
    /// <param name="level">Уровень логирования для подсчета флагов.</param>
    /// <returns>Количество установленных флагов.</returns>
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

    /// <summary>Логирует сообщение уровня Debug.</summary>
    /// <param name="message">Текст сообщения.</param>
    /// <param name="source">Имя вызывающего метода.</param>
    public static void Debug(string message, [CallerMemberName] string source = "") =>
        Log(ELogLevel.Debug, source, message);

    /// <summary>Логирует сообщение уровня Debug из <see cref="StringBuilder"/>.</summary>
    /// <param name="message">Сообщение в виде <see cref="StringBuilder"/>.</param>
    /// <param name="source">Имя вызывающего метода.</param>
    public static void Debug(StringBuilder message, [CallerMemberName] string source = "") =>
        Log(ELogLevel.Debug, source, message.ToString());

    /// <summary>Логирует сообщение уровня Verbose.</summary>
    /// <param name="message">Текст сообщения.</param>
    /// <param name="source">Имя вызывающего метода.</param>
    public static void Verbose(string message, [CallerMemberName] string source = "") =>
        Log(ELogLevel.Verbose, source, message);

    /// <summary>Логирует сообщение уровня Verbose из <see cref="StringBuilder"/>.</summary>
    /// <param name="message">Сообщение в виде <see cref="StringBuilder"/>.</param>
    /// <param name="source">Имя вызывающего метода.</param>
    public static void Verbose(StringBuilder message, [CallerMemberName] string source = "") =>
        Log(ELogLevel.Verbose, source, message.ToString());

    /// <summary>Логирует информационное сообщение.</summary>
    /// <param name="message">Текст сообщения.</param>
    /// <param name="source">Имя вызывающего метода.</param>
    public static void Information(string message, [CallerMemberName] string source = "") =>
        Log(ELogLevel.Information, source, message);

    /// <summary>Логирует информационное сообщение из <see cref="StringBuilder"/>.</summary>
    /// <param name="message">Сообщение в виде <see cref="StringBuilder"/>.</param>
    /// <param name="source">Имя вызывающего метода.</param>
    public static void Information(StringBuilder message, [CallerMemberName] string source = "") =>
        Log(ELogLevel.Information, source, message.ToString());

    /// <summary>Логирует предупреждение.</summary>
    /// <param name="message">Текст сообщения.</param>
    /// <param name="source">Имя вызывающего метода.</param>
    public static void Warning(string message, [CallerMemberName] string source = "") =>
        Log(ELogLevel.Warning, source, message);

    /// <summary>Логирует предупреждение из <see cref="StringBuilder"/>.</summary>
    /// <param name="message">Сообщение в виде <see cref="StringBuilder"/>.</param>
    /// <param name="source">Имя вызывающего метода.</param>
    public static void Warning(StringBuilder message, [CallerMemberName] string source = "") =>
        Log(ELogLevel.Warning, source, message.ToString());

    /// <summary>Логирует предупреждение с исключением.</summary>
    /// <param name="ex">Экземпляр исключения.</param>
    /// <param name="message">Текст сообщения.</param>
    /// <param name="source">Имя вызывающего метода.</param>
    public static void Warning(Exception ex, string message, [CallerMemberName] string source = "") =>
        Log(ELogLevel.Warning, source, new StringBuilder()
            .AppendLine(message)
            .AppendLine("------------ ИСКЛЮЧЕНИЕ ------------")
            .AppendLine(ex.ToString())
            .Append("------------------------------------"));

    /// <summary>Логирует предупреждение с исключением и сообщением из <see cref="StringBuilder"/>.</summary>
    /// <param name="ex">Экземпляр исключения.</param>
    /// <param name="message">Сообщение в виде <see cref="StringBuilder"/>.</param>
    /// <param name="source">Имя вызывающего метода.</param>
    public static void Warning(Exception ex, StringBuilder message, [CallerMemberName] string source = "") =>
        Warning(ex, message.ToString(), source);

    /// <summary>Логирует сообщение об ошибке.</summary>
    /// <param name="message">Текст сообщения.</param>
    /// <param name="source">Имя вызывающего метода.</param>
    public static void Error(string message, [CallerMemberName] string source = "") =>
        Log(ELogLevel.Error, source, message);

    /// <summary>Логирует сообщение об ошибке из <see cref="StringBuilder"/>.</summary>
    /// <param name="message">Сообщение в виде <see cref="StringBuilder"/>.</param>
    /// <param name="source">Имя вызывающего метода.</param>
    public static void Error(StringBuilder message, [CallerMemberName] string source = "") =>
        Log(ELogLevel.Error, source, message.ToString());

    /// <summary>Логирует ошибку с исключением.</summary>
    /// <param name="ex">Экземпляр исключения.</param>
    /// <param name="message">Текст сообщения.</param>
    /// <param name="source">Имя вызывающего метода.</param>
    public static void Error(Exception ex, string message, [CallerMemberName] string source = "") =>
        Log(ELogLevel.Error, source, new StringBuilder()
            .AppendLine(message)
            .AppendLine("------------ ИСКЛЮЧЕНИЕ ------------")
            .AppendLine(ex.ToString())
            .Append("------------------------------------"));

    /// <summary>Логирует ошибку с исключением и сообщением из <see cref="StringBuilder"/>.</summary>
    /// <param name="ex">Экземпляр исключения.</param>
    /// <param name="message">Сообщение в виде <see cref="StringBuilder"/>.</param>
    /// <param name="source">Имя вызывающего метода.</param>
    public static void Error(Exception ex, StringBuilder message, [CallerMemberName] string source = "") =>
        Error(ex, message.ToString(), source);

    /// <summary>Выполняет логирование сообщения указанного уровня.</summary>
    /// <param name="level">Уровень логирования.</param>
    /// <param name="source">Имя источника сообщения.</param>
    /// <param name="message">Текст сообщения.</param>
    /// <remarks>Проверяет, инициализирован ли логгер и соответствует ли уровень фильтру.<br/>Потокобезопасно записывает сообщение в файл.</remarks>
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

    /// <summary>Выполняет логирование сообщения указанного уровня из <see cref="StringBuilder"/>.</summary>
    /// <param name="level">Уровень логирования.</param>
    /// <param name="source">Имя источника сообщения.</param>
    /// <param name="message">Сообщение в виде <see cref="StringBuilder"/>.</param>
    private static void Log(ELogLevel level, string source, StringBuilder message) => Log(level, source, message.ToString());
}