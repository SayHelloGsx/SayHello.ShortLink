using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SayHello.ShortLink.WebHost.Migrations
{
    /// <inheritdoc />
    public partial class RemoveShortLinkNamespaceEntitlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A namespace quota now grants namespaces by itself, so quotas without the legacy boolean are revoked.
            migrationBuilder.Sql(
                """
                UPDATE "SubscriptionPlans" AS plan
                SET "ConcurrencyStamp" = replace(gen_random_uuid()::text, '-', '')
                WHERE plan."ProductCode" = 'short-link'
                  AND EXISTS (
                      SELECT 1
                      FROM "SubscriptionPlanEntitlements" AS entitlement
                      WHERE entitlement."PlanId" = plan."Id"
                        AND (
                            entitlement."FeatureKey" = 'namespaces'
                            OR (
                                entitlement."FeatureKey" = 'max-namespaces'
                                AND entitlement."ValueType" = 1
                                AND (entitlement."IsUnlimited" = TRUE OR entitlement."NumericValue" > 0)
                            )
                        )
                  );

                UPDATE "SubscriptionUserSubscriptions" AS subscription
                SET "ConcurrencyStamp" = replace(gen_random_uuid()::text, '-', '')
                WHERE subscription."ProductCode" = 'short-link'
                  AND EXISTS (
                      SELECT 1
                      FROM "SubscriptionUserSubscriptionEntitlements" AS entitlement
                      WHERE entitlement."SubscriptionId" = subscription."Id"
                        AND (
                            entitlement."FeatureKey" = 'namespaces'
                            OR (
                                entitlement."FeatureKey" = 'max-namespaces'
                                AND entitlement."ValueType" = 1
                                AND (entitlement."IsUnlimited" = TRUE OR entitlement."NumericValue" > 0)
                            )
                        )
                  );

                UPDATE "SubscriptionPlanEntitlements" AS entitlement
                SET "NumericValue" = 0,
                    "IsUnlimited" = FALSE
                FROM "SubscriptionPlans" AS plan
                WHERE entitlement."PlanId" = plan."Id"
                  AND plan."ProductCode" = 'short-link'
                  AND entitlement."FeatureKey" = 'max-namespaces'
                  AND entitlement."ValueType" = 1
                  AND (entitlement."IsUnlimited" = TRUE OR entitlement."NumericValue" > 0)
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "SubscriptionPlanEntitlements" AS legacy
                      WHERE legacy."PlanId" = entitlement."PlanId"
                        AND legacy."FeatureKey" = 'namespaces'
                        AND legacy."ValueType" = 0
                        AND legacy."BooleanValue" = TRUE
                  );

                UPDATE "SubscriptionUserSubscriptionEntitlements" AS entitlement
                SET "NumericValue" = 0,
                    "IsUnlimited" = FALSE
                FROM "SubscriptionUserSubscriptions" AS subscription
                WHERE entitlement."SubscriptionId" = subscription."Id"
                  AND subscription."ProductCode" = 'short-link'
                  AND entitlement."FeatureKey" = 'max-namespaces'
                  AND entitlement."ValueType" = 1
                  AND (entitlement."IsUnlimited" = TRUE OR entitlement."NumericValue" > 0)
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "SubscriptionUserSubscriptionEntitlements" AS legacy
                      WHERE legacy."SubscriptionId" = entitlement."SubscriptionId"
                        AND legacy."FeatureKey" = 'namespaces'
                        AND legacy."ValueType" = 0
                        AND legacy."BooleanValue" = TRUE
                  );

                DELETE FROM "SubscriptionPlanEntitlements" AS entitlement
                USING "SubscriptionPlans" AS plan
                WHERE entitlement."PlanId" = plan."Id"
                  AND plan."ProductCode" = 'short-link'
                  AND entitlement."FeatureKey" = 'namespaces';

                DELETE FROM "SubscriptionUserSubscriptionEntitlements" AS entitlement
                USING "SubscriptionUserSubscriptions" AS subscription
                WHERE entitlement."SubscriptionId" = subscription."Id"
                  AND subscription."ProductCode" = 'short-link'
                  AND entitlement."FeatureKey" = 'namespaces';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Snapshot display names were localized at grant time, so the restored name follows the quota row's language.
            migrationBuilder.Sql(
                """
                UPDATE "SubscriptionPlans" AS plan
                SET "ConcurrencyStamp" = replace(gen_random_uuid()::text, '-', '')
                WHERE plan."ProductCode" = 'short-link'
                  AND EXISTS (
                      SELECT 1
                      FROM "SubscriptionPlanEntitlements" AS entitlement
                      WHERE entitlement."PlanId" = plan."Id"
                        AND entitlement."FeatureKey" = 'max-namespaces'
                        AND entitlement."ValueType" = 1
                        AND (entitlement."IsUnlimited" = TRUE OR entitlement."NumericValue" > 0)
                  )
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "SubscriptionPlanEntitlements" AS legacy
                      WHERE legacy."PlanId" = plan."Id"
                        AND legacy."FeatureKey" = 'namespaces'
                  );

                UPDATE "SubscriptionUserSubscriptions" AS subscription
                SET "ConcurrencyStamp" = replace(gen_random_uuid()::text, '-', '')
                WHERE subscription."ProductCode" = 'short-link'
                  AND EXISTS (
                      SELECT 1
                      FROM "SubscriptionUserSubscriptionEntitlements" AS entitlement
                      WHERE entitlement."SubscriptionId" = subscription."Id"
                        AND entitlement."FeatureKey" = 'max-namespaces'
                        AND entitlement."ValueType" = 1
                        AND (entitlement."IsUnlimited" = TRUE OR entitlement."NumericValue" > 0)
                  )
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "SubscriptionUserSubscriptionEntitlements" AS legacy
                      WHERE legacy."SubscriptionId" = subscription."Id"
                        AND legacy."FeatureKey" = 'namespaces'
                  );

                INSERT INTO "SubscriptionPlanEntitlements"
                    ("PlanId", "FeatureKey", "TenantId", "ValueType", "BooleanValue", "IsUnlimited")
                SELECT entitlement."PlanId", 'namespaces', entitlement."TenantId", 0, TRUE, FALSE
                FROM "SubscriptionPlanEntitlements" AS entitlement
                JOIN "SubscriptionPlans" AS plan ON plan."Id" = entitlement."PlanId"
                WHERE plan."ProductCode" = 'short-link'
                  AND entitlement."FeatureKey" = 'max-namespaces'
                  AND entitlement."ValueType" = 1
                  AND (entitlement."IsUnlimited" = TRUE OR entitlement."NumericValue" > 0)
                ON CONFLICT ("PlanId", "FeatureKey") DO NOTHING;

                INSERT INTO "SubscriptionUserSubscriptionEntitlements"
                    ("SubscriptionId", "FeatureKey", "TenantId", "DisplayName", "ValueType", "BooleanValue", "IsUnlimited")
                SELECT entitlement."SubscriptionId",
                    'namespaces',
                    entitlement."TenantId",
                    CASE entitlement."DisplayName"
                        WHEN '命名空间数量上限' THEN '短链接命名空间'
                        ELSE 'Short-link namespaces'
                    END,
                    0,
                    TRUE,
                    FALSE
                FROM "SubscriptionUserSubscriptionEntitlements" AS entitlement
                JOIN "SubscriptionUserSubscriptions" AS subscription
                    ON subscription."Id" = entitlement."SubscriptionId"
                WHERE subscription."ProductCode" = 'short-link'
                  AND entitlement."FeatureKey" = 'max-namespaces'
                  AND entitlement."ValueType" = 1
                  AND (entitlement."IsUnlimited" = TRUE OR entitlement."NumericValue" > 0)
                ON CONFLICT ("SubscriptionId", "FeatureKey") DO NOTHING;
                """);
        }
    }
}
