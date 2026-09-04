using Volo.Abp.Settings;

namespace SayHello.ShortLink.WebHost.Settings;

public class WebHostSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        //Define your own settings here. Example:
        //context.Add(new SettingDefinition(WebHostSettings.MySetting1));
    }
}
