namespace SayHello.LiquidGlassTheme;

public enum LiquidGlassColorMode
{
    System,
    Light,
    Dark
}

public class LiquidGlassThemeOptions
{
    public LiquidGlassColorMode DefaultColorMode { get; set; } = LiquidGlassColorMode.System;

    public bool EnableWebGl { get; set; } = true;

    public int MaxGlassSurfaces { get; set; } = 24;

    public bool ShowFooter { get; set; } = true;

    public Dictionary<string, string> Layouts { get; set; } = new(StringComparer.Ordinal);
}
