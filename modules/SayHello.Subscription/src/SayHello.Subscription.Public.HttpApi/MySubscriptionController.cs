using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SayHello.Subscription.Public.Subscriptions;
using SayHello.Subscription.Subscriptions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace SayHello.Subscription.Public;

[Authorize]
[RemoteService(Name = SubscriptionPublicRemoteServiceConsts.RemoteServiceName)]
[Area(SubscriptionPublicRemoteServiceConsts.ModuleName)]
[Route("api/subscription/public/mine")]
public class MySubscriptionController : AbpControllerBase, IMySubscriptionAppService
{
    private readonly IMySubscriptionAppService _service;

    public MySubscriptionController(IMySubscriptionAppService service)
    {
        _service = service;
    }

    [HttpGet]
    public Task<PagedResultDto<UserSubscriptionDto>> GetListAsync([FromQuery] GetMySubscriptionsInput input) =>
        _service.GetListAsync(input);

    [HttpGet("{id:guid}")]
    public Task<UserSubscriptionDto> GetAsync(Guid id) => _service.GetAsync(id);
}
