using System;
using System.Text.RegularExpressions;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace SayHello.ShortLink.ShortLinks;

public partial class ShortCodePolicy : ITransientDependency
{
    [GeneratedRegex("^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AllowedCodeRegex();

    public string ValidateCustomCode(string code)
    {
        var normalizedCode = code.Trim();

        if (normalizedCode.Length is < ShortLinkConsts.MinCodeLength or > ShortLinkConsts.MaxCodeLength ||
            !AllowedCodeRegex().IsMatch(normalizedCode))
        {
            throw new BusinessException(ShortLinkErrorCodes.InvalidCode);
        }

        if (ShortLinkReservedCodes.Contains(normalizedCode))
        {
            throw new BusinessException(ShortLinkErrorCodes.ReservedCode)
                .WithData("Code", normalizedCode);
        }

        return normalizedCode;
    }
}
