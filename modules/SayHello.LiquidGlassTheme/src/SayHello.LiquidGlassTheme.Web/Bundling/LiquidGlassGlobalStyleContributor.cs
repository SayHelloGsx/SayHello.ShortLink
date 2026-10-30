using Volo.Abp.AspNetCore.Mvc.UI.Bundling;

namespace SayHello.LiquidGlassTheme.Bundling;

public class LiquidGlassGlobalStyleContributor : BundleContributor
{
    public override void ConfigureBundle(BundleConfigurationContext context)
    {
        context.Files.Add("/Themes/LiquidGlass/liquid-glass.css");
    }
}
