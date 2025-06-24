namespace SkyRez.Logger.Enums;

[Flags]
public enum ELogLevel : byte
{
    None        = 0,
    Debug       = 1 << 0,
    Verbose     = 1 << 1,
    Information = 1 << 2,
    Warning     = 1 << 3,
    Error       = 1 << 4,
    All         = Debug | Verbose | Information | Warning | Error
}