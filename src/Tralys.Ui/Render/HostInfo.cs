namespace Tralys.Ui;

internal static class HostInfo
{
    [FramePath]
    public static ReadOnlySpan<object> ContextKeys(ComponentHost host) => host.UsedKeys.AsSpan();
}
