using System.Threading.Tasks;
using Volo.Abp.Identity;

namespace SayHello.ShortLink.WebHost.Account;

public interface IEmailConfirmationSender
{
    Task SendAsync(IdentityUser user, string appName);
}
