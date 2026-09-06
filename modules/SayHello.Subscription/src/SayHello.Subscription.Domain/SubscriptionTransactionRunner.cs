using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace SayHello.Subscription;

public class SubscriptionTransactionRunner : ITransientDependency
{
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    internal IUnitOfWork? Current => _unitOfWorkManager.Current;

    public SubscriptionTransactionRunner(IUnitOfWorkManager unitOfWorkManager) => _unitOfWorkManager = unitOfWorkManager;

    public async Task<T> ReadAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        using var read = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
        var result = await action();
        await read.CompleteAsync(cancellationToken);
        return result;
    }

    public async Task<T> RunAsync<T>(Func<IUnitOfWork, Task<T>> action, CancellationToken cancellationToken)
    {
        using var owned = _unitOfWorkManager.Current == null
            ? _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true)
            : null;
        var unitOfWork = owned ?? _unitOfWorkManager.Current!;
        if (!unitOfWork.Options.IsTransactional)
        {
            throw new AbpException("Subscription mutations require a transactional unit of work.");
        }

        try
        {
            var result = await action(unitOfWork);
            if (owned != null)
            {
                await owned.CompleteAsync(cancellationToken);
            }

            return result;
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
