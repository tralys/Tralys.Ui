using Tralys.Ui.Mvvm;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct CheckListProps<TItem>
{
    public IItemsSource<TItem>? Items { get; init; }

    public KeyOf<TItem>? ItemKey { get; init; }

    /// <summary>An item's check box label.</summary>
    public TextOf<TItem>? ItemText { get; init; }

    /// <summary>The group an item belongs to: the first item of each group shows a muted caption naming it.</summary>
    public TextOf<TItem>? ItemGroup { get; init; }

    /// <summary>The checked items' keys.</summary>
    public ISelection? Value { get; init; }

    /// <summary>A check box pressed, as a toggle of its item's key, which <c>SelectionSet.Apply</c> takes.</summary>
    public Callback<SelectionChange> OnValueChange { get; init; }

    /// <summary>The check boxes' size; <see cref="ComponentSize.Md"/> by default.</summary>
    public ComponentSize? Size { get; init; }

    public Length Width { get; init; }

    public Length Height { get; init; }

    /// <summary>The height past which the list scrolls.</summary>
    public Length MaxHeight { get; init; }

    public bool Disabled { get; init; }
}

public readonly record struct CheckListItemProps
{
    public UiKey Value { get; init; }

    public TextValue Label { get; init; }

    public bool Checked { get; init; }

    /// <summary>The caption above the check box, for the first item of a group.</summary>
    public TextValue Group { get; init; }
}

/// <summary>What a <see cref="CheckList{TItem}"/> tells its <see cref="CheckList.Item"/>s.</summary>
public readonly record struct CheckListContext(Callback<UiKey> Toggle, ComponentSize Size);

/// <summary>A <see cref="CheckList{TItem}"/>'s context key and parts.</summary>
public static partial class CheckList
{
    public static readonly ContextKey<CheckListContext> Context = new("CheckList");

    /// <summary>One check box of a check list, under a caption naming its group when it opens one.</summary>
    public sealed class Item : Component<CheckListItemProps>
    {
        protected override void Render(Ui ui, in CheckListItemProps p)
        {
            var c = ui.Use(Context);
            var self = ui.Self(p);
            using (ui.Box(direction: Direction.Column))
            {
                if (!p.Group.IsNull)
                    ui.Text(
                        p.Group,
                        variant: TextVariant.Caption,
                        color: ui.Theme.Color(ColorToken.FgMuted),
                        fontWeight: ui.Theme.Weight(WeightToken.Interface),
                        margin: new Thickness(0f, 16f, 0f, 8f)
                    );
                ui.Checkbox(
                    value: p.Checked,
                    onValueChange: Callback<bool>.Of(
                        self,
                        static (h, _) => h.Use(Context).Toggle.Invoke(h.Props.Value)
                    ),
                    label: p.Label,
                    size: c.Size
                );
            }
        }
    }
}

/// <summary>A column of check boxes, one per item, that scrolls once it reaches
/// <see cref="CheckListProps{TItem}.MaxHeight"/>, under a bar down its right edge, and never sideways.
/// <see cref="CheckListProps{TItem}.Value"/> holds the checked items' keys, and a press reports a toggle through
/// <see cref="CheckListProps{TItem}.OnValueChange"/>;
/// <see cref="CheckListFilter"/> keeps them for a filter that applies at once or on commit.</summary>
public sealed partial class CheckList<TItem> : Component<CheckListProps<TItem>>
{
    protected override void Render(Ui ui, in CheckListProps<TItem> p)
    {
        var items = p.Items ?? throw Missing(nameof(p.Items));
        var itemKey = p.ItemKey ?? throw Missing(nameof(p.ItemKey));
        var model = ui.Remember(static () => new CheckListModel<TItem>());
        model.Host = ui.Self(p);
        model.Changes = ui.State(0);
        model.Listen(items);
        _ = model.Changes.Value;
        ui.Provide(
            CheckList.Context,
            new CheckListContext(model.Toggle, p.Size ?? ComponentSize.Md)
        );
        var scroll = ui.ScrollHandle();
        using (
            ui.Box(
                direction: Direction.Column,
                width: p.Width,
                height: p.Height,
                maxHeight: p.MaxHeight
            )
        )
        {
            using (
                ui.Box(
                    direction: Direction.Column,
                    gap: 8f,
                    padding: new Thickness(
                        0f,
                        0f,
                        ListBarParts.Space(scroll, ScrollbarVisibility.Auto),
                        0f
                    ),
                    overflow: Overflow.ScrollY,
                    scroll: scroll,
                    panning: Panning.VerticalOnly,
                    grow: 1f
                )
            )
            {
                TextValue previous = default;
                for (var i = 0; i < items.Count; i++)
                {
                    var item = items.ItemAt(i);
                    var key = itemKey(item);
                    var group = p.ItemGroup is { } of ? of(item) : default;
                    ui.CheckListItem(
                        value: key,
                        label: p.ItemText is { } text ? text(item) : default,
                        @checked: p.Value?.Contains(key) == true,
                        group: !group.IsNull && !group.Span.SequenceEqual(previous.Span)
                            ? group
                            : default,
                        key: key
                    );
                    previous = group;
                }
            }
            ui.ListBar(scroll: scroll, visibility: ScrollbarVisibility.Auto);
        }
    }

    static ArgumentException Missing(string prop) => new($"A check list needs its {prop}.", prop);
}

[FramePath]
internal sealed class CheckListModel<TItem> : IItemsSourceListener, IDisposable
{
    internal readonly Callback<UiKey> Toggle;
    internal Host<CheckListProps<TItem>> Host;
    internal State<int> Changes = null!;
    IItemsSource<TItem>? _items;

    public CheckListModel() =>
        Toggle = Callback<UiKey>.Of(
            this,
            static (m, key) =>
                m.Host.Props.OnValueChange.Invoke(
                    new SelectionChange(SelectionAction.Toggle, key, key)
                )
        );

    internal void Listen(IItemsSource<TItem> items)
    {
        if (ReferenceEquals(items, _items))
            return;
        _items?.RemoveListener(this);
        items.AddListener(this);
        _items = items;
    }

    public void OnItemsChanged(IItemsSource source, in ItemsChange change) => Changes.Value++;

    public void Dispose()
    {
        _items?.RemoveListener(this);
        _items = null;
    }
}

/// <summary>A filter a <see cref="CheckList{TItem}"/> edits: the keys of the checked items pass it, and nothing
/// checked passes everything. With <see cref="AutoApply"/>, each check applies at once; without it, the checks are a
/// draft until <see cref="Commit"/> applies them or <see cref="Revert"/> puts back what is applied. Keys checked as it
/// is made apply at once.</summary>
public sealed class CheckListFilter
{
    readonly KeySet _checked = new();
    readonly KeySet _applied = new();

    public CheckListFilter(IEnumerable<UiKey>? initiallyChecked = null, bool autoApply = true)
    {
        AutoApply = autoApply;
        CommitCommand = new Command(Commit, () => IsDirty);
        RevertCommand = new Command(Revert);
        ClearAllCommand = new Command(ClearAll);
        Change = Callback<SelectionChange>.Of(this, static (f, change) => f.Changed(change));
        if (initiallyChecked is null)
            return;
        foreach (var key in initiallyChecked)
            _checked.Set(key, true);
        Commit();
    }

    /// <summary>What the check list shows checked; pass it as its <c>value</c>.</summary>
    public ISelection Checked => _checked;

    /// <summary>Takes a check list's toggles; pass it as its <c>onValueChange</c>.</summary>
    public Callback<SelectionChange> Change { get; }

    public bool AutoApply { get; }

    /// <summary>Whether a key is applied, so not every key passes. Tracked.</summary>
    public bool IsAnyActive => _applied.Count > 0;

    /// <summary>Runs <see cref="Commit"/>; it can run while the checks differ from what is applied.</summary>
    public Command CommitCommand { get; }

    public Command RevertCommand { get; }

    public Command ClearAllCommand { get; }

    /// <summary>Raised after each <see cref="Commit"/>.</summary>
    public event EventHandler? Applied;

    /// <summary>Whether <paramref name="key"/> passes. Tracked.</summary>
    public bool Matches(UiKey key) => _applied.Count == 0 || _applied.Contains(key);

    /// <summary>Applies the checked keys.</summary>
    public void Commit()
    {
        _applied.CopyFrom(_checked);
        Applied?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Checks the applied keys, and only those.</summary>
    public void Revert()
    {
        _checked.CopyFrom(_applied);
        ChecksChanged();
    }

    public void ClearAll()
    {
        _checked.Clear();
        ChecksChanged();
    }

    bool IsDirty => !_checked.SetEquals(_applied);

    void Changed(in SelectionChange change)
    {
        switch (change.Action)
        {
            case SelectionAction.Toggle:
                _checked.Toggle(change.Key);
                break;
            case SelectionAction.Clear:
                _checked.Clear();
                break;
            default:
                return;
        }
        ChecksChanged();
    }

    void ChecksChanged()
    {
        if (AutoApply)
            Commit();
    }
}
