using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Volo.Abp.Account;
using Volo.Abp.Account.Emailing;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;

namespace SayHello.ShortLink.WebHost.Account;

[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IAccountAppService))]
public class EmailConfirmationAccountAppService : AccountAppService
{
    private readonly IEmailConfirmationSender _emailConfirmationSender;

    public EmailConfirmationAccountAppService(
        IdentityUserManager userManager,
        IIdentityRoleRepository roleRepository,
        IAccountEmailer accountEmailer,
        IdentitySecurityLogManager identitySecurityLogManager,
        IOptions<IdentityOptions> identityOptions,
        IEmailConfirmationSender emailConfirmationSender)
        : base(
            userManager,
            roleRepository,
            accountEmailer,
            identitySecurityLogManager,
            identityOptions)
    {
        _emailConfirmationSender = emailConfirmationSender;
    }

    public override async Task<IdentityUserDto> RegisterAsync(RegisterDto input)
    {
        var result = await base.RegisterAsync(input);
        var user = await UserManager.GetByIdAsync(result.Id);
        await _emailConfirmationSender.SendAsync(user, input.AppName ?? "MVC");
        return result;
    }
}
