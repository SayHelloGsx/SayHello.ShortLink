using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace SayHello.LiquidGlassTheme.Components.Toolbar.Appearance;

public class LiquidGlassAppearanceViewComponent : AbpViewComponent
{
    public virtual IViewComponentResult Invoke()
    {
        return View("~/Themes/LiquidGlass/Components/Toolbar/Appearance/Default.cshtml");
    }
}
