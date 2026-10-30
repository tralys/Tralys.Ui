; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
TRL2001 | Tralys.Ui | Error | LINQ in a [FramePath] member or a member one calls
TRL2002 | Tralys.Ui | Error | Iterator or allocating enumerator in a [FramePath] member or a member one calls
TRL2003 | Tralys.Ui | Error | params array allocated in a [FramePath] member or a member one calls
TRL2004 | Tralys.Ui | Error | String concatenation in a [FramePath] member or a member one calls
TRL2005 | Tralys.Ui | Error | Capturing delegate in a [FramePath] member or a member one calls
TRL2006 | Tralys.Ui | Error | Boxing in a [FramePath] member or a member one calls
TRL2101 | Tralys.Ui | Warning | BCL INotifyCollectionChanged collection
TRL2102 | Tralys.Ui | Warning | Observable collection seen through a wider element type
TRL3001 | Tralys.Ui | Error | Props are not a readonly record struct of { get; init; } members
TRL3002 | Tralys.Ui | Error | A callback, handler, slot or draw lambda is not static
TRL3003 | Tralys.Ui | Error | A ui helper does not forward its caller's identity
TRL3004 | Tralys.Ui | Error | An identity-carrying ui call in a loop has no key
TRL3005 | Tralys.Ui | Error | A method that takes a Ui is not marked [FramePath]
TRL3006 | Tralys.Ui | Error | A component is not a sealed, stateless class
TRL3007 | Tralys.Ui | Error | A ui call argument is not named
TRL3008 | Tralys.Ui | Error | A component places its own root
TRL3010 | Tralys.Ui | Error | A Scope is not the resource of a using
TRL3011 | Tralys.Ui | Warning | A row slot remembers what it can't reset
TRL3012 | Tralys.Ui | Error | A hook keeps a value it can only compare by boxing
