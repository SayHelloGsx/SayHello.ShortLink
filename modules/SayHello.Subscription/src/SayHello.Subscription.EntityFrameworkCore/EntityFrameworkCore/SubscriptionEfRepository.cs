using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace SayHello.Subscription.EntityFrameworkCore;

public abstract class SubscriptionEfRepository<TEntity> : EfCoreRepository<ISubscriptionDbContext, TEntity, Guid>
    where TEntity : AggregateRoot<Guid>, IMultiTenant
{
    private readonly ICurrentTenant _tenant;

    protected SubscriptionEfRepository(IDbContextProvider<ISubscriptionDbContext> provider, ICurrentTenant tenant)
        : base(provider)
    {
        _tenant = tenant;
    }

    protected void EnsureTenant(Guid? tenantId) => SubscriptionGuard.SameTenant(_tenant.Id, tenantId);

    public override async Task<TEntity?> FindAsync(Guid id, bool includeDetails = true,
        CancellationToken cancellationToken = default)
    {
        var query = includeDetails ? await WithDetailsAsync() : await GetQueryableAsync();
        return await query.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _tenant.Id, cancellationToken);
    }

    public override async Task<TEntity> GetAsync(Guid id, bool includeDetails = true,
        CancellationToken cancellationToken = default) =>
        await FindAsync(id, includeDetails, cancellationToken) ?? throw new EntityNotFoundException(typeof(TEntity), id);

    public override Task<TEntity> InsertAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        EnsureTenant(entity.TenantId);
        return TranslateAsync(() => base.InsertAsync(entity, autoSave, cancellationToken));
    }

    public override Task<TEntity> UpdateAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        EnsureTenant(entity.TenantId);
        return TranslateAsync(() => base.UpdateAsync(entity, autoSave, cancellationToken));
    }

    public override async Task DeleteAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        EnsureTenant(entity.TenantId);
        await TranslateAsync(async () =>
        {
            await base.DeleteAsync(entity, autoSave, cancellationToken);
            return entity;
        }, deleting: true);
    }

    private static async Task<TEntity> TranslateAsync(Func<Task<TEntity>> action, bool deleting = false)
    {
        try
        {
            return await action();
        }
        catch (AbpDbConcurrencyException exception)
        {
            throw new BusinessException(SubscriptionErrorCodes.ConcurrencyConflict, innerException: exception);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new BusinessException(SubscriptionErrorCodes.ConcurrencyConflict, innerException: exception);
        }
        catch (DbUpdateException exception) when (GetConstraintError(exception, deleting) != null)
        {
            throw new BusinessException(GetConstraintError(exception, deleting)!, innerException: exception);
        }
    }

    private static string? GetConstraintError(DbUpdateException exception, bool deleting)
    {
        var inner = exception.InnerException;
        if (inner == null)
        {
            return null;
        }

        // Provider-specific diagnostics are inspected without coupling the reusable module to a provider.
        var state = inner.GetType().GetProperty("SqlState")?.GetValue(inner) as string;
        var constraint = inner.GetType().GetProperty("ConstraintName")?.GetValue(inner) as string;
        var sqliteCode = inner.GetType().GetProperty("SqliteExtendedErrorCode")?.GetValue(inner) as int?;
        if (deleting && (state == "23503" || sqliteCode == 787))
        {
            return SubscriptionErrorCodes.CatalogReferenced;
        }

        if (state == "23505" && constraint != null)
        {
            if (constraint.StartsWith("UX_Subscription_Current_", StringComparison.Ordinal))
            {
                return SubscriptionErrorCodes.ConcurrencyConflict;
            }

            if (constraint.StartsWith("UX_Subscription_Product_", StringComparison.Ordinal) ||
                constraint.StartsWith("UX_Subscription_Plan_", StringComparison.Ordinal) ||
                constraint.StartsWith("UX_Subscription_Bundle_", StringComparison.Ordinal))
            {
                return SubscriptionErrorCodes.DuplicateCode;
            }
        }

        if (sqliteCode == 2067)
        {
            var prefix = SubscriptionDbProperties.DbTablePrefix;
            if (inner.Message.Contains(prefix + "UserSubscriptions.UserId", StringComparison.Ordinal) &&
                inner.Message.Contains(prefix + "UserSubscriptions.ProductId", StringComparison.Ordinal))
            {
                return SubscriptionErrorCodes.ConcurrencyConflict;
            }

            if (new[] { "Products", "Plans", "Bundles" }.Any(table =>
                    inner.Message.Contains(prefix + table + ".Code", StringComparison.Ordinal)))
            {
                return SubscriptionErrorCodes.DuplicateCode;
            }
        }

        return null;
    }
}
