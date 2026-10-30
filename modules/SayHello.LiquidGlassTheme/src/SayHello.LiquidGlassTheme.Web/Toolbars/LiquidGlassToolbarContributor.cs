using SayHello.LiquidGlassTheme.Components.Toolbar.Appearance;
using SayHello.LiquidGlassTheme.Components.Toolbar.Language;
using SayHello.LiquidGlassTheme.Components.Toolbar.User;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared.Toolbars;

namespace SayHello.LiquidGlassTheme.Toolbars;

public class LiquidGlassToolbarContributor : IToolbarContributor
{
    public virtual Task ConfigureToolbarAsync(IToolbarConfigurationContext context)
    {
        if (context.Theme is LiquidGlassTheme && context.Toolbar.Name == StandardToolbars.Main)
        {
            context.Toolbar.Items.Add(new ToolbarItem(typeof(LiquidGlassAppearanceViewComponent), 0));
            context.Toolbar.Items.Add(new ToolbarItem(typeof(LiquidGlassLanguageViewComponent), 100));
            context.Toolbar.Items.Add(new ToolbarItem(typeof(LiquidGlassUserViewComponent), 1000));
        }

        return Task.CompletedTask;
    }
}
