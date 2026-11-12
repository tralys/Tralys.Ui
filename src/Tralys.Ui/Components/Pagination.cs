using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct PaginationProps
{
    /// <summary>The page shown, counted from 1, and shown brought within the first and last page.</summary>
    public int Value { get; init; }

    public Callback<int> OnValueChange { get; init; }

    /// <summary>How many items there are.</summary>
    public int Total { get; init; }

    /// <summary>How many items a page holds; 0 or less puts every item on one page.</summary>
    public int PageSize { get; init; }

    public Length Width { get; init; }

    public bool Disabled { get; init; }
}

/// <summary>Steps through the pages of a list: "Page n of m" and first, previous, next and last buttons, each enabled
/// while there is a page that way. A step reports the page it asks for through
/// <see cref="PaginationProps.OnValueChange"/>, and the pagination shows <see cref="PaginationProps.Value"/>, brought
/// within the pages there are, so fewer items than its page needs leave it on the last page. It renders nothing while
/// there is one page or none.</summary>
public sealed class Pagination : Component<PaginationProps>
{
    protected override void Render(Ui ui, in PaginationProps p)
    {
        var pages = PagesFor(p.Total, p.PageSize);
        if (pages <= 1)
            return;
        var t = ui.Theme;
        var shown = Math.Clamp(p.Value, 1, pages);
        var self = ui.Self(p);
        using (ui.Box(align: Align.Center, width: p.Width))
        {
            ui.Text(
                ui.Format($"Page {shown} of {pages}"),
                fontWeight: t.Weight(WeightToken.Interface),
                shrink: 0f
            );
            using (ui.Box(align: Align.Center, justify: Justify.End, gap: 8, grow: 1f))
            {
                Step(ui, IconRole.ChevronsLeft, shown > 1, Callback.Of(self, static h => Go(h, 1)));
                Step(
                    ui,
                    IconRole.ChevronLeft,
                    shown > 1,
                    Callback.Of(self, static h => Go(h, Shown(h.Props) - 1))
                );
                Step(
                    ui,
                    IconRole.ChevronRight,
                    shown < pages,
                    Callback.Of(self, static h => Go(h, Shown(h.Props) + 1))
                );
                Step(
                    ui,
                    IconRole.ChevronsRight,
                    shown < pages,
                    Callback.Of(self, static h => Go(h, PagesFor(h.Props.Total, h.Props.PageSize)))
                );
            }
        }
    }

    [FramePath]
    static void Step(
        Ui ui,
        IconRole icon,
        bool enabled,
        Callback onClick,
        [System.Runtime.CompilerServices.CallerFilePath] string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int line = 0
    ) =>
        ui.IconButton(
            icon: IconGlyph.Of(icon),
            scheme: ColorScheme.Secondary,
            variant: ComponentVariant.GhostOutlined,
            size: ComponentSize.Xs,
            disabled: !enabled,
            onClick: onClick,
            file: file,
            line: line
        );

    [FramePath]
    static int PagesFor(int total, int pageSize) =>
        pageSize <= 0 ? 1 : Math.Max(1, (total + pageSize - 1) / pageSize);

    [FramePath]
    static int Shown(in PaginationProps p) => Math.Clamp(p.Value, 1, PagesFor(p.Total, p.PageSize));

    [FramePath]
    static void Go(Host<PaginationProps> h, int page)
    {
        ref readonly var p = ref h.Props;
        if (page >= 1 && page <= PagesFor(p.Total, p.PageSize) && page != Shown(p))
            p.OnValueChange.Invoke(page);
    }
}
