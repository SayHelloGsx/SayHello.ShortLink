namespace SayHello.Subscription.Admin.Permissions;

public static class SubscriptionAdminPermissions
{
    public const string GroupName = "Subscription";
    public const string Default = "Subscription.Admin";

    public static class Products
    {
        public const string Default = "Subscription.Admin.Products";
        public const string Create = Default + ".Create";
        public const string Update = Default + ".Update";
        public const string Delete = Default + ".Delete";
        public const string Publish = Default + ".Publish";
    }

    public static class Plans
    {
        public const string Default = "Subscription.Admin.Plans";
        public const string Create = Default + ".Create";
        public const string Update = Default + ".Update";
        public const string Delete = Default + ".Delete";
        public const string Publish = Default + ".Publish";
    }

    public static class Bundles
    {
        public const string Default = "Subscription.Admin.Bundles";
        public const string Create = Default + ".Create";
        public const string Update = Default + ".Update";
        public const string Delete = Default + ".Delete";
        public const string Publish = Default + ".Publish";
    }

    public static class Users
    {
        public const string Default = "Subscription.Admin.Users";
        public const string Lookup = Default + ".Lookup";
        public const string Assign = Default + ".Assign";
        public const string Revoke = Default + ".Revoke";
        public const string AdjustExpiration = Default + ".AdjustExpiration";
    }
}
