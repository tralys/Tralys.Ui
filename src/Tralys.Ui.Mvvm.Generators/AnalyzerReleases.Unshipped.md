; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
TRL0001 | Tralys.Ui | Error | Two inputs generated the same file name
TRL3101 | Tralys.Ui | Error | [DelegateCommand] owner must be partial
TRL3102 | Tralys.Ui | Error | [DelegateCommand] method returns neither void nor ValueTask
TRL3103 | Tralys.Ui | Error | [DelegateCommand] method takes more than one parameter
TRL3104 | Tralys.Ui | Error | [DelegateCommand] method is static
TRL3105 | Tralys.Ui | Error | [DelegateCommand] method has another [DelegateCommand] overload
TRL3201 | Tralys.Ui | Error | [ObservableProperty] owner must be partial
TRL3202 | Tralys.Ui | Error | [ObservableProperty] owner must derive from ObservableObject
TRL3203 | Tralys.Ui | Error | [ObservableProperty] must be a partial instance property with no body
TRL3204 | Tralys.Ui | Error | Struct observable property type does not implement IEquatable<T>
TRL3205 | Tralys.Ui | Error | [NotifyPropertyChangedFor] names no property of its type
TRL3206 | Tralys.Ui | Warning | [ObservableProperty] setter callback name is taken
