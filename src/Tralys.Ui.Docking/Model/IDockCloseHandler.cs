namespace Tralys.Ui.Docking;

/// <summary>An owner that closes some elements itself, so it can ask the user first. The dock hands it a user's close
/// of those elements instead of closing them, and the owner unregisters or hides each one it does close.</summary>
public interface IDockCloseHandler
{
    bool Handles(DockElement element);

    /// <summary>One user close, in tab order. The dock does not wait for it.</summary>
    void RequestClose(IReadOnlyList<DockElement> elements);
}
