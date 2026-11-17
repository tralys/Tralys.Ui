using Tralys.Ui.Mvvm;

namespace Tralys.Ui.PackageSmoke.ViewModels;

public sealed partial class CounterViewModel : ObservableObject
{
    [ObservableProperty]
    public partial float Count { get; set; }

    [DelegateCommand]
    void Increment() => Count++;
}
