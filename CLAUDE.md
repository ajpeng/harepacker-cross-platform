# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Repo Is

Cross-platform port of [Harepacker-resurrected](https://github.com/lastbattle/Harepacker-resurrected) — a MapleStory `.wz` file and map editor suite. The port targets `net10.0` (macOS/Linux/Windows) on the `cross-platform` branch.

## Build Commands

```bash
# Build individual projects
dotnet build MapleLib/MapleLib/MapleLib.csproj
dotnet build HaSharedLibrary/HaSharedLibrary.csproj
dotnet build HaRepacker/Harepacker-resurrected.csproj
# HaCreator not yet ported

# Build everything
dotnet build MapleHaSuite.sln

# Run HaRepacker
dotnet run --project HaRepacker/Harepacker-resurrected.csproj

# Tests (update csproj to net10.0 first)
dotnet test UnitTest_WzFile/UnitTest_WzFile.csproj
```

## Repository Structure

- `MapleLib/` — git submodule; core .wz file parsing library
  - `MapleLib/MapleLib/` — main library (SkiaSharp, NAudio.Core, NAudio.Midi)
  - `spine-runtimes-2.1.25/spine-csharp/` — Spine 2D animation (compiled into HaSharedLibrary for MonoGame types)
- `HaSharedLibrary/` — shared rendering library (MonoGame.DesktopGL, SkiaSharp)
- `HaRepacker/` — .wz file editor app (Avalonia UI, ported from WinForms/WPF)
- `HaCreator/` — map editor app (not yet ported, still net10.0-windows)

## Cross-Platform Architecture

### What Changed from Original
| Component | Before | After |
|-----------|--------|-------|
| Target | `net10.0-windows` | `net10.0` |
| Images | `System.Drawing.Bitmap` | `SkiaSharp.SKBitmap` |
| MonoGame | `MonoGame.Framework.WindowsDX` | `MonoGame.Framework.DesktopGL` |
| UI (HaRepacker) | WinForms + WPF | Avalonia 11.2.3 |
| Text editor | WPF AvalonEdit | `Avalonia.AvaloniaEdit` 11.2.0 |
| Audio | NAudio.WinMM + Mp3FileReader | NAudio.Core (stub playback) |
| APNG export | apng64/apng32.dll | throws PlatformNotSupportedException |

### Key Type Substitutions
- `System.Drawing.Bitmap` → `SkiaSharp.SKBitmap`
- `System.Drawing.Imaging.BitmapData` → `MapleLib.Helpers.PngUtility.WzBitmapData` (struct with Scan0/Stride/Width/Height)
- `Microsoft.Xna.Framework.Graphics.SurfaceFormat` → `MapleLib.WzLib.WzProperties.WzSurfaceFormat` (local enum)
- `System.Drawing.Color` / `Point` / `PointF` / `Rectangle` / `Size` — kept as-is (in `System.Drawing.Primitives`, cross-platform)
- WPF `IValueConverter` → Avalonia `Avalonia.Data.Converters.IValueConverter`
- WinForms `TreeNode` → `HaRepacker.Models.WzNode` (plain data model with `ObservableCollection<WzNode> Nodes`)

### spine-xna Files
`spine-xna/*.cs` files are excluded from `spine-csharp.csproj` and compiled directly in `HaSharedLibrary.csproj` using `<Compile Include="..\MapleLib\spine-runtimes-2.1.25\spine-csharp\spine-xna\*.cs" />`. This is because they need MonoGame types which are only in HaSharedLibrary.

### MapleLib Submodule
MapleLib is a git submodule at `MapleLib/`. After editing submodule files:
```bash
cd MapleLib && git add -A && git commit -m "..." && cd ..
git add MapleLib && git commit -m "Update MapleLib submodule"
```

## HaRepacker Avalonia Port Status

The port replaces all WinForms/WPF code with Avalonia. Files marked `<Compile Remove="...">` in the csproj are the original WinForms code excluded pending port.

**Ported:** App.axaml, MainWindow.axaml, MainPanel.axaml, all 4 SubPanels, Models/WzNode.cs, Warning.cs  
**Pending:** ~25 WinForms Form classes (MainForm 2583 lines, dialogs, input boxes), ContextMenuManager, UndoRedoManager, HotSwap, FHMapper, WPF Converter/ folder

## Avalonia Notes

- Use `Avalonia.AvaloniaEdit` NuGet package (assembly name = `AvaloniaEdit`, not `Avalonia.AvaloniaEdit`)
- `ColorPicker` requires Avalonia 11.3+; on 11.2.3 use RGBA sliders instead
- Named `ScaleTransform` inside `RenderTransform` in AXAML does NOT auto-generate code-behind fields — set them in the constructor via `border.RenderTransform = myScale`
- `WzObject.Parent` has `internal set` — subclasses outside MapleLib cannot satisfy this abstract member; avoid subclassing `WzObject` from HaRepacker

## Known Issues / Stubs

- `WzSoundResourceStreamer` is a no-op stub — audio playback needs a cross-platform backend (OpenAL via `OpenAL-CS` or similar)
- `SharpApng.WriteApng()` throws `PlatformNotSupportedException` — APNG export not yet implemented
- `ScreenDPIUtil.cs` and `MemoryScannerHelper.cs` (Windows-only) are excluded from HaSharedLibrary compilation
