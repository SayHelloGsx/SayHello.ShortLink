namespace SayHello.Subscription;

public static class SubscriptionDbProperties
{
    public static string DbTablePrefix { get; set; } = "Subscription";
    public static string? DbSchema { get; set; }
    public const string ConnectionStringName = "Subscription";
}
