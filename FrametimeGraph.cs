using Godot;
using System.Collections.Generic;

// Scrolling frametime graph: newest sample enters on the right, oldest falls
// off the left. Anchored full-width to the bottom of the viewport.
public partial class FrametimeGraph : Control
{
    private const float GraphHeight = 100f;
    private const float LineThickness = 2f;
    private const float MaxMs = 30f; // top of the vertical scale
    private const float LegendStepMs = 5f;
    private const float LegendWidth = 40f;
    private const int LegendFontSize = 10;

    private static readonly Color LineColor = Colors.Green;
    private static readonly Color BackgroundColor = new Color(0f, 0f, 0f, 0.5f);
    private static readonly Color GridColor = new Color(1f, 1f, 1f, 0.25f);
    private static readonly Color LegendTextColor = Colors.White;

    private readonly List<float> _samples = new List<float>();

    public override void _Ready()
    {
        AnchorLeft = 0f;
        AnchorRight = 1f;
        AnchorTop = 1f;
        AnchorBottom = 1f;
        OffsetLeft = 0f;
        OffsetRight = 0f;
        OffsetTop = -GraphHeight;
        OffsetBottom = 0f;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta)
    {
        _samples.Add((float)(delta * 1000.0));

        int capacity = Mathf.Max(1, (int)(Size.X - LegendWidth));
        int excess = _samples.Count - capacity;
        if (excess > 0)
            _samples.RemoveRange(0, excess);

        QueueRedraw();
    }

    public override void _Draw()
    {
        var size = Size;
        var graphRect = new Rect2(LegendWidth, 0f, size.X - LegendWidth, size.Y);
        DrawRect(graphRect, BackgroundColor);

        var font = ThemeDB.FallbackFont;

        for (float ms = 0f; ms <= MaxMs; ms += LegendStepMs)
        {
            float y = size.Y - (ms / MaxMs) * size.Y;
            DrawLine(new Vector2(LegendWidth, y), new Vector2(size.X, y), GridColor, 1f);
            if ((int)ms % 10 == 0)
            {
                DrawString(font, new Vector2(2f, Mathf.Clamp(y + LegendFontSize * 0.3f, LegendFontSize, size.Y)),
                    $"{(int)ms}ms", HorizontalAlignment.Left, LegendWidth - 4f, LegendFontSize, LegendTextColor);
            }
        }

        if (_samples.Count < 2)
            return;

        var points = new Vector2[_samples.Count];
        for (int i = 0; i < _samples.Count; i++)
        {
            float ms = Mathf.Min(_samples[i], MaxMs);
            float x = graphRect.Position.X + i;
            float y = size.Y - (ms / MaxMs) * size.Y;
            points[i] = new Vector2(x, y);
        }

        DrawPolyline(points, LineColor, LineThickness, true);
    }
}
