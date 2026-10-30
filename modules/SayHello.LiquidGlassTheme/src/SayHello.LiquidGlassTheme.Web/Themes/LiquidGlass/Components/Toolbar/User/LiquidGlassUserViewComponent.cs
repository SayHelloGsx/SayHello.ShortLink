using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.UI.Navigation;

namespace SayHello.LiquidGlassTheme.Components.Toolbar.User;

public class LiquidGlassUserViewComponent : AbpViewComponent
{
    protected IMenuManager MenuManager { get; }

    public LiquidGlassUserViewComponent(IMenuManager menuManager)
    {
        MenuManager = menuManager;
    }

    public virtual async Task<IViewComponentResult> InvokeAsync()
    {
        return View("~/Themes/LiquidGlass/Components/Toolbar/User/Default.cshtml",
            await MenuManager.GetAsync(StandardMenus.User));
    }
}
