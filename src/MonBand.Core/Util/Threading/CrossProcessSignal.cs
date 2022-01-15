using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace MonBand.Core.Util.Threading;

public sealed class CrossProcessSignal : IDisposable
{
    readonly EventWaitHandle _waitHandle;

    public CrossProcessSignal(string eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(eventName));
        }

        this._waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
    }

    public void Signal()
    {
        this._waitHandle.Set();
    }

    [SuppressMessage("ReSharper", "HeapView.ClosureAllocation")]
    public Task WaitForSignalAsync()
    {
        var taskCompletionSource = new TaskCompletionSource<bool>();
        var registeredWaitHandle = ThreadPool.RegisterWaitForSingleObject(
            this._waitHandle,
            (_, _) => taskCompletionSource.TrySetResult(true),
            null,
            -1,
            true);
        var returnTask = taskCompletionSource.Task;
        returnTask.ContinueWith(_ => registeredWaitHandle.Unregister(null));
        return returnTask;
    }

    public void Dispose()
    {
        this._waitHandle.Dispose();
    }
}
