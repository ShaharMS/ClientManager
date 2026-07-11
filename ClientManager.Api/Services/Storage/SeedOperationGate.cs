using ClientManager.Api.Models.Exceptions;

namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Ensures only one seed operation runs at a time across all seed HTTP verbs.
/// </summary>
public sealed class SeedOperationGate
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        if (!await _semaphore.WaitAsync(0, cancellationToken))
        {
            throw new ConflictException(
                "A seed operation is already in progress. Wait for it to finish before starting another. " +
                "Seed export, import, and delete operations can take a long time for large statistics volumes.");
        }

        try
        {
            return await action(cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task RunAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        await RunAsync<object?>(async token =>
        {
            await action(token);
            return null;
        }, cancellationToken);
    }
}
