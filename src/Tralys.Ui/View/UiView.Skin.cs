namespace Tralys.Ui;

public sealed partial class UiView
{
    internal void SkinChanged() => InvalidateEnv(_root, EnvReads.Theme);

    internal void IconsChanged() => InvalidateEnv(_root, EnvReads.Theme);
}
