using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared.Toolbars;

namespace SayHello.LiquidGlassTheme.Components.Toolbar;

public class LiquidGlassToolbarViewComponent : AbpViewComponent
{
    protected IToolbarManager ToolbarManager { get; }

    public LiquidGlassToolbarViewComponent(IToolbarManager toolbarManager)
    {
        ToolbarManager = toolbarManager;
    }

    public virtual async Task<IViewComponentResult> InvokeAsync()
    {
        return View("~/Themes/LiquidGlass/Components/Toolbar/Default.cshtml",
            await ToolbarManager.GetAsync(StandardToolbars.Main));
    }
}
