namespace SayHello.ShortLink;

public static class ShortLinkErrorCodes
{
    public const string InvalidCode = "ShortLink:010001";
    public const string ReservedCode = "ShortLink:010002";
    public const string CodeAlreadyExists = "ShortLink:010003";
    public const string InvalidTargetUrl = "ShortLink:010004";
    public const string UnsafeTargetUrl = "ShortLink:010005";
    public const string TargetHostCannotBeResolved = "ShortLink:010006";
    public const string BlockedTargetDomain = "ShortLink:010007";
    public const string LinkQuotaExceeded = "ShortLink:010008";
    public const string EmailNotConfirmed = "ShortLink:010009";
    public const string LinkAccessDenied = "ShortLink:010010";
    public const string InvalidState = "ShortLink:010011";
    public const string CreationRateExceeded = "ShortLink:010012";
    public const string BlockedDomainAlreadyExists = "ShortLink:010013";
    public const string InvalidSettingValue = "ShortLink:010014";
    public const string BlockedDomainCacheLockTimeout = "ShortLink:010015";
    public const string BlockedDomainImportEmpty = "ShortLink:010016";
    public const string BlockedDomainImportTooLarge = "ShortLink:010017";
    public const string BlockedDomainImportTooManyRows = "ShortLink:010018";
    public const string BlockedDomainImportInvalidExtension = "ShortLink:010019";
    public const string BlockedDomainImportMissingDomainHeader = "ShortLink:010020";
    public const string BlockedDomainImportInvalidCsv = "ShortLink:010021";
}
