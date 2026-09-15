namespace EventLens.Core;

[Flags]
public enum EventSeverity
{
    None = 0,
    Critical = 1,
    Error = 2,
    Warning = 4,
    Information = 8,
    Verbose = 16,
    All = Critical | Error | Warning | Information | Verbose
}