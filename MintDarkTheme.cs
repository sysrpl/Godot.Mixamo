using Godot;

// Approximates Linux Mint's "Mint-Y-Dark" desktop theme: flat dark gray
// panels/buttons with the Mint signature green used as the accent color
// for hover, pressed and focus states.
//
// Native/embedded Window chrome (title bar) is drawn by the OS window
// manager or, in embedded mode, by an engine-internal stylebox Godot does
// not reliably expose across platforms - so dialogs built with this theme
// go borderless and draw their own header bar out of themed Controls
// instead of depending on Window theming.
public static class MintDarkTheme
{
    public static readonly Color Background = new Color("#333333");
    public static readonly Color HeaderBackground = new Color("#2a2a2a");
    public static readonly Color ButtonNormal = new Color("#3d3d3d");
    public static readonly Color ButtonHover = new Color("#474747");
    public static readonly Color ButtonPressed = new Color("#2b2b2b");
    public static readonly Color Border = new Color("#4d4d4d");
    public static readonly Color Accent = new Color("#6cc066");
    public static readonly Color AccentHover = new Color("#7fd179");
    public static readonly Color AccentDim = new Color("#57a05c");
    public static readonly Color TextPrimary = new Color("#e0e0e0");
    public static readonly Color TextSecondary = new Color("#b0b0b0");
    public static readonly Color TextOnAccent = new Color("#1c2a1c");

    public const string AccentButtonVariation = "AccentButton";

    public static Theme Build()
    {
        var theme = new Theme();

        theme.SetColor("font_color", "Label", TextPrimary);

        theme.SetColor("font_color", "Button", TextPrimary);
        theme.SetColor("font_hover_color", "Button", TextPrimary);
        theme.SetColor("font_pressed_color", "Button", TextPrimary);
        theme.SetColor("font_focus_color", "Button", TextPrimary);
        theme.SetStylebox("normal", "Button", MakeFlatStyle(ButtonNormal, Border));
        theme.SetStylebox("hover", "Button", MakeFlatStyle(ButtonHover, AccentDim));
        theme.SetStylebox("pressed", "Button", MakeFlatStyle(ButtonPressed, Accent));
        theme.SetStylebox("focus", "Button", MakeOutlineStyle(Accent));

        // Solid mint-green "suggested action" style button, matching the
        // accent color Mint-Y uses for primary/default dialog buttons.
        theme.SetTypeVariation(AccentButtonVariation, "Button");
        theme.SetColor("font_color", AccentButtonVariation, TextOnAccent);
        theme.SetColor("font_hover_color", AccentButtonVariation, TextOnAccent);
        theme.SetColor("font_pressed_color", AccentButtonVariation, TextOnAccent);
        theme.SetColor("font_focus_color", AccentButtonVariation, TextOnAccent);
        theme.SetStylebox("normal", AccentButtonVariation, MakeFlatStyle(Accent, Accent));
        theme.SetStylebox("hover", AccentButtonVariation, MakeFlatStyle(AccentHover, AccentHover));
        theme.SetStylebox("pressed", AccentButtonVariation, MakeFlatStyle(AccentDim, AccentDim));
        theme.SetStylebox("focus", AccentButtonVariation, MakeOutlineStyle(TextPrimary));

        return theme;
    }

    public static StyleBoxFlat MakeBodyPanelStyle()
    {
        var style = new StyleBoxFlat
        {
            BgColor = Background,
            BorderColor = Border
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(6);
        style.SetContentMarginAll(0f);
        return style;
    }

    public static StyleBoxFlat MakeHeaderPanelStyle()
    {
        var style = new StyleBoxFlat
        {
            BgColor = HeaderBackground,
            BorderColor = Accent,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            ContentMarginLeft = 16f,
            ContentMarginRight = 16f,
            ContentMarginTop = 10f,
            ContentMarginBottom = 10f
        };
        return style;
    }

    private static StyleBoxFlat MakeFlatStyle(Color bg, Color border)
    {
        var style = new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = border
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(3);
        style.ContentMarginLeft = 14f;
        style.ContentMarginRight = 14f;
        style.ContentMarginTop = 6f;
        style.ContentMarginBottom = 6f;
        return style;
    }

    private static StyleBoxFlat MakeOutlineStyle(Color borderColor)
    {
        // Focus is drawn as an outline overlay on top of the normal/hover/pressed
        // style, so keep it transparent and only draw the accent border.
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0f, 0f, 0f, 0f),
            BorderColor = borderColor
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(3);
        style.ContentMarginLeft = 14f;
        style.ContentMarginRight = 14f;
        style.ContentMarginTop = 6f;
        style.ContentMarginBottom = 6f;
        return style;
    }
}
