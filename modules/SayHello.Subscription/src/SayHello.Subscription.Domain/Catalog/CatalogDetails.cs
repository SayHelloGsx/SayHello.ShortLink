namespace SayHello.Subscription.Catalog;

public sealed record CatalogDetails(string Name, string? Description = null, int DisplayOrder = 0);
