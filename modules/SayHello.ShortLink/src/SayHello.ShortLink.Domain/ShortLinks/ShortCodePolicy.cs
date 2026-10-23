using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace SayHello.ShortLink.ShortLinks;

public class ShortCodePolicy : ITransientDependency
{
    public string ValidateCustomCode(string code, bool allowReserved = false)
    {
        var normalizedCode = code.Trim();

        if (!ShortCodeFormat.IsValid(normalizedCode))
        {
            throw new BusinessException(ShortLinkErrorCodes.InvalidCode);
        }

        if (!allowReserved && ShortLinkReservedCodes.Contains(normalizedCode))
        {
            throw new BusinessException(ShortLinkErrorCodes.ReservedCode)
                .WithData("Code", normalizedCode);
        }

        return normalizedCode;
    }
}
