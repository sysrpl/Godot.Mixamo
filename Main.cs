using Godot;
using System.Collections.Generic;

// Root scene script for the FBX dance-animation viewer. Builds the entire
// scene (camera rig, lighting, ground, UI) in code rather than a .tscn file,
// loads a starting character model, and lets the user orbit the camera,
// swap models via drag-and-drop or the Open dialog, and toggle a handful of
// rendering/debug options with hotkeys.
public partial class Main : Node3D
{
    // Scene nodes created and wired up in _Ready.
    private Node3D _cameraPivot;
    private Camera3D _camera;
    private AnimationPlayer _animPlayer;
    private DirectionalLight3D _light;
    private WorldEnvironment _worldEnvironment;
    private Node3D _character;

    // Orbit-camera state: yaw/pitch/distance are updated by mouse input and
    // converted into an actual transform by UpdateCameraTransform().
    private float _yaw = 0f;
    private float _pitch = -20f;
    private float _distance = 4f;
    private const float MinDistance = 1f;
    private const float MaxDistance = 15f;
    private const float RotateSpeed = 0.3f;
    private const float ZoomSpeed = 0.3f;

    // Toggle state backing the hotkeys handled in _UnhandledInput.
    private bool _dragging = false;
    private bool _whiteRenderMode = false;
    private bool _shadowsEnabled = true;
    private bool _aoEnabled = true;
    private bool _lightShadingEnabled = true; // true = normal lit render, false = SSAO debug view
    private bool _reflectionsEnabled = true;

    private List<MeshInstance3D> _meshInstances = new List<MeshInstance3D>();
    private StandardMaterial3D _whiteMaterial;
    private ShaderMaterial _groundMaterial;
    private Label _fpsLabel;
    private HelpDialog _helpDialog;
    private OpenFileDialog _openDialog;
    private FrametimeGraph _frametimeGraph;

    // Builds the whole scene programmatically and runs once when the node
    // enters the tree: camera rig, lighting/environment, ground, the
    // starting character, and all of the HUD/dialog UI.
    public override void _Ready()
    {
        // Orbit rig: a pivot Node3D sits at the target point (roughly head
        // height above the character) and the camera is a child offset
        // along +Z from it. Rotating the pivot orbits the camera around the
        // target; moving the camera along its local Z zooms in/out. See
        // UpdateCameraTransform() for how yaw/pitch/distance drive this.
        _cameraPivot = new Node3D();
        _cameraPivot.Position = new Vector3(0, 1f, 0);
        AddChild(_cameraPivot);

        _camera = new Camera3D();
        _cameraPivot.AddChild(_camera);
        UpdateCameraTransform();

        // World environment: a procedural sky plus screen-space ambient
        // occlusion (SSAO) and screen-space reflections (SSR). SSAO/SSR only
        // take effect when a WorldEnvironment with these enabled is present.
        _worldEnvironment = new WorldEnvironment();
        var env = new Environment();
        env.BackgroundMode = Environment.BGMode.Sky;
        var sky = new Sky();
        sky.SkyMaterial = new ProceduralSkyMaterial();
        env.Sky = sky;
        env.SsaoEnabled = true;
        env.SsaoRadius = 0.5f;
        env.SsaoIntensity = 4.0f;
        env.SsaoPower = 2.0f;
        env.SsaoDetail = 0.5f;
        env.SsaoHorizon = 0.06f;
        env.SsaoSharpness = 0.98f;
        env.SsaoLightAffect = 0.5f;
        env.SsrEnabled = true;
        env.SsrMaxSteps = 64;
        _worldEnvironment.Environment = env;
        AddChild(_worldEnvironment);

        // Single directional light (sun-like), angled down onto the scene,
        // casting shadows so the character grounds itself on the floor.
        _light = new DirectionalLight3D();
        _light.RotationDegrees = new Vector3(-45, -45, 0);
        _light.ShadowEnabled = true;
        AddChild(_light);

        // Ground plane to receive shadows, with a shader-drawn grid instead
        // of a texture: a flat 20x20 PlaneMesh whose material computes grid
        // lines procedurally from world-space XZ position (see the shader
        // source below for how the line pattern itself is drawn).
        var ground = new MeshInstance3D();
        var planeMesh = new PlaneMesh();
        planeMesh.Size = new Vector2(20, 20);
        ground.Mesh = planeMesh;

        var gridShader = new Shader();
        gridShader.Code = @"
shader_type spatial;
render_mode blend_mix, cull_back, diffuse_burley, specular_schlick_ggx;

uniform vec3 base_color : source_color = vec3(0.08, 0.08, 0.09);
uniform vec3 line_color : source_color = vec3(1.0, 1.0, 1.0);
uniform float cell_size = 1.0;   // world units between grid lines
uniform float line_thickness = 1.5; // in pixels, roughly
uniform float roughness_value = 1.0;
uniform float metallic_value = 0.0;

varying vec3 world_pos;

void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    // Distance (in grid cells) from this fragment to the nearest grid line
    // on each axis: fract(coord - 0.5) - 0.5 wraps coord into [-0.5, 0.5)
    // per cell, and abs() folds that into [0, 0.5], i.e. 0 exactly on a line.
    vec2 coord = world_pos.xz / cell_size;
    vec2 grid_uv = abs(fract(coord - 0.5) - 0.5);
    // fwidth() gives the change in coord between neighboring pixels, so
    // multiplying by line_thickness converts a pixel-width line into the
    // matching width in grid_uv space, letting the line keep a constant
    // screen-space thickness regardless of distance/zoom.
    vec2 deriv = fwidth(coord) * line_thickness;
    vec2 line = smoothstep(vec2(0.0), deriv, grid_uv);
    // line.x/line.y are ~0 right on a line and ~1 away from it; take
    // whichever axis is closer to a line (min) and invert so grid_mask is
    // ~1 on a line, ~0 elsewhere.
    float grid_mask = 1.0 - min(line.x, line.y);

    ALBEDO = mix(base_color, line_color, grid_mask);
    ROUGHNESS = roughness_value;
    METALLIC = metallic_value;
}
";
        _groundMaterial = new ShaderMaterial();
        _groundMaterial.Shader = gridShader;
        _groundMaterial.SetShaderParameter("cell_size", 1.0f);
        _groundMaterial.SetShaderParameter("line_thickness", 1.5f);
        ApplyGroundReflectionParams();

        ground.MaterialOverride = _groundMaterial;
        ground.Position = new Vector3(0, 0, 0);
        AddChild(ground);

        // White material used for toggle #1 (ToggleWhiteRenderMode): swapped
        // in as a MaterialOverride on every mesh to inspect geometry/shading
        // without the character's own textures.
        _whiteMaterial = new StandardMaterial3D();
        _whiteMaterial.AlbedoColor = Colors.White;

        // Load the initial FBX the same way a dropped file is loaded (straight
        // from disk via FBXDocument/FBXState) rather than through Godot's
        // imported-resource pipeline, so it doesn't depend on a checked-in
        // dance.fbx.import / .godot import cache being present.
        LoadCharacterFromExternalFile(ProjectSettings.GlobalizePath("res://dance.fbx"));

        // FPS counter, top-left corner: a screen-space CanvasLayer keeps the
        // HUD fixed regardless of camera movement; the label's text is
        // refreshed every frame in _Process.
        var fpsLayer = new CanvasLayer();
        AddChild(fpsLayer);
        _fpsLabel = new Label();
        _fpsLabel.Position = new Vector2(10, 10);
        _fpsLabel.AddThemeColorOverride("font_color", Colors.White);
        _fpsLabel.AddThemeFontSizeOverride("font_size", 20);
        fpsLayer.AddChild(_fpsLabel);

        // Scrolling frametime graph, bottom of the window (see
        // FrametimeGraph.cs for how it samples/draws itself).
        _frametimeGraph = new FrametimeGraph();
        fpsLayer.AddChild(_frametimeGraph);

        // Open + Help buttons, top-right corner. Both are anchored to the
        // right edge (AnchorLeft/Right = 1) and positioned with Offset*
        // rather than Position so they stay pinned to the corner if the
        // window is resized.
        var uiTheme = MintDarkTheme.Build();

        var openButton = new Button();
        openButton.Text = "Open";
        openButton.Theme = uiTheme;
        openButton.AnchorLeft = 1f;
        openButton.AnchorRight = 1f;
        openButton.AnchorTop = 0f;
        openButton.AnchorBottom = 0f;
        openButton.OffsetLeft = -80f;
        openButton.OffsetRight = -10f;
        openButton.OffsetTop = 10f;
        openButton.OffsetBottom = 40f;
        fpsLayer.AddChild(openButton);

        // OpenFileDialog wraps either the OS-native file picker or Godot's
        // own FileDialog (see OpenFileDialog.cs); FileSelected fires with
        // the chosen path, which we feed straight into the same loader used
        // for drag-and-drop.
        _openDialog = new OpenFileDialog(this, OpenFileDialog.FileTypeFilter.FbxFiles, "Open FBX File", uiTheme);
        _openDialog.FileSelected += LoadCharacterFromExternalFile;
        openButton.Pressed += _openDialog.Show;

        var helpButton = new Button();
        helpButton.Text = "Help";
        helpButton.Theme = uiTheme;
        helpButton.AnchorLeft = 1f;
        helpButton.AnchorRight = 1f;
        helpButton.AnchorTop = 0f;
        helpButton.AnchorBottom = 0f;
        helpButton.OffsetLeft = -80f;
        helpButton.OffsetRight = -10f;
        helpButton.OffsetTop = 50f;
        helpButton.OffsetBottom = 80f;
        fpsLayer.AddChild(helpButton);

        _helpDialog = new HelpDialog(this, uiTheme);
        helpButton.Pressed += _helpDialog.Show;

        // Allow dropping .fbx files onto the game window to swap model + animation
        GetWindow().FilesDropped += OnFilesDropped;

        GD.Print("Hotkeys: [1] toggle white render, [2] toggle shadows, [3] toggle AO, [4] toggle lit / SSAO debug view, [6] toggle ground reflections, [7] toggle frametime graph, [F1] toggle fullscreen, [Esc] quit");
        GD.Print("Drag and drop an .fbx file onto the window to replace the model.");
    }

    // Runs every rendered frame; the only per-frame work here is refreshing
    // the FPS counter text.
    public override void _Process(double delta)
    {
        _fpsLabel.Text = $"FPS: {Engine.GetFramesPerSecond()}";
    }

    // Handles mouse-look/zoom for the orbit camera and all keyboard
    // hotkeys. Using _UnhandledInput (rather than _Input) means UI controls
    // like the Open/Help buttons get first claim on input events, so
    // clicking them doesn't also start a camera drag.
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                // Track left-button press/release so InputEventMouseMotion
                // below knows whether the user is currently dragging.
                _dragging = mouseButton.Pressed;
            }
            else if (mouseButton.ButtonIndex == MouseButton.WheelUp)
            {
                // Scroll wheel zooms by shrinking/growing the camera's
                // distance from the pivot, clamped to a sane range.
                _distance = Mathf.Clamp(_distance - ZoomSpeed, MinDistance, MaxDistance);
                UpdateCameraTransform();
            }
            else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
            {
                _distance = Mathf.Clamp(_distance + ZoomSpeed, MinDistance, MaxDistance);
                UpdateCameraTransform();
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion && _dragging)
        {
            // While dragging, mouse movement rotates the orbit pivot:
            // horizontal movement changes yaw, vertical changes pitch.
            // Pitch is clamped so the camera can't flip over the top/bottom.
            _yaw -= mouseMotion.Relative.X * RotateSpeed;
            _pitch -= mouseMotion.Relative.Y * RotateSpeed;
            _pitch = Mathf.Clamp(_pitch, -80f, 80f);
            UpdateCameraTransform();
        }
        else if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            // !keyEvent.Echo ignores the repeated events an OS sends while a
            // key is held down, so each toggle only fires once per press.
            switch (keyEvent.Keycode)
            {
                case Key.Key1:
                    ToggleWhiteRenderMode();
                    break;
                case Key.Key2:
                    ToggleShadows();
                    break;
                case Key.Key3:
                    ToggleAmbientOcclusion();
                    break;
                case Key.Key4:
                    ToggleLightShading();
                    break;
                case Key.Key6:
                    ToggleReflections();
                    break;
                case Key.Key7:
                    ToggleFrametimeGraph();
                    break;
                case Key.F1:
                    ToggleFullscreen();
                    break;
                case Key.Escape:
                    GetTree().Quit();
                    break;
            }
        }
    }

    // Called when the OS reports files dropped onto the game window; picks
    // out the first .fbx in the drop and loads it, ignoring anything else.
    private void OnFilesDropped(string[] files)
    {
        foreach (var file in files)
        {
            if (file.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            {
                GD.Print($"Loading dropped FBX: {file}");
                LoadCharacterFromExternalFile(file);
                return; // only handle the first .fbx if several files were dropped at once
            }
        }
        GD.Print("No .fbx file found among dropped files.");
    }

    // Loads an .fbx file from an absolute filesystem path (as opposed to a
    // res:// project asset) and swaps it in as the active character.
    private void LoadCharacterFromExternalFile(string path)
    {
        // Dropped files live outside the project, so they can't go through the
        // normal res:// import pipeline. FBXDocument/FBXState (Godot 4.3+) load
        // FBX scenes directly from an absolute filesystem path at runtime.
        //
        // Note: some Godot .NET/Mono builds don't ship C# wrapper types for
        // FBXDocument/FBXState even though the native class is compiled in
        // (proven by the fact the editor itself can import dance.fbx). So we
        // go through ClassDB by name instead of `new FBXDocument()` - this only
        // needs the native class to be registered, not a generated C# type.
        if (!ClassDB.ClassExists("FBXDocument"))
        {
            GD.PrintErr("FBXDocument is not available in this Godot build - cannot load FBX files at runtime.");
            return;
        }

        // ClassDB.Instantiate returns a Variant wrapping the native object;
        // AsGodotObject() unwraps it into something we can call methods on.
        var docVariant = ClassDB.Instantiate("FBXDocument");
        var stateVariant = ClassDB.Instantiate("FBXState");
        var doc = docVariant.AsGodotObject();
        var state = stateVariant.AsGodotObject();

        if (doc == null || state == null)
        {
            GD.PrintErr("Failed to instantiate FBXDocument/FBXState.");
            return;
        }

        // doc.Call(...) invokes the native methods dynamically by name since
        // there's no generated C# API for them; append_from_file parses the
        // FBX into `state`, then generate_scene builds an actual Node tree
        // from that parsed state.
        var appendResult = doc.Call("append_from_file", path, state);
        var err = (Error)appendResult.As<int>();
        if (err != Error.Ok)
        {
            GD.PrintErr($"Failed to load dropped FBX '{path}': {err}");
            return;
        }

        var sceneVariant = doc.Call("generate_scene", state);
        var newCharacter = sceneVariant.AsGodotObject() as Node3D;
        if (newCharacter == null)
        {
            GD.PrintErr($"FBX '{path}' loaded but did not produce a usable 3D scene.");
            return;
        }

        ReplaceCharacter(newCharacter);
    }

    // Swaps in a new character model: frees the old one, adds the new one
    // to the scene, rebuilds cached mesh/animation references, and starts
    // an animation playing.
    private void ReplaceCharacter(Node3D newCharacter)
    {
        // Defer-free the old model rather than freeing immediately, since it's
        // safe to queue removal while still swapping references this frame.
        if (_character != null)
        {
            _character.QueueFree();
        }

        _character = newCharacter;
        AddChild(_character);

        // Rebuild mesh list and re-apply current toggle states (white render, shadows)
        _meshInstances.Clear();
        CollectMeshInstances(_character, _meshInstances);
        foreach (var mesh in _meshInstances)
        {
            mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            mesh.MaterialOverride = _whiteRenderMode ? _whiteMaterial : null;
        }

        _animPlayer = FindAnimationPlayer(_character);
        if (_animPlayer != null)
        {
            var anims = _animPlayer.GetAnimationList();
            GD.Print($"Found {anims.Length} animation(s): {string.Join(", ", anims)}");

            // Prefer whichever animation has "mixamo" in its name, since
            // Mixamo-exported FBX rigs (this project's intended source)
            // typically only contain one real animation clip named that
            // way alongside auxiliary tracks; fall back to the last
            // animation in the list if none match.
            string targetAnim = null;
            foreach (var name in anims)
            {
                if (name.Contains("mixamo", System.StringComparison.OrdinalIgnoreCase))
                {
                    targetAnim = name;
                    break;
                }
            }
            if (targetAnim == null && anims.Length > 0)
                targetAnim = anims[anims.Length - 1];

            if (targetAnim != null)
            {
                _animPlayer.Play(targetAnim);
                var anim = _animPlayer.GetAnimation(targetAnim);
                anim.LoopMode = Animation.LoopModeEnum.Linear;
                GD.Print($"Playing animation: {targetAnim}");
            }
            else
            {
                GD.PrintErr("AnimationPlayer found but has 0 animations.");
            }
        }
        else
        {
            // No AnimationPlayer means the model won't animate; dump the
            // node tree to help diagnose why (e.g. wrong export settings).
            GD.PrintErr("No AnimationPlayer found in the loaded scene:");
            PrintTree(_character, 0);
        }
    }

    // Hotkey [1]: swaps every mesh's material for a flat white one (or back
    // to its original material), useful for inspecting geometry/shading.
    private void ToggleWhiteRenderMode()
    {
        _whiteRenderMode = !_whiteRenderMode;
        foreach (var mesh in _meshInstances)
        {
            mesh.MaterialOverride = _whiteRenderMode ? _whiteMaterial : null;
        }
        GD.Print($"White render mode: {_whiteRenderMode}");
    }

    // Hotkey [2]: toggles whether the directional light casts shadows.
    private void ToggleShadows()
    {
        _shadowsEnabled = !_shadowsEnabled;
        _light.ShadowEnabled = _shadowsEnabled;
        GD.Print($"Shadows enabled: {_shadowsEnabled}");
    }

    // Hotkey [3]: toggles screen-space ambient occlusion on the environment.
    private void ToggleAmbientOcclusion()
    {
        _aoEnabled = !_aoEnabled;
        _worldEnvironment.Environment.SsaoEnabled = _aoEnabled;
        GD.Print($"Ambient occlusion enabled: {_aoEnabled}");
    }

    // Hotkey [4]: switches between normal lit rendering and an SSAO-only
    // debug view (see comment below for why this uses viewport debug draw
    // instead of a material flag).
    private void ToggleLightShading()
    {
        // Godot's lighting pipeline computes SSAO as part of the lit shader path,
        // so per-material Unshaded mode drops AO along with N.L shading - there's
        // no way to keep one without the other at the material level.
        // Instead, use the viewport's SSAO debug draw mode to view the AO buffer
        // in isolation, with no directional light shading applied at all.
        _lightShadingEnabled = !_lightShadingEnabled;
        var viewport = GetViewport();
        viewport.DebugDraw = _lightShadingEnabled
            ? Viewport.DebugDrawEnum.Disabled
            : Viewport.DebugDrawEnum.Ssao;
        GD.Print($"Dot-product light shading enabled: {_lightShadingEnabled}");
    }

    // Hotkey [F1]: toggles borderless fullscreen.
    private void ToggleFullscreen()
    {
        var window = GetWindow();
        bool isFullscreen = window.Mode == Window.ModeEnum.Fullscreen
            || window.Mode == Window.ModeEnum.ExclusiveFullscreen;

        // Window.ModeEnum.Fullscreen is borderless (no window chrome/decorations),
        // covering the full screen - exactly what "fullscreen without chrome" means here.
        window.Mode = isFullscreen ? Window.ModeEnum.Windowed : Window.ModeEnum.Fullscreen;
        GD.Print($"Fullscreen: {!isFullscreen}");
    }

    // Hotkey [6]: toggles screen-space reflections and updates the ground
    // shader so the reflection is actually visible (or not) to match.
    private void ToggleReflections()
    {
        _reflectionsEnabled = !_reflectionsEnabled;
        _worldEnvironment.Environment.SsrEnabled = _reflectionsEnabled;
        ApplyGroundReflectionParams();
        GD.Print($"Ground reflections enabled: {_reflectionsEnabled}");
    }

    // Hotkey [7]: shows/hides the scrolling frametime graph overlay.
    private void ToggleFrametimeGraph()
    {
        _frametimeGraph.Visible = !_frametimeGraph.Visible;
        GD.Print($"Frametime graph visible: {_frametimeGraph.Visible}");
    }

    // Pushes the ground shader's roughness/metallic uniforms to match the
    // current reflections toggle; called both at startup and from
    // ToggleReflections.
    private void ApplyGroundReflectionParams()
    {
        // Low roughness + a touch of metallic makes the ground's screen-space
        // reflections actually visible; when off, revert to a plain matte surface.
        if (_reflectionsEnabled)
        {
            _groundMaterial.SetShaderParameter("roughness_value", 0.05f);
            _groundMaterial.SetShaderParameter("metallic_value", 0.3f);
        }
        else
        {
            _groundMaterial.SetShaderParameter("roughness_value", 1.0f);
            _groundMaterial.SetShaderParameter("metallic_value", 0.0f);
        }
    }

    // Applies the current yaw/pitch/distance orbit-camera state to the
    // actual scene transform: rotates the pivot, offsets the camera along
    // its local +Z by `distance`, then re-aims it at the pivot so it always
    // faces the target regardless of rotation.
    private void UpdateCameraTransform()
    {
        _cameraPivot.Rotation = new Vector3(
            Mathf.DegToRad(_pitch),
            Mathf.DegToRad(_yaw),
            0f
        );
        _camera.Position = new Vector3(0, 0, _distance);
        _camera.LookAt(_cameraPivot.GlobalPosition, Vector3.Up);
    }

    // Recursively searches a node's subtree (depth-first) for the first
    // AnimationPlayer, returning null if none exists.
    private AnimationPlayer FindAnimationPlayer(Node node)
    {
        if (node is AnimationPlayer ap) return ap;
        foreach (var child in node.GetChildren())
        {
            var found = FindAnimationPlayer(child);
            if (found != null) return found;
        }
        return null;
    }

    // Recursively walks a node's subtree, appending every MeshInstance3D
    // found into `list`. Used to rebuild _meshInstances whenever the
    // character model is replaced.
    private void CollectMeshInstances(Node node, List<MeshInstance3D> list)
    {
        if (node is MeshInstance3D mesh) list.Add(mesh);
        foreach (var child in node.GetChildren())
        {
            CollectMeshInstances(child, list);
        }
    }

    // Debug helper: prints a node and its descendants as an indented tree,
    // used when a loaded FBX has no AnimationPlayer so its structure can be
    // inspected in the output log.
    private void PrintTree(Node node, int depth)
    {
        GD.Print(new string(' ', depth * 2) + node.Name + " (" + node.GetType().Name + ")");
        foreach (var child in node.GetChildren())
            PrintTree(child, depth + 1);
    }
}
