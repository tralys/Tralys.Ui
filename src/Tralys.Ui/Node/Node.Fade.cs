namespace Tralys.Ui;

public sealed partial class Node
{
    internal int FadeLayer = -1;
    internal long FadeList;
    internal float FadeBase;
    internal Node? FadeOwner;
    internal bool FadeQueued;

    void ResetFade()
    {
        FadeLayer = -1;
        FadeList = 0;
        FadeBase = 0f;
        FadeOwner = null;
        FadeQueued = false;
    }
}
