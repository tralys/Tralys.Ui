namespace Tralys.Ui;

public sealed partial class UiContext
{
    IconSet? _icons;

    /// <summary>The glyphs the app's icon set draws for the roles components show, such as its generated
    /// <c>Icons.Set</c>; until it is given, an icon of a role draws nothing. Giving it re-renders every mounted host
    /// that read the theme, through which components resolve roles.</summary>
    /// <exception cref="InvalidOperationException">Another set was given before, or it is set from another
    /// thread.</exception>
    public IconSet? Icons
    {
        get => _icons;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            Dispatcher.VerifyAccess();
            if (ReferenceEquals(_icons, value))
                return;
            if (_icons is not null)
                throw new InvalidOperationException(
                    "The context's icon set is given once; show another set's views in another context."
                );
            _icons = value;
            foreach (var view in _views)
                view.IconsChanged();
        }
    }
}
