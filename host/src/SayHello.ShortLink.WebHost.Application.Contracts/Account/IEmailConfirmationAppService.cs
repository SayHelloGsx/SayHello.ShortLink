using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace SayHello.ShortLink.WebHost.Account;

[RemoteService(false)]
public interface IEmailConfirmationAppService : IApplicationService
{
    Task<bool> ConfirmAsync(Guid userId, string token);

    Task ResendAsync(string emailAddress);
}
