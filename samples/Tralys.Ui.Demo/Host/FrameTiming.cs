namespace Tralys.Ui.Demo;

// Each phase is in Stopwatch ticks.
readonly record struct FrameTiming(
    long Pump,
    long Update,
    long Render,
    long Submit,
    long Present,
    uint DrawCalls,
    int Quads,
    bool Rebuilt
)
{
    public long Ui => Update + Render + Submit;
}
