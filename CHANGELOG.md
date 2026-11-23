# Changelog

All notable changes to this project are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `new FontCollection()`, a collection on FreeType that disposes its FreeType library with itself.

### Changed

- FreeType arrives from FreeType.Net: the same FreeType 2.13.2, loaded as `freetype_net`, whose Windows DLL needs no
  Visual C++ redistributable.

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
