# Godot Mixamo Model Viewer

A lightweight Godot 4.7 + C# viewer for previewing Mixamo-style animated FBX
characters. The entire scene — camera rig, lighting, ground, and UI — is
built in code (see [Main.cs](Main.cs)), so there's no `.tscn` scene graph to
maintain; drop in an FBX and it plays.

## Features

- Orbit camera with mouse drag + scroll-wheel zoom
- Loads FBX models straight from disk at runtime (via `FBXDocument`/`FBXState`),
  so it works without going through Godot's resource-import pipeline
- Drag-and-drop an `.fbx` file onto the window, or use the **Open** button, to
  swap the active model and animation
- Auto-plays the first animation clip with "mixamo" in its name (falling back
  to the last clip in the file), looped
- Procedural grid ground plane with screen-space reflections
- FPS counter and a scrolling frametime graph
- Debug toggles for shadows, ambient occlusion, screen-space reflections, and
  a flat white/unshaded render mode

## Controls

| Input | Action |
|---|---|
| Left drag | Orbit camera |
| Wheel | Zoom in / out |
| Drag & drop `.fbx` | Load a new model |
| Open button | Browse for a new model |
| `1` | Toggle white render mode |
| `2` | Toggle shadows |
| `3` | Toggle ambient occlusion |
| `4` | Toggle lit / SSAO debug view |
| `6` | Toggle ground reflections |
| `7` | Toggle frametime graph |
| `F1` | Toggle fullscreen |
| `Esc` | Quit |

## Requirements

- [Godot 4.7](https://godotengine.org/download) (.NET/Mono build)
- .NET 8.0 SDK

## Running

Open the project folder in the Godot editor and press Play, or from the
command line:

```
godot --path . 
```

The project loads `dance.fbx` on startup; drag any other Mixamo FBX export
onto the window afterward to preview it instead.

## Project layout

- `Main.cs` — builds the scene, camera, lighting, and UI; handles model
  loading and all hotkeys
- `OpenFileDialog.cs` — native/Godot file picker for choosing an FBX
- `HelpDialog.cs` — in-app controls reference
- `MintDarkTheme.cs` — shared UI theme
- `FrametimeGraph.cs` — scrolling frametime overlay
- `dance.fbx`, `mma-kick.fbx` — sample Mixamo animations
