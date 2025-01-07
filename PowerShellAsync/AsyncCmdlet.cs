using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace TTRider.PowerShellAsync
{
    internal class Debug
    {
        [Conditional("TRACE")]
        public static void Trace(int objectHash,
                                 object? arg0 = default,
                                 object? arg1 = default,
                                 object? arg2 = default,
                                 object? arg3 = default)
        {
            var methodInfo = new StackTrace()!.GetFrame(1)!.GetMethod()!;
            if (methodInfo.Name == "Trace")
                methodInfo = new StackTrace()!.GetFrame(2)!.GetMethod()!;
            bool notNull(object? o) => o != null;
            string display(object? o)
            {
                if (o == null) return string.Empty;
                if (o is System.Delegate d) return d.Method.Name;
                return o.ToString() ?? string.Empty;
            }
            var tid = System.Threading.Thread.CurrentThread.ManagedThreadId;
            var sout = (String.Join(' ',
                ((object?[])[tid, objectHash, methodInfo.DeclaringType!.FullName, methodInfo.Name, arg0, arg1, arg2, arg3])
                .Where(notNull)
                .Select(display)));
            System.Diagnostics.Trace.WriteLine(sout);
        }
    }

    /// <summary>
    /// Base class for Cmdlets that run asynchronously.
    /// </summary>
    /// <remarks>
    ///	Inherit from this class if your Cmdlet needs to use <c>async</c> / <c>await</c> functionality.
    /// </remarks>
    public abstract class AsyncCmdlet : PSCmdlet, IDisposable
    {
        [Conditional("TRACE")]
        private void Trace(
            object? arg0 = default,
            object? arg1 = default,
            object? arg2 = default,
            object? arg3 = default) =>
            Debug.Trace(GetHashCode(), arg0, arg1, arg2, arg3);

        private static readonly TimeSpan CancellationTimeout = TimeSpan.FromMicroseconds(250);

        /// <summary>
        ///	The synchronisation context to run all async tasks on a single thread, the powershell thread.
        /// </summary>
        private readonly ThreadAffinitiveSynchronizationContext _syncContext = new();

        #region Construction and Destruction

        /// <summary>
        ///	Initialiser the <see cref="AsyncCmdlet"/>.
        /// </summary>
        protected AsyncCmdlet()
        {
            Trace();
        }

        /// <summary>
        ///	Disposer for <see cref="AsyncCmdlet"/>.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///	Dispose of resources being used by the <see cref="AsyncCmdlet"/>.
        /// </summary>
        /// <param name="disposing">
        ///	Explicit disposal?
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            Trace(disposing);
            if (disposing)
            {
                _syncContext.Dispose();
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
            Trace();
            this._syncContext.SendAsync(BeginProcessingAsync);
        }

        /// <summary>
        /// Sealed, will post to the async context, performs execution of the command.
        /// <see cref="Cmdlet.BeginProcessing()"/> for details.
        /// </summary>
        protected sealed override void ProcessRecord()
        {
            Trace();
            this._syncContext.SendAsync(ProcessRecordAsync);
        }

        /// <summary>
        /// Sealed, will post to the async context, performs clean-up after the command execution.
        /// <see cref="Cmdlet.BeginProcessing()"/> for details.
        /// </summary>
        protected sealed override void EndProcessing()
        {
            Trace();
            this._syncContext.SendAsync(EndProcessingAsync);
        }

        /// <summary>
        /// Sealed, will post to the async context, interrupts currently running code within
        /// the command. It should interrupt BeginProcessing, ProcessRecord, and EndProcessing.
        /// <see cref="Cmdlet.BeginProcessing()"/> for details.
        /// </summary>
        protected sealed override void StopProcessing()
        {
            //this doesn't run from the pipeline thread.
            Trace();
            this._syncContext.Send(StopProcessingAsync).Wait(CancellationTimeout);
            this._syncContext.Cancel();
        }

        #endregion Sealed Overrides

        #region Intercepted Methods

        /// <summary>
        /// Display debug information. Will post to the async context.
        /// <see cref="Cmdlet.WriteDebug(string)"/> for details.
        /// </summary>
        public new void WriteDebug(string text)
        {
            Trace(text);
            this._syncContext.Post(() => base.WriteDebug(text));
        }

        /// <summary>
        /// Writes the specified error to the error pipe. Will post to the async context.
        /// <see cref="Cmdlet.WriteError(ErrorRecord)"/> for details.
        /// </summary>
        public new void WriteError(ErrorRecord errorRecord)
        {
            Trace(errorRecord);
            this._syncContext.Post(() => base.WriteError(errorRecord));
        }

        /// <summary>
        /// Writes the object to the output pipe. Will post to the async context.
        /// <see cref="Cmdlet.WriteObject(object)"/> for details.
        /// </summary>
        public new void WriteObject(object sendToPipeline)
        {
            Trace(sendToPipeline);
            this._syncContext.Post(() => base.WriteObject(sendToPipeline));
        }

        /// <summary>
        /// Writes one or more objects to the output pipe. Will post to the async context.
        /// <see cref="Cmdlet.WriteObject(object, bool)"/> for details.
        /// </summary>
        public new void WriteObject(object sendToPipeline, bool enumerateCollection)
        {
            Trace(sendToPipeline, enumerateCollection);
            this._syncContext.Post(() => base.WriteObject(sendToPipeline, enumerateCollection));
        }

        /// <summary>
        /// Display progress information. Will post to the async context.
        /// <see cref="Cmdlet.WriteProgress(ProgressRecord)"/> for details.
        /// </summary>
        /// <param name="progressRecord">Progress information.</param>
        public new void WriteProgress(ProgressRecord progressRecord)
        {
            Trace(progressRecord);
            this._syncContext.Post(() => base.WriteProgress(progressRecord));
        }

        /// <summary>
        /// Display verbose information. Will post to the async context.
        /// <see cref="Cmdlet.WriteVerbose(string)"/> for details.
        /// </summary>
        public new void WriteVerbose(string text)
        {
            Trace(text);
            this._syncContext.Post(() => base.WriteVerbose(text));
        }

        /// <summary>
        /// Display warning information. Will post to the async context.
        /// <see cref="Cmdlet.WriteWarning(string)"/> for details.
        /// </summary>
        public new void WriteWarning(string text)
        {
            Trace(text);
            this._syncContext.Post(() => base.WriteWarning(text));
        }

        /// <summary>
        /// Write text into pipeline execution log. Will post to the async context.
        /// <see cref="Cmdlet.WriteCommandDetail(string)"/> for details.
        /// </summary>
        public new void WriteCommandDetail(string text)
        {
            Trace(text);
            this._syncContext.Post(() => base.WriteCommandDetail(text));
        }

        /// <summary>
        /// Terminate the command and report an error. Will post to the async context.
        /// <see cref="Cmdlet.ThrowTerminatingError(ErrorRecord)"/> for details.
        /// </summary>
        public new void ThrowTerminatingError(ErrorRecord errorRecord)
        {
            Trace(errorRecord);
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
            Trace(target);
            return this._syncContext.Send(() => base.ShouldProcess(target));
        }

        /// <summary>
        /// Confirm the operation with the user for WhatIf.
        /// <see cref="Cmdlet.ShouldProcess(string, string)"/> for details.
        /// </summary>
        public new bool ShouldProcess(string target, string action)
        {
            Trace(target, action);
            return this._syncContext.Send(() => base.ShouldProcess(target, action));
        }

        /// <summary>
        /// Confirm the operation with the user for WhatIf.
        /// <see cref="Cmdlet.ShouldProcess(string, string, string)"/> for details.
        /// </summary>
        public new bool ShouldProcess(string verboseDescription, string verboseWarning, string caption)
        {
            Trace(verboseDescription, verboseWarning, caption);
            return this._syncContext.Send(() => base.ShouldProcess(verboseDescription, verboseWarning, caption));
        }

        /// <summary>
        /// Confirm the operation with the user for WhatIf.
        /// <see cref="Cmdlet.ShouldProcess(string, string, string, out ShouldProcessReason)"/> for details.
        /// </summary>
        public new bool ShouldProcess(string verboseDescription, string verboseWarning, string caption,
            out ShouldProcessReason shouldProcessReason)
        {
            Trace(verboseDescription, verboseWarning, caption);
            var result = this._syncContext.Send(() =>
            {
                var result = base.ShouldProcess(verboseDescription, verboseWarning, caption, out var reason);
                return (result, reason);
            });
            shouldProcessReason = result.reason;
            Trace("result", result);
            return result.result;
        }

        /// <summary>
        /// Confirm the operation with the user always except when Force.
        /// <see cref="Cmdlet.ShouldContinue(string, string)"/> for details.
        /// </summary>
        public new bool ShouldContinue(string query, string caption)
        {
            Trace(query, caption);
            return this._syncContext.Send(() => base.ShouldContinue(query, caption));
        }

        /// <summary>
        /// Confirm the operation with the user always except when Force.
        /// <see cref="Cmdlet.ShouldContinue(string, string, ref bool, ref bool)"/> for details.
        /// </summary>
        public new bool ShouldContinue(string query, string caption, ref bool yesToAll, ref bool noToAll)
        {
            Trace(query, caption);
            var _yesToAll = yesToAll;
            var _noToAll = noToAll;
            var result = this._syncContext.Send(() =>
            {
                var result = base.ShouldContinue(query, caption, ref _yesToAll, ref _noToAll);
                return (result, _yesToAll, _noToAll);
            });
            yesToAll = result._yesToAll;
            noToAll = result._noToAll;
            Trace("result", yesToAll, noToAll);
            return result.result;
        }

        /// <summary>
        /// Returns true if a transaction is available and active.
        /// <see cref="Cmdlet.TransactionAvailable()"/> for details.
        /// </summary>
        public new bool TransactionAvailable()
        {
            Trace();
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
            return BeginProcessingAsync(_syncContext.CancellationToken);
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
            return EndProcessingAsync(_syncContext.CancellationToken);
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
            return ProcessRecordAsync(_syncContext.CancellationToken);
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
            return StopProcessingAsync(ThreadAffinitiveSynchronizationContext.Current!.CancellationToken);
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
            [Conditional("TRACE")]
            private void Trace(
                object? arg0 = default,
                object? arg1 = default,
                object? arg2 = default,
                object? arg3 = default) =>
                Debug.Trace(GetHashCode(), arg0, arg1, arg2, arg3);

            /// <summary>
            ///	The source for cancellation tokens that can be used to cancel the operation.
            /// </summary>
            CancellationTokenSource? _cancellationTokenSource = new();


            /// <summary>
            ///	A blocking collection (effectively a queue) of work items to execute, consisting of callback delegates and their callback state (if any).
            /// </summary>
            private BlockingCollection<(SendOrPostCallback? callback, object? callbackState)>? _workItemQueue;


            /// <summary>
            ///	Lock used on state-change start/stop.
            /// </summary>
            private object _workItemQueueLock = new();


            /// <summary>
            ///	Create a new thread-affinitive synchronisation context.
            /// </summary>
            public ThreadAffinitiveSynchronizationContext()
            {
                Trace();
            }


            /// <summary>
            ///	The current synchonization context for the thread.
            /// </summary>
            public static new ThreadAffinitiveSynchronizationContext? Current =>
                SynchronizationContext.Current as ThreadAffinitiveSynchronizationContext;


            /// <summary>
            ///	The cancellation token that can be used to register operations for cancellation.
            /// </summary>
            public CancellationToken CancellationToken => _cancellationTokenSource!.Token;


            /// <summary>
            ///	Cancel the operation and all outgoing tasks.
            /// </summary>
            public void Cancel() => _cancellationTokenSource!.Cancel();


            /// <summary>
            ///	Dispose of resources being used by the synchronisation context.
            /// </summary>
            public void Dispose()
            {
                Trace();
                StopMessagePump();
                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                }
            }


            /// <summary>
            ///	Check if the synchronisation context has been disposed.
            /// </summary>
            void CheckDisposed()
            {
                Trace(_workItemQueue == null);
                ObjectDisposedException.ThrowIf(_workItemQueue == null, GetType());
            }


            /// <summary>
            ///	Start the message pump again for more callbacks.
            /// </summary>
            void StartMessagePump()
            {
                Trace();
                ObjectDisposedException.ThrowIf(_workItemQueue != null, GetType());

                lock (_workItemQueueLock)
                {
                _workItemQueue = new();
            }
            }


            /// <summary>
            ///	Run the message pump for the callback queue on the current thread.
            /// </summary>
            void RunMessagePump(CancellationToken cancellationToken)
            {
                Trace();
                CheckDisposed();

                while (!_workItemQueue!.IsCompleted && !cancellationToken.IsCancellationRequested)
                {
                    Trace("trytake");
                    while (_workItemQueue!.TryTake(out var workItem, Timeout.Infinite, cancellationToken))
                    {
                        var (callback, state) = workItem;
                        Trace("take", callback);
                        callback?.Invoke(state);

                        // Has the synchronisation context been disposed?
                        if (_workItemQueue == null)
                            break;
                    }
                }
                Trace("completed", _workItemQueue!.IsCompleted, "cancelled", cancellationToken.IsCancellationRequested);
            }


            /// <summary>
            ///	Terminate the message pump once all callbacks have completed.
            /// </summary>
            void TerminateMessagePump()
            {
                Trace();
                CheckDisposed();

                _workItemQueue!.CompleteAdding();
            }


            /// <summary>
            ///	Stop the message pump and free resources.
            /// </summary>
            void StopMessagePump()
            {
                Trace();
                lock (_workItemQueueLock)
                {
                    if (_workItemQueue != null)
                    {
                        TerminateMessagePump(); //signal waiters
                        _workItemQueue!.Dispose();
                        _workItemQueue = null;
                    }
                }
            }


            /// <summary>
            ///	Allow using clause with the message pump start/stop.
            /// </summary>
            public sealed class MessagePumpScope : IDisposable
            {
                private readonly ThreadAffinitiveSynchronizationContext _savedContext;

                public MessagePumpScope(ThreadAffinitiveSynchronizationContext context)
                {
                    Debug.Trace(GetHashCode());
                    _savedContext = context;
                    _savedContext.StartMessagePump();
                }

                public void Dispose()
                {
                    Debug.Trace(GetHashCode());
                    _savedContext.StopMessagePump();
                }
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
                Trace(action);
                TaskCompletionSource<T> tcs = new();
                Post(_ =>
                {
                    try
                    {
                        Trace("run", action);
                        tcs.SetResult(action());
                    }
                    catch (Exception ex)
                    {
                        Trace("exn", ex);
                        tcs.SetException(ex);
                    }
                }, null);
                Trace("completed", action);
                return tcs.Task.Result;
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
                using (new SynchronizationContextScope(this))
                using (new MessagePumpScope(this))
                {
                    Post(_ =>
                    {
                        Trace("new task", action);
                        var task = action();
                        task.ContinueWith(t =>
                        {
                            Trace("completed", action);
                            if (t.IsFaulted)
                            {
                                Post(_ =>
                                {
                                    Trace("fault", t.Exception);
                                    throw t.Exception;
                                }, null);
                            }
                            TerminateMessagePump();
                        }, scheduler: TaskScheduler.Default);
                    }, null);
                    RunQueueSynchronized(timeout);
                };
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
                Trace(action);
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
                ArgumentNullException.ThrowIfNull(callback);

                // Implement reentrancy
                if (Current is not null)
                {
                    using (new SynchronizationContextScope(this))
                    {
                        Trace("reentrancy", callback);
                        callback!(callbackState);
                        return;
                    };
                }

                // Send it to the Queue to be run in the proper thread.
                try
                {
                    Trace("queue", callback);
                    _workItemQueue!.Add((callback, callbackState));
                }
                catch (Exception e) when (e is NullReferenceException || e is ObjectDisposedException)
                {
                    // queue was disposed because of cancellation.
                }
                catch (InvalidOperationException eMessagePumpAlreadyTerminated)
                {
                    throw new InvalidOperationException(
                        "Cannot enqueue the specified callback because the synchronisation context's message pump has already been terminated.",
                        eMessagePumpAlreadyTerminated
                        );
                }
            }


            /// <summary>
            ///	Run the queue synchronously until it becomes empty.
            /// </summary>
            /// <param name="timeout">Timeout the task has to run until it is cancelled.</param>
            private static void RunQueueSynchronized(TimeSpan? timeout = null)
            {
                Debug.Trace(0, timeout);
                ArgumentNullException.ThrowIfNull(Current);
                var cancellationToken = Current.CancellationToken;

                if (timeout != null)
                {
                    var cts = CancellationTokenSource.CreateLinkedTokenSource(Current.CancellationToken);
                    cts.CancelAfter(timeout.Value);
                    cancellationToken = cts.Token;
                }

                try
                {
                    Current.RunMessagePump(cancellationToken);
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


        /// <summary>
        ///	Allow using clause with a synchronisation context (<see cref="SynchronizationContext"/>).
        /// </summary>
        public sealed class SynchronizationContextScope : IDisposable
        {
            private readonly SynchronizationContext? _savedContext;

            public SynchronizationContextScope(SynchronizationContext newContext)
            {
                Debug.Trace(GetHashCode());
                _savedContext = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(newContext);
            }

            public void Dispose()
            {
                Debug.Trace(GetHashCode());
                SynchronizationContext.SetSynchronizationContext(_savedContext);
            }
        }
    }
}
