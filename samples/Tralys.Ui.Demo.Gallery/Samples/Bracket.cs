using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery;

public readonly record struct BracketProps
{
    /// <summary>The rounds, one column each; the first holds 2^(Rounds − 1) entries.</summary>
    public int Rounds { get; init; }

    /// <summary>Every entry's size: a card in each slot.</summary>
    public Size Card { get; init; }

    /// <summary>Renders entry i: every slot of the tree in round then slot order, so slots 2n and 2n + 1 of a round
    /// feed slot n of the next.</summary>
    public Slot<int> Entry { get; init; }
}

/// <summary>A sample of a component an app builds on the core: a knockout tree, one column per round, its entries
/// placed by position over a canvas that draws the connectors between rounds in the theme's border colour.</summary>
public sealed class Bracket : Component<BracketProps>
{
    const float ConnectorWidth = 32f;
    const float VGap = 8f;
    const float LineThickness = 1f;

    readonly record struct Lines(Color Line, int Rounds, Size Card);

    static readonly CanvasDraw<Lines> Draw = static (dc, s, info) =>
    {
        var pitch = s.Card.Height + VGap;
        for (var round = 0; round < s.Rounds - 1; round++)
        {
            var startX = round * (s.Card.Width + ConnectorWidth) + s.Card.Width;
            var midX = startX + ConnectorWidth / 2f;
            var nextX = (round + 1) * (s.Card.Width + ConnectorWidth);
            for (var slot = 0; slot < SlotsIn(s.Rounds, round); slot += 2)
            {
                var topY = Centre(round, slot, pitch);
                var bottomY = Centre(round, slot + 1, pitch);
                var midY = (topY + bottomY) / 2f;
                HLine(dc, s.Line, startX, topY, midX - startX);
                HLine(dc, s.Line, startX, bottomY, midX - startX);
                dc.DrawRectangle(
                    s.Line,
                    new Rect(
                        midX - LineThickness / 2f,
                        topY - LineThickness / 2f,
                        LineThickness,
                        bottomY - topY + LineThickness
                    )
                );
                HLine(dc, s.Line, midX, midY, nextX - midX);
            }
        }
    };

    protected override void Render(Ui ui, in BracketProps p)
    {
        var card = p.Card;
        var pitch = card.Height + VGap;
        using (
            ui.Box(
                width: p.Rounds * card.Width + (p.Rounds - 1) * ConnectorWidth,
                height: SlotsIn(p.Rounds, 0) * pitch
            )
        )
        {
            ui.Canvas(
                state: new Lines(ui.Theme.Color(ColorToken.Border), p.Rounds, card),
                draw: Draw,
                position: Position.Absolute,
                left: 0,
                top: 0,
                right: 0,
                bottom: 0
            );
            var index = 0;
            for (var round = 0; round < p.Rounds; round++)
            {
                for (var slot = 0; slot < SlotsIn(p.Rounds, round); slot++, index++)
                {
                    using (
                        ui.Box(
                            direction: Direction.Column,
                            position: Position.Absolute,
                            left: round * (card.Width + ConnectorWidth),
                            top: Centre(round, slot, pitch) - card.Height / 2f,
                            width: card.Width,
                            height: card.Height,
                            key: index
                        )
                    )
                        p.Entry.Render(ui, index, index);
                }
            }
        }
    }

    [FramePath]
    static int SlotsIn(int rounds, int round) => 1 << (rounds - 1 - round);

    [FramePath]
    static float Centre(int round, int slot, float pitch) => (slot + 0.5f) * (1 << round) * pitch;

    [FramePath]
    static void HLine(DrawingContext dc, Color line, float x, float y, float width) =>
        dc.DrawRectangle(line, new Rect(x, y - LineThickness / 2f, width, LineThickness));
}
