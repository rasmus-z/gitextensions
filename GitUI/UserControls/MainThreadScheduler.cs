﻿using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Threading.Tasks;

namespace GitUI.UserControls
{
    internal sealed class MainThreadScheduler : LocalScheduler
    {
        internal static readonly MainThreadScheduler Instance = new MainThreadScheduler();

        public override IDisposable Schedule<TState>(TState state, TimeSpan dueTime, Func<IScheduler, TState, IDisposable> action)
        {
            var disposable = new SingleAssignmentDisposable();
            var normalizedTime = Scheduler.Normalize(dueTime);
            ThreadHelper.JoinableTaskFactory.RunAsync(
                async () =>
                {
                    await Task.Delay(normalizedTime).ConfigureAwait(false);
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    disposable.Disposable = new MainThreadDisposable(action(this, state));
                });
            return disposable;
        }

        private sealed class MainThreadDisposable : ICancelable, IDisposable
        {
            private readonly IDisposable disposable;

            public MainThreadDisposable(IDisposable disposable)
            {
                this.disposable = disposable;
            }

            public bool IsDisposed
            {
                get;
                private set;
            }

            public void Dispose()
            {
                if (IsDisposed)
                {
                    return;
                }

                if (!ThreadHelper.JoinableTaskContext.IsOnMainThread)
                {
                    ThreadHelper.JoinableTaskFactory.Run(
                        async () =>
                        {
                            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                            Dispose();
                        });

                    return;
                }

                disposable.Dispose();
                IsDisposed = true;
            }
        }
    }
}
