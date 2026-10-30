using Microsoft.Extensions.Options;
using Volo.Abp.AspNetCore.Mvc.UI.Theming;
using Volo.Abp.DependencyInjection;

namespace SayHello.LiquidGlassTheme;

[ThemeName(Name)]
public class LiquidGlassTheme : ITheme, ITransientDependency
{
    public const string Name = "SayHello.LiquidGlass";
    public const string ApplicationLayout = "~/Themes/LiquidGlass/Layouts/Application/Default.cshtml";
    public const string AccountLayout = "~/Themes/LiquidGlass/Layouts/Account/Default.cshtml";
    public const string EmptyLayout = "~/Themes/LiquidGlass/Layouts/Empty/Default.cshtml";

    protected IOptions<LiquidGlassThemeOptions> Options { get; }

    public LiquidGlassTheme(IOptions<LiquidGlassThemeOptions> options)
    {
        Options = options;
    }

    public virtual string GetLayout(string name, bool fallbackToDefault = true)
    {
        if (Options.Value.Layouts.TryGetValue(name, out var layout) && !string.IsNullOrWhiteSpace(layout))
        {
            return layout;
        }

        return name switch
        {
            StandardLayouts.Application or StandardLayouts.Public => ApplicationLayout,
            StandardLayouts.Account => AccountLayout,
            StandardLayouts.Empty => EmptyLayout,
            _ => fallbackToDefault ? ApplicationLayout : null!
        };
    }
}
