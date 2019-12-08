using System;
using System.Threading;
using System.Threading.Tasks;

namespace MonBand.Core.Util
{
    public sealed class CrossProcessSignal : IDisposable
    {
        readonly EventWaitHandle _reloadEvent;
        readonly CancellationTokenSource _waitCancellationSource;
        readonly Task _reloadTask;

        public event EventHandler Signaled;

        public CrossProcessSignal(string eventName)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(eventName));
            }

            this._reloadEvent = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
            this._waitCancellationSource = new CancellationTokenSource();
            this._reloadTask = Task.Run(this.WaitForSignal);
        }

        public static void Signal(string eventName)
        {
            using var handle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
            handle.Set();
        }

        async Task WaitForSignal()
        {
            try
            {
                while (true)
                {
                    await Task.Run(() => this._reloadEvent.WaitOne())
                        .WithCancellation(this._waitCancellationSource.Token)
                        .ConfigureAwait(false);
                    this.Signaled?.Invoke(this, EventArgs.Empty);
                }
            }
            catch
            {
                // ignore
            }
        }

        public void Dispose()
        {
            this._waitCancellationSource.Cancel();
            this._reloadTask.GetAwaiter().GetResult();
            this._reloadEvent.Dispose();
        }
    }
}
