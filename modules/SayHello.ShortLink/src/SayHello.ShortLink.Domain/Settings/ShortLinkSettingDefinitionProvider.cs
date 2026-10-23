using SayHello.ShortLink.ShortLinks;
using Volo.Abp.Settings;
using SayHello.ShortLink.ShortLinkNamespaces;

namespace SayHello.ShortLink.Settings;

public class ShortLinkSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        context.Add(
            new SettingDefinition(
                ShortLinkSettings.DefaultNamespace,
                ShortLinkNamespaceConsts.DefaultNamespace)
                .WithProviders(DefaultValueSettingValueProvider.ProviderName, GlobalSettingValueProvider.ProviderName),
            new SettingDefinition(
                ShortLinkSettings.MaxLinksPerUser,
                ShortLinkDefaults.MaxLinksPerUser.ToString()),
            new SettingDefinition(
                ShortLinkSettings.CreateLimitPerHour,
                ShortLinkDefaults.CreateLimitPerHour.ToString()),
            new SettingDefinition(
                ShortLinkSettings.VisitRetentionDays,
                ShortLinkDefaults.VisitRetentionDays.ToString()),
            new SettingDefinition(
                ShortLinkSettings.DeletedCodeCooldownDays,
                ShortLinkDefaults.DeletedCodeCooldownDays.ToString()),
            new SettingDefinition(
                ShortLinkSettings.GeneratedCodeLength,
                ShortLinkConsts.GeneratedCodeLength.ToString()));
    }
}
