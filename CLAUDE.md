# Tralys.Ui

What Tralys.Ui is and what each package does: `README.md`. It is also every package's nuget.org page, so it stays
consumer-facing, and relative links break there. This file covers working in the repo.

## Build and test

- `dotnet build Tralys.Ui.slnx`. Warnings are errors. `Tralys.Ui.Bgfx` compiles its D3D11 shaders through `wine` on Linux.
- Test projects are TUnit executables: `dotnet run --project tests/<Project>`, never `dotnet test`.
  Narrow with `-- --treenode-filter '/*/*/<Class>/<Test>'`.
- The full suite is the `Test` loop in `.github/workflows/ci.yml`; a new test project joins `Tralys.Ui.slnx`.
- `Tralys.Ui.Engine.Tests` pins the engine with nothing above it; `Tralys.Ui.Generators.Tests` pins the generators and
  analyzers, `Tralys.Ui.Mvvm`'s and the theme compiler's included.
- `Tralys.Ui.Bgfx.Tests` runs on the OS's renderer: Vulkan on Linux (a GPU driver or lavapipe), D3D11 on Windows, Metal
  on macOS. CI runs the suite and `Tralys.Ui.AotSmoke` on all three; the other steps run on Linux only.
- `Tralys.Ui.AotSmoke` fails on any trim or AOT warning, then on any allocating frame; publishing needs clang:
  `dotnet publish tests/Tralys.Ui.AotSmoke -c Release && tests/Tralys.Ui.AotSmoke/bin/Release/net11.0/linux-x64/publish/Tralys.Ui.AotSmoke`
- `Tralys.Ui.PackageSmoke` restores the `Tralys.Ui` IDs only from `artifacts/packages`, so pack first:
  `dotnet pack Tralys.Ui.slnx -c Release -o artifacts/packages`, then
  `dotnet run --project tests/Tralys.Ui.PackageSmoke -c Release -p:TralysUiVersion=<packed version>`.
- `src/Tralys.Ui.Build` is the tool `TralysStripCallerPaths` runs; the Tralys.Ui package carries it under `tools/`, and
  only `Tralys.Ui.PackageSmoke` turns it on.
- The GPU vertex contract (`UiVertex`, `UiPrimitive`, `UiPaint`, `DrawBatch`, `DrawLayer`, `VertexRange` and the
  `DrawList` members exposing them) is `[Experimental("TRL5001")]`; `Tralys.Ui.Engine`, `Tralys.Ui.Bgfx`, `Tralys.Ui.Testing`,
  `samples/Tralys.Ui.Demo` and every project under `tests/` opt in with `NoWarn`.

## Zero allocation per frame

- Code that runs every frame carries `[FramePath]`, on the member or its type. TRL2001–2006 then reject LINQ,
  iterators, `params` arrays, string concatenation, capturing lambdas and boxing. Restructure; never suppress.
- The analyzers see marked code, every override and implementation of a marked member, and every member of the same
  compilation a frame path calls, directly or through others: a delegate, a handler or a lambda is no call, nor is what
  a `throw` builds, and a call through an interface or a virtual member reaches no implementation or override of it
  unless that member is marked. A component's `Render` is marked, and TRL3005 makes every method taking a `Ui` marked. `[ColdPath]`
  stops the reach at a member that runs once for what it makes, such as a box kept until its value changes. A change on
  the frame path also gets a case in a `*AllocationTests.cs` that asserts `AllocationGate.WorstFrameBytes(...)` is 0.
- `StorySweepAllocationTests` in `tests/Tralys.Ui.Demo.Tests` runs every story under every `Input`, so a component is
  swept once it has a story, and a new kind of input is a new `Input`. Nothing is exempt: a story that allocates is
  fixed. Its walk mounts every story twice on one stage, and the second must rent nothing a shared pool lacks: every
  shared pool counts its misses in `PoolMisses`, and storage a pooled object grows comes from `SharedArrays` and goes
  back when the object is released.
- `tests/Tralys.Ui.Bench` runs the scale workloads and prints each workload's median: `dotnet run --project
  tests/Tralys.Ui.Bench -c Release`; `--profile <workload>` runs one for ten seconds under a profiler.
- Time a frame where everything changes with `dotnet run --project samples/Tralys.Ui.Demo -c Release -- --workload
  --bench --headless --renderer vulkan --frames 600`: per-phase times, draw calls and the worst frame's bytes;
  `DemoHostAllocationTests` holds its worst frame to 0 B.

## Tests

- Interaction goes through `UiDriver` (`Click`, `Drag`, `Type`, `Press`, `Settle`, ...), never by raising events
  or setting state the input would have set.

## Components

- A component is `src/Tralys.Ui/Components/<Name>.cs`, with parts a family shares in a `*Parts.cs` beside it; the
  generators give each its `ui.<Name>(...)` call.
- Its stories are `[Stories<T>("<Category>")]` classes in
  `samples/Tralys.Ui.Demo.Gallery/Stories/<Category>/<Name>Stories.cs`; a generator registers them.
- Check a story by eye: `dotnet run --project samples/Tralys.Ui.Demo -c Release -- --story Switch.States --headless
  --frames 30 --screenshot <dir>/shot`, then `magick <dir>/shot.tga <dir>/shot.png` and read the PNG. It takes
  `--theme light` and `--density touch`; `--devtools` opens the inspector beside the story, and F12 opens and shuts it.
- `dotnet run --project samples/Tralys.Ui.Demo -c Release` opens the gallery in a window; `--gallery --story
  Switch.States` opens it at a story, `--states` holds it in each state, and `--panel` shows the view in a texture.
- `tests/Tralys.Ui.Demo.Tests/Goldens` holds every story's layout in both densities and its draw commands in Dark, Light
  and each held state that changes them, all made without a GPU, plus PNGs of the stories `PixelGoldenTests.Rendered`
  names, which lavapipe (`vulkan-swrast` on Arch), WARP on Windows and the Apple GPU all draw within tolerance. Held
  states are forced, not driven through `UiDriver`. After an intended change, rebaseline with `TRALYS_UPDATE_GOLDENS=1
  dotnet run --project tests/Tralys.Ui.Demo.Tests -c Release` and review the goldens' diff; a failing run writes what it
  got under `artifacts/goldens`, which CI uploads.
- `--capture-stories <dir>` writes every story's layout as JSON with no GPU. `--pixels --headless --renderer vulkan`
  draws too, and writes each layout's frame beside it as a top-down 24-bit TGA.

## Web gallery

- `samples/Tralys.Ui.Gallery.Web` runs the demo's gallery in a browser on WebGPU: the shell, models and asset images live
  in `samples/Tralys.Ui.Demo.Gallery` beside the stories, so both hosts show the same gallery. It stays out of
  `Tralys.Ui.slnx` and `ci.yml`, so nothing else waits on emscripten; `pages.yml` publishes it on a `v*` tag or by hand,
  and builds it weekly.
- It runs on CoreCLR (`UseMonoRuntime=false`), with crossgen2 compiling it to wasm ahead of time
  (`PublishReadyToRun`), on the .NET 12 daily SDK its `global.json` pins and finds in the repo's `.dotnet`, where
  its `install-sdk.sh` installs it with the `wasm-tools` workload from that SDK's feed; the libraries stay `net11.0`.
  From its folder: `dotnet publish -c Release --artifacts-path ../../artifacts/web`. The site lands in
  `artifacts/gallery/wwwroot`.
- Run it from a static server, then open `?story=Button.Variants`. Headless Chrome finds a WebGPU adapter only with
  `--enable-unsafe-webgpu --enable-features=Vulkan --use-angle=vulkan`, and a page's screenshot comes back blank, so
  read the canvas: `canvas.toDataURL()` inside a `requestAnimationFrame`.

## Dependencies and release

- Package versions live only in `Directory.Packages.props`; packable projects share `VersionPrefix` in
  `Directory.Build.props`.
- Each packable project lists its public API in `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`; a public
  change that isn't in them fails the build (RS0016/RS0017), and its code fix writes the line.
- Release: give `CHANGELOG.md` a section for the version and move every `PublicAPI.Unshipped.txt` entry into
  `PublicAPI.Shipped.txt`, then push `v<version>` from `master`. `release.yml` tests, packs, publishes to nuget.org
  and cuts the GitHub Release from that section; a `-` in the tag makes a prerelease.
