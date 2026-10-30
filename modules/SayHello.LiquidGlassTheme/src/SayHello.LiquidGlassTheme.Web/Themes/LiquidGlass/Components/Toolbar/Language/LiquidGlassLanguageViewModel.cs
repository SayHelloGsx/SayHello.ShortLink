using Volo.Abp.Localization;

namespace SayHello.LiquidGlassTheme.Components.Toolbar.Language;

public class LiquidGlassLanguageViewModel
{
    public required IReadOnlyList<LanguageInfo> Languages { get; init; }

    public required LanguageInfo CurrentLanguage { get; init; }
}
