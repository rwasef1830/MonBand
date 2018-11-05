using System;
using System.Threading;
using System.Threading.Tasks;

namespace MonBand.Core.Util
{
    public static class TaskCancellationExtensions
    {
        public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));

            var taskCompletionSource = new TaskCompletionSource<object>();
            using (cancellationToken.Register(
                o => ((TaskCompletionSource<object>)o).SetCanceled(),
                taskCompletionSource))
            {
                if (task != await Task.WhenAny(task, taskCompletionSource.Task).ConfigureAwait(false))
                {
#pragma warning disable 4014
                    task.ContinueWith(
                        t => t.Exception?.Handle(_ => true),
                        TaskContinuationOptions.OnlyOnFaulted);
#pragma warning restore 4014
                    await taskCompletionSource.Task.ConfigureAwait(false); // throws
                    return default;
                }
            }

            return await task.ConfigureAwait(false);
        }

        public static async Task WithCancellation(this Task task, CancellationToken cancellationToken)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));

            var taskCompletionSource = new TaskCompletionSource<object>();
            using (cancellationToken.Register(
                o => ((TaskCompletionSource<object>)o).SetCanceled(),
                taskCompletionSource))
            {
                if (task != await Task.WhenAny(task, taskCompletionSource.Task).ConfigureAwait(false))
                {
#pragma warning disable 4014
                    task.ContinueWith(
                        t => t.Exception?.Handle(_ => true),
                        TaskContinuationOptions.OnlyOnFaulted);
#pragma warning restore 4014
                    await taskCompletionSource.Task.ConfigureAwait(false); // throws
                    return;
                }
            }

            await task.ConfigureAwait(false);
        }
    }
}
