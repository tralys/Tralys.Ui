# Changelog

All notable changes to this project are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-11-23

### Added

- `new FontCollection()`, a collection on FreeType that disposes its FreeType library with itself.
- `Story.Description`, `Story.SourcePath` and `Story.SourceLine`: each story's doc summary and where it is declared.

### Changed

- FreeType arrives from FreeType.Net: the same FreeType 2.13.2, loaded as `freetype_net`, whose Windows DLL needs no
  Visual C++ redistributable.
- The library keeps the compiler's null checks on, and its public API says what can be null:
  `Ui.State<T>` requires its initial value, `Ui.RefState<T>` returns `State<T?>`, `ContextKey<T>` requires its
  fallback, `TableRow<TItem>` requires its item, and `Pen.IsDashed` carries `[MemberNotNullWhen]`.
- TRL3001 rejects a non-nullable reference or type-parameter prop that is not `required`, since a call that omits it
  leaves it null. Mark such a prop `required` or declare it nullable.

### Fixed

- Autofocus, hover over what mounts under the pointer and a modal's first focus stop land in the update that mounts
  them, so the first frame shows them.
- An animated value that changes in the update it mounted in snaps rather than animating from a value never drawn.
- An `Editable` draws its focus outline only while its focus shows.
- Filled accent text takes the palette's colour, so light-theme Primary buttons read; it was pinned to black.

### Removed

- The `Tralys.Ui.FreeType` package. `Tralys.Ui.Engine` carries the FreeType backend, so an app drops the package and
  replaces `new FontCollection(new FreeTypeBackend())` with `new FontCollection()`.

## [0.1.0-preview.2] - 2026-11-19

### Added

- `forceStatesOn` on `Box` and `Grid`: `forceStates` then holds only on components of that type and what they
  contain, from their first render.
- `HorizontalScrollbar` on `ListBox`, `Table` and `Tree`: a bar along the bottom edge while the content scrolls
  sideways.
- Check lists and menus show a scrollbar beside rows that scroll past their maximum height.

### Changed

- `Box` and `Grid` take the new `forceStatesOn` parameter, which changes their signatures.
- Colour animations blend premultiplied, and gradients interpolate premultiplied, so a transparent end adds no tint
  of its colour channels.
- In debug builds, a component that renders a node into the box holding its caller's children throws, where the
  caller's next render used to remove the node silently.

### Fixed

- A focused `ListBox.Row` kept its focus ring only until the component rendering it rendered again.

## [0.1.0-preview.1] - 2026-11-19

Initial release.
