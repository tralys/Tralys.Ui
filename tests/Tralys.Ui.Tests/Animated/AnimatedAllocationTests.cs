using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class AnimatedAllocationTests
{
    [Test]
    public async Task A_turning_spinner_a_sweeping_progress_and_a_scrolling_marquee_advanced_every_frame_allocate_nothing()
    {
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
                {
                    ui.Spinner();
                    ui.Progress(indeterminate: true, width: 200);
                    using (ui.Marquee(width: 100))
                    using (ui.Box(width: 300, height: 10)) { }
                }
            })
            .WithIcons();

        var bytes = driver.WorstFrameBytes(static d => d.Frame(0.1f));

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task A_progress_bar_and_ring_whose_value_steps_every_frame_allocate_nothing()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
            {
                ui.Progress(value: model.Count, width: 300);
                using (ui.CircularProgress(value: model.Count, width: 40, height: 40)) { }
            }
        });

        var bytes = driver.WorstFrameBytes(
            model,
            static (d, m) =>
            {
                m.Count = (m.Count + 1) % 101;
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task A_toast_shown_every_10_frames_for_half_a_second_aging_fading_and_leaving_allocates_nothing()
    {
        var feed = new Feed();
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Toaster(duration: 0.5f))
                    for (var i = 0; i < feed.Shown.Count; i++)
                    {
                        var note = feed.Shown[i];
                        ui.Toast(
                            title: "Your disk is nearly full",
                            severity: ToastSeverity.Warning,
                            onDismiss: note.Dismiss,
                            key: note.Id
                        );
                    }
            })
            .WithIcons();

        var bytes = driver.WorstFrameBytes(
            feed,
            static (d, f) =>
            {
                if (f.Frames++ % 10 == 0)
                    f.Show();
                d.Frame();
            }
        );

        await Assert.That(feed.Dismissed).IsGreaterThan(20);
        await Assert.That(bytes).IsEqualTo(0L);
    }

    sealed class Feed
    {
        readonly Stack<Note> _spare = new();
        long _shown;

        public ObservableList<Note> Shown { get; } = new(8);

        public int Frames { get; set; }

        public int Dismissed { get; private set; }

        public void Show()
        {
            var note = _spare.Count > 0 ? _spare.Pop() : new Note(this);
            note.Id = ++_shown;
            Shown.Add(note);
        }

        public void Remove(Note note)
        {
            if (!Shown.Remove(note))
                return;
            Dismissed++;
            _spare.Push(note);
        }
    }

    sealed class Note
    {
        public Note(Feed feed) => Dismiss = Callback.Of(feed, this, static (f, n) => f.Remove(n));

        public long Id { get; set; }

        public Callback Dismiss { get; }
    }
}
