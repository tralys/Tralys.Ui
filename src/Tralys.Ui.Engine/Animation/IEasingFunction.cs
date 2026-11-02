namespace Tralys.Ui;

/// <summary>Maps an animation's linear progress, from 0 to 1, to the progress it shows.</summary>
public interface IEasingFunction
{
    [FramePath]
    double Ease(double normalizedTime);
}
