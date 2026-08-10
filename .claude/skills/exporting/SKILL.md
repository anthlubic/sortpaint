---
name: exporting
description: Build and export Sort Painting to a distributable form, currently web/HTML5 builds via 2dog. Use when asked to export, produce a build, make a web or browser version, publish to GitHub Pages or itch.io, or when a web build, the Pages deploy workflow, or `dotnet publish SortPaint.web` fails and needs diagnosing. Covers the 2dog web host, the wasm-tools prerequisite, serving and deploying AppBundle, and why Godot's own Web export preset is not used.
---

# Exporting

Export presets live in `export_presets.cfg` at the repo root, which is checked in
so build configuration is shared. It holds the desktop presets plus a `Web`
preset that the web build depends on, see below.

## Web builds

Web is built with [2dog](https://2dog.dev) (`outfox/2dog`, MIT), not with
Godot's own Web export preset.

### How this works, and why the Web preset still matters

Godot 4 refuses to *export* a C#/.NET project to Web. Running
`godot --export-release "Web"` directly fails at the config check with
"Exporting to Web is currently not supported in Godot 4 when using C#/.NET",
and no flag turns that off.

2dog sidesteps it by inverting the relationship: instead of Godot hosting the C#
assembly, a .NET WebAssembly app hosts a libgodot build of the engine. The game
assembly is unchanged, so scenes, scripts, and the editor workflow all stay as
they are.

Even so, **`export_presets.cfg` must define a preset named `Web`**. 2dog uses it
to pack the game content into `godot.pck`, which is a different code path from
the blocked full export. Deleting that preset breaks the publish with:

```
error : TwoDog: web publish exports the game pck via the 'Web' export preset,
which 'export_presets.cfg' does not define (or the file is missing).
```

Note that `2dog add --web` does not create the preset, so on a fresh setup it
has to be added by hand or through the Godot editor (Project > Export > Add >
Web). Keep the `[preset.N]` sections contiguous from 0; Godot stops reading the
file at the first gap. Point `<TwoDogWebExportPreset>` at a different preset
only if you deliberately want a second web configuration.

### Layout

`dnx 2dog add --web` generated the web host. The pieces:

| Path | Role |
| ---- | ---- |
| `SortPaint.web/` | Browser host. `.gdignore` keeps the Godot editor out of it. |
| `SortPaint.web/Program.cs` | Entry point. Constructs `twodog.Engine` and runs it. |
| `SortPaint.web/TwoDogWebBoot.cs` | Bootstrap, compiled into the *game* assembly by a `Compile Include` in the root csproj, because Godot resolves scripts by reflection against the assembly holding the generated initializer. |
| `SortPaint.web/wwwroot/` | Browser shell, `index.html` and favicons. |
| `SortPaint.slnx` | Solution. The web host is marked `Build=false` so a plain solution build does not need the wasm workload. |
| `global.json` | Pins a .NET 10 SDK, at the root and again in `SortPaint.web/`. |

2dog also bumped the game assembly to `net10.0` and added
`LIBGODOT_ENABLED` plus `AllowUnsafeBlocks`. The test project stays on `net8.0`
and is unaffected.

### Prerequisite

The wasm build workload, once per machine:

```bash
dotnet workload install wasm-tools
```

On this Arch box the SDK lives in the pacman-managed `/usr/share/dotnet`, so
that needs `sudo` and may need re-running after a `dotnet-sdk-bin` upgrade. The
Arch repo package also called `wasm-tools` is unrelated Rust tooling and is not
a substitute. Check with `dotnet workload list`.

### Building

```bash
dotnet publish SortPaint.web
```

Run it from the repo root. It defaults to Release via
`SortPaint.web/Directory.Build.props`; pass `-c Debug` to override. The publish
builds the managed app, exports the Godot content to `godot.pck`, statically
links the engine into `dotnet.native.wasm`, and assembles
`SortPaint.web/AppBundle/`. That folder is git-ignored.

Exporting the content uses the desktop editor packages on the machine, so the
Godot 4.7.x .NET editor needs to be installed for a publish, even though the
resulting bundle is pure static files.

A clean first publish takes several minutes, mostly emscripten linking and
size-optimising the engine. Expect roughly 100 MB in `AppBundle/`, of which
`godot.wasm` is about 46 MB and `godot.pck` is small. Verified working on
2026-08-09: the bundle boots and the game is playable in Firefox.

### Why the audio driver is Dummy

`project.godot` sets `audio/driver/driver="Dummy"`. The game has no sound, but
Godot's web platform initializes its audio driver at startup regardless, and
that driver builds a real `AudioContext` (`godot_audio_init` in
`_framework/dotnet.native.js`) whose worklet then streams silence forever. The
first touch resumes it (`godot_audio_resume`). iOS Safari counts a running
`AudioContext` as playback: it shows the speaker indicator on the tab and ducks
whatever the player was listening to. The Dummy driver means the web driver
never initializes, so no context is ever created.

Two things to know about that setting. Godot's driver lookup treats Dummy as a
special trailing entry, so it is worth confirming rather than assuming: run the
game and check `AudioServer.get_driver_name()`, which returns `"Dummy"` with
this in place. And it is project-wide, not web-only, so adding any sound to the
game means removing the line.

Verified on 2026-08-10 by publishing the bundle, serving it, and loading a copy
of `index.html` with the `AudioContext` constructor wrapped in a counter: zero
constructions through a full boot and a click.

### Serving

`AppBundle/` is a plain static site. Any static host will do, over HTTPS or on
localhost (the shell checks for a secure context, so `file://` is out).

It does **not** need cross-origin isolation. The 2dog engine build is
single-threaded, and the shell sets `GODOT_THREADS_ENABLED = false`, so
`Engine.getMissingFeatures` skips its `SharedArrayBuffer` and
`crossOriginIsolated` checks. Verified on 2026-08-09 by serving the bundle from
a bare `python3 -m http.server`: `crossOriginIsolated` is `false`,
`SharedArrayBuffer` is `undefined`, and `getMissingFeatures` still returns `[]`.

If you want the isolation headers anyway, this sends them:

```bash
python3 - <<'PY'
import functools, http.server
class H(http.server.SimpleHTTPRequestHandler):
    def end_headers(self):
        self.send_header("Cross-Origin-Opener-Policy", "same-origin")
        self.send_header("Cross-Origin-Embedder-Policy", "require-corp")
        super().end_headers()
http.server.ThreadingHTTPServer(
    ("127.0.0.1", 8060),
    functools.partial(H, directory="SortPaint.web/AppBundle"),
).serve_forever()
PY
```

Then open `http://127.0.0.1:8060/index.html`.

Two traps when checking a build in an automated browser. Godot drives its main
loop from `requestAnimationFrame`, which a hidden or minimised tab throttles to
a stop, so bring the tab to the front before reading anything into a stalled
boot (`document.visibilityState` must be `visible`). And a stalled boot looks
identical whatever the cause, so read the console rather than the page: a
missing `_framework` asset parks on the same bar with the same empty
`status-notice`. Do not judge a bundle by `Engine.getMissingFeatures` alone,
that only covers the browser feature checks and passes fine on a bundle that
cannot start.

`dotnet serve` also sends the headers if you would rather install it
(`dotnet tool install --global dotnet-serve`, then
`dotnet serve --directory SortPaint.web/AppBundle -o`).

The publish writes `.br` and `.gz` siblings for the large files, so a real host
should be configured to serve those; `godot.wasm` alone is about 46 MB
uncompressed.

Opening `index.html` over `file://` never works.

### Publishing

Pushing to `main` deploys the bundle to GitHub Pages at
<https://anthlubic.github.io/sortpaint/>, via `.github/workflows/pages.yml`.
The workflow runs the rules tests, publishes the bundle the same way you would
locally, lays it out under a per-build directory (below), and hands that to
`actions/deploy-pages`. Nothing about the build is CI-specific: no Godot install
is needed, because 2dog exports the pck with the editor libgodot from its own
NuGet packages.

### Why the site is not served from its root

Pages sends `cache-control: max-age=600` on every file and offers no way to
change it. That makes a reload straddling a deploy able to mix fresh files with
ten-minute-old ones, and the mix is fatal rather than untidy: `dotnet.boot.js`
pins every asset by name and hash, and one stale or failed fetch rejects
`mono_download_assets`. It happened on iOS Safari on 2026-08-09, the first time
this site was ever redeployed, and it looked exactly like a broken build.
Clearing Safari's website data was the only way out, which is not something a
player will work out.

So each deploy goes to a directory named for its commit, and nothing inside one
is ever written twice, which makes a cached copy of it safe by construction. The
site root holds only `.github/pages/index.html`, a loader whose content never
changes, so caching it is always right. It reads the current directory name from
`version.json` with `cache: 'no-store'`, the one fetch the HTTP cache can answer
neither from nor into, then `location.replace`s into the build, carrying the
query string and fragment along so `?2dog-timing` survives.

Two consequences worth knowing. Only the current build's directory is deployed,
so a link to an older one dies at the next push, and the game re-downloads on
each deploy (about 12 MB over the wire, since Pages gzips the wasm). And the
loader is the site's only stable URL, so a change to it cannot be rolled back by
another deploy for anyone still holding it cached: check it against a local
static server before pushing, including the path where `version.json` is missing.

The only thing it strips is `*.br`: Pages will not serve a brotli sibling, and
nothing asks for one by name. The `.gz` siblings stay, since `TWODOG_PCK_GZ` can
point the shell at them and it inflates them in the page. Pages turned out not
to need that anyway, it gzips `application/wasm` itself, so `godot.wasm` goes
over the wire at about 11 MB rather than 47 MB.

**Never delete anything `_framework/dotnet.boot.js` names.** Every asset in that
manifest is fetched at boot, and a single 404 rejects `mono_download_assets`, so
the engine never starts. The failure is quiet in a way that misleads: the
loading bar sits there forever, `status-notice` stays empty, and the error
surfaces only as an uncaught promise rejection in the console, because the
shell's `displayFailureNotice` never sees it. A bundle that parks on the loading
bar is a console job, not a headers job. The workflow walks the manifest and
fails the build if an asset is missing, so this cannot reach a deploy twice.

To drop a manifest asset, stop the publish emitting it instead. That is what
`<WasmEmitSymbolMap>false</WasmEmitSymbolMap>` in `SortPaint.web.csproj` does:
the wasm SDK defaults it on for a plain `browser-wasm` app, and it produces
`dotnet.native.js.symbols`, 19 MB of wasm-function-number to name mapping that
only symbolicates stack traces. Off at the source, it is neither built nor
listed, which took the bundle from 103 MB to 86 MB. Deleting it from the output
instead is what broke the first deploy.

itch.io is the other route, and sends the isolation headers when the uploaded
file is flagged as using SharedArrayBuffer. This build does not need that.

### Diagnosing a failed publish

- `NETSDK1147` or a missing `browser-wasm` runtime pack means `wasm-tools` is
  not installed. See the prerequisite above.
- A publish that cannot find the Godot editor failed at the content-export step,
  not the managed build. Check `godot --version` resolves to a 4.7.x mono build.
- Game scripts missing at runtime, with the engine otherwise starting, usually
  means trimming removed them. `SortPaint` is already listed as a
  `TrimmerRootAssembly`; any NuGet package the game reaches by reflection needs
  its own entry in `SortPaint.web/SortPaint.web.csproj`.
- Keep `2dog.engine` and the engine version in step. The package is pinned to
  the matching Godot version (`4.7.1.68` against Godot 4.7.1), so a Godot
  upgrade means bumping it too.
- `dotnet clean` triggers the `DeepClean` target that removes `AppBundle/`, which
  is the quickest way to rule out a stale bundle.
