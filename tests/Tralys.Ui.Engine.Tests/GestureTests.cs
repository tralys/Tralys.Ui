using System.Numerics;

namespace Tralys.Ui.Engine.Tests;

public sealed class GestureTests
{
    [Test]
    public async Task AMouse_CrossesTheDragThresholdByItsStraightLineDistance_NotByEachAxis()
    {
        const float slop = GestureSlop.PointerDrag;

        await Assert
            .That(DragThreshold.IsCrossed(PointerKind.Mouse, new Vector2(slop - 0.1f, 0f)))
            .IsFalse();
        await Assert
            .That(DragThreshold.IsCrossed(PointerKind.Mouse, new Vector2(slop, 0f)))
            .IsTrue();
        await Assert
            .That(
                DragThreshold.IsCrossed(PointerKind.Mouse, new Vector2(0.75f * slop, 0.75f * slop))
            )
            .IsTrue();
    }

    [Test]
    public async Task ATouchOrPen_CrossesTheDragThresholdOnlyPastTheTapSlop_ByItsStraightLineDistance()
    {
        const float slop = GestureSlop.Tap;

        await Assert
            .That(DragThreshold.IsCrossed(PointerKind.Touch, new Vector2(slop, 0f)))
            .IsFalse();
        await Assert
            .That(DragThreshold.IsCrossed(PointerKind.Touch, new Vector2(slop + 0.1f, 0f)))
            .IsTrue();
        await Assert
            .That(DragThreshold.IsCrossed(PointerKind.Pen, new Vector2(slop, 0f)))
            .IsFalse();
        await Assert
            .That(DragThreshold.IsCrossed(PointerKind.Pen, new Vector2(0.75f * slop, 0.75f * slop)))
            .IsTrue();
    }
}
