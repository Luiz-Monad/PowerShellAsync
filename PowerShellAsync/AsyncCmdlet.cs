using System;
using System.Collections.Concurrent;
using System.Management.Automation;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace TTRider.PowerShellAsync
{
    /// <summary>
    /// Base class for Cmdlets that run asynchronously.
    /// </summary>
    /// <remarks>
    ///	Inherit from this class if your Cmdlet needs to use <c>async</c> / <c>await</c> functionality.
    /// </remarks>
    public abstract class AsyncCmdlet : PSCmdlet
    {
        private static readonly TimeSpan CancellationTimeout = TimeSpan.FromMicroseconds(250);

        /// <summary>
        ///	The source for cancellation tokens that can be used to cancel the operation.
        /// </summary>
        readonly CancellationTokenSource _cancellationSource = new();

        /// <summary>
        ///	The synchronisation context to run all async tasks on a single thread, the powershell thread.
        /// </summary>
        readonly ThreadAffinitiveSynchronizationContext _syncContext = new();

        #region Construction and Destruction

        /// <summary>
        ///	Initialise the <see cref="AsyncCmdlet"/>.
        /// </summary>
        protected AsyncCmdlet()
        {
        }

        /// <summary>
        ///	Finaliser for <see cref="AsyncCmdlet"/>.
        /// </summary>
        ~AsyncCmdlet()
        {
            Dispose(false);
        }

        /// <summary>
        ///	Dispose of resources being used by the Cmdlet.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///	Dispose of resources being used by the Cmdlet.
        /// </summary>
        /// <param name="disposing">
        ///	Explicit disposal?
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cancellationSource.Dispose();
            }
        }

        #endregion Construction and Destruction

        #region Sealed Overrides

        /// <summary>
        /// Sealed, will delegate to the async context, performs initialization of command execution.
        /// <see cref="Cmdlet.BeginProcessing()"/> for details.
        /// </summary>
        protected sealed override void BeginProcessing()
        {
            this._syncContext.SendAsync(BeginProcessingAsync);
        }

        /// <summary>
        /// Sealed, will post to the async context, performs execution of the command.
        /// <see cref="Cmdlet.BeginProcessing()"/> for details.
        /// </summary>
        protected sealed override void ProcessRecord()
        {
            this._syncContext.SendAsync(ProcessRecordAsync);
        }

        /// <summary>
        /// Sealed, will post to the async context, performs clean-up after the command execution.
        /// <see cref="Cmdlet.BeginProcessing()"/> for details.
        /// </summary>
        protected sealed override void EndProcessing()
        {
            this._syncContext.SendAsync(EndProcessingAsync);
        }

        /// <summary>
        /// Sealed, will post to the async context, interrupts currently running code within
        /// the command. It should interrupt BeginProcessing, ProcessRecord, and EndProcessing.
        /// <see cref="Cmdlet.BeginProcessing()"/> for details.
        /// </summary>
        protected sealed override void StopProcessing()
        {
            this._syncContext.SendAsync(StopProcessingAsync, CancellationTimeout);
            this._cancellationSource.Cancel();
        }

        #endregion Sealed Overrides

        #region Intercepted Methods

        /// <summary>
        /// Display debug information. Will post to the async context.
        /// <see cref="Cmdlet.WriteDebug(string)"/> for details.
        /// </summary>
        public new void WriteDebug(string text)
        {
            this._syncContext.Post(() => base.WriteDebug(text));
        }

        /// <summary>
        /// Writes the specified error to the error pipe. Will post to the async context.
        /// <see cref="Cmdlet.WriteError(ErrorRecord)"/> for details.
        /// </summary>
        public new void WriteError(ErrorRecord errorRecord)
        {
            this._syncContext.Post(() => base.WriteError(errorRecord));
        }

        /// <summary>
        /// Writes the object to the output pipe. Will post to the async context.
        /// <see cref="Cmdlet.WriteObject(object)"/> for details.
        /// </summary>
        public new void WriteObject(object sendToPipeline)
        {
            this._syncContext.Post(() => base.WriteObject(sendToPipeline));
        }

        /// <summary>
        /// Writes one or more objects to the output pipe. Will post to the async context.
        /// <see cref="Cmdlet.WriteObject(object, bool)"/> for details.
        /// </summary>
        public new void WriteObject(object sendToPipeline, bool enumerateCollection)
        {
            this._syncContext.Post(() => base.WriteObject(sendToPipeline, enumerateCollection));
        }

        /// <summary>
        /// Display progress information. Will post to the async context.
        /// <see cref="Cmdlet.WriteProgress(ProgressRecord)"/> for details.
        /// </summary>
        /// <param name="progressRecord">Progress information.</param>
        public new void WriteProgress(ProgressRecord progressRecord)
        {
            this._syncContext.Post(() => base.WriteProgress(progressRecord));
        }

        /// <summary>
        /// Display verbose information. Will post to the async context.
        /// <see cref="Cmdlet.WriteVerbose(string)"/> for details.
        /// </summary>
        public new void WriteVerbose(string text)
        {
            this._syncContext.Post(() => base.WriteVerbose(text));
        }

        /// <summary>
        /// Display warning information. Will post to the async context.
        /// <see cref="Cmdlet.WriteWarning(string)"/> for details.
        /// </summary>
        public new void WriteWarning(string text)
        {
            this._syncContext.Post(() => base.WriteWarning(text));
        }

        /// <summary>
        /// Write text into pipeline execution log. Will post to the async context.
        /// <see cref="Cmdlet.WriteCommandDetail(string)"/> for details.
        /// </summary>
        public new void WriteCommandDetail(string text)
        {
            this._syncContext.Post(() => base.WriteCommandDetail(text));
        }

        /// <summary>
        /// Terminate the command and report an error. Will post to the async context.
        /// <see cref="Cmdlet.ThrowTerminatingError(ErrorRecord)"/> for details.
        /// </summary>
        public new void ThrowTerminatingError(ErrorRecord errorRecord)
        {
            this._syncContext.Post(() => base.ThrowTerminatingError(errorRecord));
        }

        #endregion Intercepted Methods

        #region Intercepted Functions with reentrancy

        /// <summary>
        /// Confirm the operation with the user for WhatIf.
        /// <see cref="Cmdlet.ShouldProcess(string)"/> for details.
        /// </summary>
        public new bool ShouldProcess(string target)
        {
            return this._syncContext.Send(() => base.ShouldProcess(target));
        }

        /// <summary>
        /// Confirm the operation with the user for WhatIf.
        /// <see cref="Cmdlet.ShouldProcess(string, string)"/> for details.
        /// </summary>
        public new bool ShouldProcess(string target, string action)
        {
            return this._syncContext.Send(() => base.ShouldProcess(target, action));
        }

        /// <summary>
        /// Confirm the operation with the user for WhatIf.
        /// <see cref="Cmdlet.ShouldProcess(string, string, string)"/> for details.
        /// </summary>
        public new bool ShouldProcess(string verboseDescription, string verboseWarning, string caption)
        {
            return this._syncContext.Send(() => base.ShouldProcess(verboseDescription, verboseWarning, caption));
        }

        /// <summary>
        /// Confirm the operation with the user for WhatIf.
        /// <see cref="Cmdlet.ShouldProcess(string, string, string, out ShouldProcessReason)"/> for details.
        /// </summary>
        public new bool ShouldProcess(string verboseDescription, string verboseWarning, string caption,
            out ShouldProcessReason shouldProcessReason)
        {
            var result = this._syncContext.Send(() =>
            {
                var result = base.ShouldProcess(verboseDescription, verboseWarning, caption, out var reason);
                return (result, reason);
            });
            shouldProcessReason = result.reason;
            return result.result;
        }

        /// <summary>
        /// Confirm the operation with the user always except when Force.
        /// <see cref="Cmdlet.ShouldContinue(string, string)"/> for details.
        /// </summary>
        public new bool ShouldContinue(string query, string caption)
        {
            return this._syncContext.Send(() => base.ShouldContinue(query, caption));
        }

        /// <summary>
        /// Confirm the operation with the user always except when Force.
        /// <see cref="Cmdlet.ShouldContinue(string, string, ref bool, ref bool)"/> for details.
        /// </summary>
        public new bool ShouldContinue(string query, string caption, ref bool yesToAll, ref bool noToAll)
        {
            var _yesToAll = yesToAll;
            var _noToAll = noToAll;
            var result = this._syncContext.Send(() =>
            {
                var result = base.ShouldContinue(query, caption, ref _yesToAll, ref _noToAll);
                return (result, _yesToAll, _noToAll);
            });
            yesToAll = result._yesToAll;
            noToAll = result._noToAll;
            return result.result;
        }

        /// <summary>
        /// Returns true if a transaction is available and active.
        /// <see cref="Cmdlet.TransactionAvailable()"/> for details.
        /// </summary>
        public new bool TransactionAvailable()
        {
            return this._syncContext.Send(() => base.TransactionAvailable());
        }

        #endregion Intercepted Functions with reentrancy

        #region Async Processing Methods

        /// <summary>
        ///	When overridden in the derived class, asynchronously performs initialization of command execution.
        /// Default implementation in the base class just returns.
        /// </summary>
        /// <returns>
        ///	A <see cref="Task"/> representing the asynchronous operation.
        /// </returns>
        /// <exception cref="System.Exception">
        /// This method is overridden in the implementation of individual Cmdlets, and can
        /// throw literally any exception.
        /// </exception>
        protected virtual Task BeginProcessingAsync()
        {
            return BeginProcessingAsync(_cancellationSource.Token);
        }

        /// <summary>
        ///	When overridden in the derived class, asynchronously performs initialization of command execution.
        /// Default implementation in the base class just returns.
        /// </summary>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> that can be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation.
        /// </returns>
        /// <exception cref="System.Exception">
        /// This method is overridden in the implementation of individual Cmdlets, and can
        /// throw literally any exception.
        /// </exception>
        protected virtual Task BeginProcessingAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        ///	When overridden in the derived class, asynchronously performs clean-up after the command execution.
        /// Default implementation in the base class just returns.
        /// </summary>
        /// <returns>
        ///	A <see cref="Task"/> representing the asynchronous operation.
        /// </returns>
        /// <exception cref="System.Exception">
        /// This method is overridden in the implementation of individual Cmdlets, and can
        /// throw literally any exception.
        /// </exception>
        protected virtual Task EndProcessingAsync()
        {
            return EndProcessingAsync(_cancellationSource.Token);
        }

        /// <summary>
        ///	When overridden in the derived class, asynchronously performs clean-up after the command execution.
        /// Default implementation in the base class just returns.
        /// </summary>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> that can be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation.
        /// </returns>
        /// <exception cref="System.Exception">
        /// This method is overridden in the implementation of individual Cmdlets, and can
        /// throw literally any exception.
        /// </exception>
        protected virtual Task EndProcessingAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// When overridden in the derived class, asynchronously performs execution of the command.
        /// </summary>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation.
        /// </returns>
        /// <exception cref="System.Exception">
        /// This method is overridden in the implementation of individual Cmdlets, and can
        /// throw literally any exception.
        /// </exception>
        protected virtual Task ProcessRecordAsync()
        {
            return ProcessRecordAsync(_cancellationSource.Token);
        }

        /// <summary>
        /// When overridden in the derived class, asynchronously performs execution of the command.
        /// </summary>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> that can be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation.
        /// </returns>
        /// <exception cref="System.Exception">
        /// This method is overridden in the implementation of individual Cmdlets, and can
        /// throw literally any exception.
        /// </exception>
        protected virtual Task ProcessRecordAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// When overridden in the derived class, asynchronously interrupts currently running code within
        /// the command. It should interrupt BeginProcessing, ProcessRecord, and EndProcessing.
        /// Default implementation in the base class just returns.
        /// This is the last chance to graacefully stop the asynchronous tasks, after this
        /// all ongoing asynchronous task are going to be cancelled by the token source.
        /// </summary>
        /// <exception cref="System.Exception">
        /// This method is overridden in the implementation of individual Cmdlets, and can
        /// throw literally any exception.
        /// </exception>
        protected virtual Task StopProcessingAsync()
        {
            return StopProcessingAsync(_cancellationSource.Token);
        }

        /// <summary>
        /// When overridden in the derived class, asynchronously interrupts currently running code within
        /// the command. It should interrupt BeginProcessing, ProcessRecord, and EndProcessing.
        /// Default implementation in the base class just returns.
        /// This is the last chance to graacefully stop the asynchronous tasks, after this
        /// all ongoing asynchronous task are going to be cancelled by the token source.
        /// </summary>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> that can be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation.
        /// </returns>
        /// <exception cref="System.Exception">
        /// This method is overridden in the implementation of individual Cmdlets, and can
        /// throw literally any exception.
        /// </exception>
        protected virtual Task StopProcessingAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        #endregion Async Processing Methods

        /// <summary>
        ///	A synchronisation context that runs all calls scheduled on it (via <see cref="SynchronizationContext.Post"/>) on a single thread.
        /// </summary>
        /// <remarks>
        ///	With thanks to Stephen Toub.
        /// </remarks>
        public sealed class ThreadAffinitiveSynchronizationContext
            : SynchronizationContext, IDisposable
        {
            /// <summary>
            ///	A blocking collection (effectively a queue) of work items to execute, consisting of callback delegates and their callback state (if any).
            /// </summary>
            BlockingCollection<(SendOrPostCallback? callback, object? callbackState)>? _workItemQueue;


            /// <summary>
            ///	Create a new thread-affinitive synchronisation context.
            /// </summary>
            public ThreadAffinitiveSynchronizationContext()
            {
            }


            /// <summary>
            ///	Dispose of resources being used by the synchronisation context.
            /// </summary>
            void IDisposable.Dispose()
            {
                if (_workItemQueue != null)
                {
                    TerminateMessagePump(); //signal waiters
                    StopMessagePump();
                }
            }


            /// <summary>
            ///	Check if the synchronisation context has been disposed.
            /// </summary>
            void CheckDisposed()
            {
                ObjectDisposedException.ThrowIf(_workItemQueue == null, GetType());
            }


            /// <summary>
            ///	Start the message pump again for more callbacks.
            /// </summary>
            void StartMessagePump()
            {
                ObjectDisposedException.ThrowIf(_workItemQueue != null, GetType());

                _workItemQueue = new();
            }


            /// <summary>
            ///	Run the message pump for the callback queue on the current thread.
            /// </summary>
            void RunMessagePump(TimeSpan? timeout = null)
            {
                CheckDisposed();

                while (_workItemQueue!.TryTake(out var workItem, timeout ?? Timeout.InfiniteTimeSpan))
                {
                    var (callback, state) = workItem;
                    callback?.Invoke(state);

                    // Has the synchronisation context been disposed?
                    if (_workItemQueue == null)
                        break;
                }
            }


            /// <summary>
            ///	Terminate the message pump once all callbacks have completed.
            /// </summary>
            void TerminateMessagePump()
            {
                CheckDisposed();

                _workItemQueue!.CompleteAdding();
            }


            /// <summary>
            ///	Stop the message pump and free resources.
            /// </summary>
            void StopMessagePump()
            {
                CheckDisposed();

                _workItemQueue!.Dispose();
                _workItemQueue = null;
            }


            /// <summary>
            /// Synchronously executes a delegate on this synchronization context and returns its result.
            /// </summary>
            /// <typeparam name="T">The type of the result.</typeparam>
            /// <param name="this">The synchronization context.</param>
            /// <param name="action">The delegate to execute.</param>
            /// <exception cref="InvalidOperationException">
            /// The message pump has already been started, and then terminated by calling <see cref="TerminateMessagePump"/>.
            /// </exception>
            public T Send<T>(Func<T> action)
            {
                return UseContext(() =>
                {
                    T? result = default;
                    Post(_ =>
                    {
                        result = action();
                    }, null);
                    RunQueueSynchronized(once: true);
                    return result!;
                });
            }


            // <summary>
            /// Synchronously executes a Task on this synchronization context and waits for it to finish.
            /// </summary>
            /// <param name="action">The task to execute.</param>
            /// <param name="timeout">Timeout the task has to run until it is cancelled.</param>
            /// <exception cref="InvalidOperationException">
            /// The message pump has already been started, and then terminated by calling <see cref="TerminateMessagePump"/>.
            /// </exception>
            public void SendAsync(Func<Task> action, TimeSpan? timeout = null)
            {
                UseContext(() =>
                {
                    StartMessagePump();
                    Post(_ =>
                    {
                        var task = action();
                        task.ContinueWith(_ =>
                        {
                            TerminateMessagePump();
                        }, scheduler: TaskScheduler.Default);
                    }, null);
                    RunQueueSynchronized(timeout);
                    StopMessagePump();
                    return true;
                });
            }


            /// <summary>
            /// Asynchronously executes a delegate on this synchronization context.
            /// </summary>
            /// <param name="action">The delegate to execute.</param>
            /// <exception cref="InvalidOperationException">
            /// The message pump has already been started, and then terminated by calling <see cref="TerminateMessagePump"/>.
            /// </exception>
            public void Post(Action action)
            {
                Post(_ => action(), null);
            }


            /// <summary>
            /// Asynchronously executes a delegate on this synchronization context.
            /// </summary>
            /// <param name="callback">
            ///	The <see cref="SendOrPostCallback"/>Delegate to call in the synchronisation context.
            /// </param>
            /// <param name="callbackState">
            /// Optional state data passed to the callback.
            /// </param>
            /// <exception cref="InvalidOperationException">
            /// The message pump has already been started, and then terminated by calling <see cref="TerminateMessagePump"/>.
            /// </exception>
            public override void Post(SendOrPostCallback? callback, object? callbackState)
            {
                ArgumentNullException.ThrowIfNull(callback, nameof(callback));
                CheckDisposed();

                // Implement reentrancy
                if (Current is ThreadAffinitiveSynchronizationContext)
                {
                    callback!(callbackState);
                    return;
                }

                // Send it to the Queue to be run in the proper thread.
                try
                {
                    _workItemQueue!.Add((callback, callbackState));
                }
                catch (InvalidOperationException eMessagePumpAlreadyTerminated)
                {
                    throw new InvalidOperationException(
                        "Cannot enqueue the specified callback because the synchronisation context's message pump has already been terminated.",
                        eMessagePumpAlreadyTerminated
                        );
                }
            }

            public T UseContext<T>(Func<T> value)
            {
                SynchronizationContext? savedContext = Current;
                try
                {
                    SetSynchronizationContext(this);
                    return value();
                }
                finally
                {
                    SetSynchronizationContext(savedContext);
                }
            }

            /// <summary>
            ///	Run the queue synchronously until it becomes empty.
            /// </summary>
            /// <param name="timeout">Timeout the task has to run until it is cancelled.</param>
            private static void RunQueueSynchronized(TimeSpan? timeout = null, bool once = false)
            {
                var synchronizationContext = Current as ThreadAffinitiveSynchronizationContext;
                System.Diagnostics.Debug.Assert(synchronizationContext != null);

                var cancellationToken = Task.Factory.CancellationToken;

                if (timeout != null)
                {
                    var cts = new CancellationTokenSource();
                    cts.CancelAfter(timeout.Value);
                    cancellationToken = cts.Token;
                }

                try
                {
                    while (!synchronizationContext._workItemQueue!.IsCompleted && !once)
                    {
                        synchronizationContext.RunMessagePump();
                    }
                }
                catch (AggregateException eWaitForTask) // The TPL will almost always wrap an AggregateException around any exception thrown by the async operation.
                {
                    // Is this just a wrapped exception?
                    AggregateException flattenedAggregate = eWaitForTask.Flatten();
                    if (flattenedAggregate.InnerExceptions.Count != 1)
                        throw; // Nope, genuine aggregate.

                    // Yep, so rethrow (preserving original stack-trace).
                    ExceptionDispatchInfo
                        .Capture(
                            flattenedAggregate
                                .InnerExceptions[0]
                        )
                        .Throw();

                    throw; // Never reached.
                }
            }
        }
    }
}
