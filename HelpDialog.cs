using Godot;

public class HelpSection
{
    public string Name { get; set; }
    public (string Key, string Desc)[] Items { get; set; }
}

// Popup listing a caller-supplied set of controls, styled with MintDarkTheme.
public class HelpDialog
{
    private readonly AcceptDialog _dialog;

    // parent is the Node the dialog gets added to as a child. title and sections
    // define the dialog's content, supplied by whichever caller wants a help popup.
    public HelpDialog(Node parent, string title, HelpSection[] sections, Theme theme = null)
    {
        _dialog = Build(title, sections);
        if (theme != null)
            _dialog.Theme = theme;
        parent.AddChild(_dialog);
    }

    public void Show()
    {
        _dialog.PopupCentered();
    }

    private static AcceptDialog Build(string title, HelpSection[] sections)
    {
        var dialog = new AcceptDialog
        {
            Title = title,
            OkButtonText = "Close",
            MinSize = new Vector2I(360, 0),

            // Native/embedded window chrome can't be reliably restyled across
            // platforms, so go borderless and draw our own header bar instead.
            Borderless = true
        };

        var body = new PanelContainer();
        body.AddThemeStyleboxOverride("panel", MintDarkTheme.MakeBodyPanelStyle());

        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 0);

        var header = new PanelContainer();
        header.AddThemeStyleboxOverride("panel", MintDarkTheme.MakeHeaderPanelStyle());
        var headerLabel = new Label
        {
            Text = title
        };
        headerLabel.AddThemeFontSizeOverride("font_size", 18);
        headerLabel.AddThemeColorOverride("font_color", MintDarkTheme.TextPrimary);
        header.AddChild(headerLabel);
        layout.AddChild(header);

        var contentMargin = new MarginContainer();
        contentMargin.AddThemeConstantOverride("margin_left", 16);
        contentMargin.AddThemeConstantOverride("margin_right", 16);
        contentMargin.AddThemeConstantOverride("margin_top", 12);
        contentMargin.AddThemeConstantOverride("margin_bottom", 16);

        var sectionsBox = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(320, 0)
        };
        sectionsBox.AddThemeConstantOverride("separation", 10);

        foreach (var section in sections)
        {
            AddSection(sectionsBox, section.Name, section.Items);
        }

        contentMargin.AddChild(sectionsBox);
        layout.AddChild(contentMargin);
        body.AddChild(layout);
        dialog.AddChild(body);

        var closeButton = dialog.GetOkButton();
        closeButton.ThemeTypeVariation = MintDarkTheme.AccentButtonVariation;

        return dialog;
    }

    private static void AddSection(VBoxContainer parent, string title, (string Key, string Desc)[] rows)
    {
        var header = new Label
        {
            Text = title
        };
        header.AddThemeFontSizeOverride("font_size", 16);
        parent.AddChild(header);

        var grid = new GridContainer
        {
            Columns = 2
        };
        grid.AddThemeConstantOverride("h_separation", 24);
        grid.AddThemeConstantOverride("v_separation", 4);
        parent.AddChild(grid);

        foreach (var row in rows)
        {
            var keyLabel = new Label
            {
                Text = row.Key
            };
            keyLabel.AddThemeColorOverride("font_color", MintDarkTheme.Accent);
            grid.AddChild(keyLabel);

            var descLabel = new Label
            {
                Text = row.Desc
            };
            grid.AddChild(descLabel);
        }
    }
}
