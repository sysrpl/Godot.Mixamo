using Godot;

// Popup listing the app's mouse/keyboard controls, styled with MintDarkTheme.
public class HelpDialog
{
    private readonly AcceptDialog _dialog;

    // parent is the Node the dialog gets added to as a child.
    public HelpDialog(Node parent, Theme theme = null)
    {
        _dialog = Build();
        if (theme != null)
            _dialog.Theme = theme;
        parent.AddChild(_dialog);
    }

    public void Show()
    {
        _dialog.PopupCentered();
    }

    private static AcceptDialog Build()
    {
        var dialog = new AcceptDialog();
        dialog.Title = "Hotkeys & Controls";
        dialog.OkButtonText = "Close";
        dialog.MinSize = new Vector2I(360, 0);

        // Native/embedded window chrome can't be reliably restyled across
        // platforms, so go borderless and draw our own header bar instead.
        dialog.Borderless = true;

        var body = new PanelContainer();
        body.AddThemeStyleboxOverride("panel", MintDarkTheme.MakeBodyPanelStyle());

        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 0);

        var header = new PanelContainer();
        header.AddThemeStyleboxOverride("panel", MintDarkTheme.MakeHeaderPanelStyle());
        var headerLabel = new Label();
        headerLabel.Text = "Hotkeys & Controls";
        headerLabel.AddThemeFontSizeOverride("font_size", 18);
        headerLabel.AddThemeColorOverride("font_color", MintDarkTheme.TextPrimary);
        header.AddChild(headerLabel);
        layout.AddChild(header);

        var contentMargin = new MarginContainer();
        contentMargin.AddThemeConstantOverride("margin_left", 16);
        contentMargin.AddThemeConstantOverride("margin_right", 16);
        contentMargin.AddThemeConstantOverride("margin_top", 12);
        contentMargin.AddThemeConstantOverride("margin_bottom", 16);

        var sections = new VBoxContainer();
        sections.CustomMinimumSize = new Vector2(320, 0);
        sections.AddThemeConstantOverride("separation", 10);

        AddSection(sections, "Mouse", new (string Key, string Desc)[]
        {
            ("Left drag", "Orbit camera"),
            ("Wheel", "Zoom in / out"),
            ("Drag & drop .fbx", "Load a new model"),
            ("Open button", "Browse for a new model"),
        });

        AddSection(sections, "Keyboard", new (string Key, string Desc)[]
        {
            ("1", "Toggle white render mode"),
            ("2", "Toggle shadows"),
            ("3", "Toggle ambient occlusion"),
            ("4", "Toggle lit / SSAO debug view"),
            ("6", "Toggle ground reflections"),
            ("7", "Toggle frametime graph"),
            ("F1", "Toggle fullscreen"),
            ("Esc", "Quit"),
        });

        contentMargin.AddChild(sections);
        layout.AddChild(contentMargin);
        body.AddChild(layout);
        dialog.AddChild(body);

        var closeButton = dialog.GetOkButton();
        closeButton.ThemeTypeVariation = MintDarkTheme.AccentButtonVariation;

        return dialog;
    }

    private static void AddSection(VBoxContainer parent, string title, (string Key, string Desc)[] rows)
    {
        var header = new Label();
        header.Text = title;
        header.AddThemeFontSizeOverride("font_size", 16);
        parent.AddChild(header);

        var grid = new GridContainer();
        grid.Columns = 2;
        grid.AddThemeConstantOverride("h_separation", 24);
        grid.AddThemeConstantOverride("v_separation", 4);
        parent.AddChild(grid);

        foreach (var row in rows)
        {
            var keyLabel = new Label();
            keyLabel.Text = row.Key;
            keyLabel.AddThemeColorOverride("font_color", MintDarkTheme.Accent);
            grid.AddChild(keyLabel);

            var descLabel = new Label();
            descLabel.Text = row.Desc;
            grid.AddChild(descLabel);
        }
    }
}
