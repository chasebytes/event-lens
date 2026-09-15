namespace EventLens.Core;

public enum CollectionState
{
    NeverRun,
    Collecting,
    Healthy,
    Disabled,
    AccessDenied,
    Failed
}