using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotThrow.Internal;

internal static class TaskHelpers
{
    // True when the task finished without error, so the caller can go on without awaiting.
    public static bool IsDone(this Task task) => task.Status == TaskStatus.RanToCompletion;

    // Turns an exception thrown before the first await into a faulted or canceled task,
    // the same as an async method would.
    public static Task<TResult> FromException<TResult>(Exception exception)
    {
        if (exception is OperationCanceledException canceled)
        {
            var source = new TaskCompletionSource<TResult>();
            source.TrySetCanceled(canceled.CancellationToken);
            return source.Task;
        }

        return Task.FromException<TResult>(exception);
    }

    public static Task FromException(Exception exception) => FromException<bool>(exception);

    public static void ThrowIfCanceled(CancellationToken cancellationToken) => cancellationToken.ThrowIfCancellationRequested();
}
