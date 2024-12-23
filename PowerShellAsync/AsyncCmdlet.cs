using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        /// <summary>
        ///	The source for cancellation tokens that can be used to cancel the operation.
        /// </summary>
        readonly CancellationTokenSource _cancellationSource = new CancellationTokenSource();

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
            ThreadAffinitiveSynchronizationContext.RunSynchronized(BeginProcessingAsync);
        }

        /// <summary>
        /// Sealed, will post to the async context, performs execution of the command.
        /// <see cref="Cmdlet.BeginProcessing()"/> for details.
        /// </summary>
        protected sealed override void ProcessRecord()
        {
            ThreadAffinitiveSynchronizationContext.RunSynchronized(ProcessRecordAsync);
        }

        /// <summary>
        /// Sealed, will post to the async context, performs clean-up after the command execution.
        /// <see cref="Cmdlet.BeginProcessing()"/> for details.
        /// </summary>
        protected sealed override void EndProcessing()
        {
            ThreadAffinitiveSynchronizationContext.RunSynchronized(EndProcessingAsync);
        }

        /// <summary>
        /// Sealed, will post to the async context, interrupts currently running code within
        /// the command. It should interrupt BeginProcessing, ProcessRecord, and EndProcessing.
        /// <see cref="Cmdlet.BeginProcessing()"/> for details.
        /// </summary>
        protected sealed override void StopProcessing()
        {
            ThreadAffinitiveSynchronizationContext.RunSynchronized(StopProcessingAsync);
        }

        #endregion Sealed Overrides

        #region Intercepted Methods

        /// <summary>
        /// Display debug information. Will post to the async context.
        /// <see cref="Cmdlet.WriteDebug(string)"/> for details.
        /// </summary>
        public new void WriteDebug(string text)
        {
            ThreadAffinitiveSynchronizationContext.RunSynchronized(() => base.WriteDebug(text));
        }

        /// <summary>
        /// Writes the specified error to the error pipe. Will post to the async context.
        /// <see cref="Cmdlet.WriteError(ErrorRecord)"/> for details.
        /// </summary>
        public new void WriteError(ErrorRecord errorRecord)
        {
            ThreadAffinitiveSynchronizationContext.RunSynchronized(() => base.WriteError(errorRecord));
        }

        /// <summary>
        /// Writes the object to the output pipe. Will post to the async context.
        /// <see cref="Cmdlet.WriteObject(object)"/> for details.
        /// </summary>
        public new void WriteObject(object sendToPipeline)
        {
            ThreadAffinitiveSynchronizationContext.RunSynchronized(() => base.WriteObject(sendToPipeline));
        }

        /// <summary>
        /// Writes one or more objects to the output pipe. Will post to the async context.
        /// <see cref="Cmdlet.WriteObject(object, bool)"/> for details.
        /// </summary>
        public new void WriteObject(object sendToPipeline, bool enumerateCollection)
        {
            ThreadAffinitiveSynchronizationContext.RunSynchronized(() => base.WriteObject(sendToPipeline, enumerateCollection));
        }

        /// <summary>
        /// Display progress information. Will post to the async context.
        /// <see cref="Cmdlet.WriteProgress(ProgressRecord)"/> for details.
        /// </summary>
        /// <param name="progressRecord">Progress information.</param>
        public new void WriteProgress(ProgressRecord progressRecord)
        {
            ThreadAffinitiveSynchronizationContext.RunSynchronized(() => base.WriteProgress(progressRecord));
        }

        /// <summary>
        /// Display verbose information. Will post to the async context.
        /// <see cref="Cmdlet.WriteVerbose(string)"/> for details.
        /// </summary>
        public new void WriteVerbose(string text)
        {
            ThreadAffinitiveSynchronizationContext.RunSynchronized(() => base.WriteVerbose(text));
        }

        /// <summary>
        /// Display warning information. Will post to the async context.
        /// <see cref="Cmdlet.WriteWarning(string)"/> for details.
        /// </summary>
        public new void WriteWarning(string text)
        {
            ThreadAffinitiveSynchronizationContext.RunSynchronized(() => base.WriteWarning(text));
        }

        /// <summary>
        /// Write text into pipeline execution log. Will post to the async context.
        /// <see cref="Cmdlet.WriteCommandDetail(string)"/> for details.
        /// </summary>
        public new void WriteCommandDetail(string text)
        {
            ThreadAffinitiveSynchronizationContext.RunSynchronized(() => base.WriteCommandDetail(text));
        }

        /// <summary>
        /// Terminate the command and report an error. Will post to the async context.
        /// <see cref="Cmdlet.ThrowTerminatingError(ErrorRecord)"/> for details.
        /// </summary>
        public new void ThrowTerminatingError(ErrorRecord errorRecord)
        {
            ThreadAffinitiveSynchronizationContext.RunSynchronized(() => base.ThrowTerminatingError(errorRecord));
        }

        #endregion Intercepted Methods

        #region Intercepted Functions with reentrancy

        /// <summary>
        /// Confirm the operation with the user for WhatIf.
        /// <see cref="Cmdlet.ShouldProcess(string)"/> for details.
        /// </summary>
        public new bool ShouldProcess(string target)
        {
            var workItem = Task.Run(() => base.ShouldProcess(target));
            ThreadAffinitiveSynchronizationContext.RunSynchronized(() => workItem);
            return workItem.Result;
        }

        /// <summary>
        /// Confirm the operation with the user for WhatIf.
        /// <see cref="Cmdlet.ShouldProcess(string, string)"/> for details.
        /// </summary>
        public new bool ShouldProcess(string target, string action)
        {
            var workItem = Task.Run(() => base.ShouldProcess(target, action));
            ThreadAffinitiveSynchronizationContext.RunSynchronized(() => workItem);
            return workItem.Result;
        }

        /// <summary>
        /// Confirm the operation with the user for WhatIf.
        /// <see cref="Cmdlet.ShouldProcess(string, string, string)"/> for details.
        /// </summary>
        public new bool ShouldProcess(string verboseDescription, string verboseWarning, string caption)
        {
            var workItem = Task.Run(() => base.ShouldProcess(verboseDescription, verboseWarning, caption));
            ThreadAffinitiveSynchronizationContext.RunSynchronized(() => workItem);
            return workItem.Result;
        }

        /// <summary>
        /// Confirm the operation with the user for WhatIf.
        /// <see cref="Cmdlet.ShouldProcess(string, string, string, out ShouldProcessReason)"/> for details.
        /// </summary>
        public new bool ShouldProcess(string verboseDescription, string verboseWarning, string caption,
            out ShouldProcessReason shouldProcessReason)
        {
            var workItem = Task.Run(() =>
            {
                var result = base.ShouldProcess(verboseDescription, verboseWarning, caption, out var reason);
                return (result, reason);
            });
            ThreadAffinitiveSynchronizationContext.RunSynchronized(() => workItem);
            shouldProcessReason = workItem.Result.reason;
            return workItem.Result.result;
        }

        /// <summary>
        /// Confirm the operation with the user always except when Force.
        /// <see cref="Cmdlet.ShouldContinue(string, string)"/> for details.
        /// </summary>
        public new bool ShouldContinue(string query, string caption)
        {
            var workItem = Task.Run(() => base.ShouldContinue(query, caption));
            ThreadAffinitiveSynchronizationContext.RunSynchronized(() => workItem);
            return workItem.Result;
        }

        /// <summary>
        /// Confirm the operation with the user always except when Force.
        /// <see cref="Cmdlet.ShouldContinue(string, string, ref bool, ref bool)"/> for details.
        /// </summary>
        public new bool ShouldContinue(string query, string caption, ref bool yesToAll, ref bool noToAll)
        {
            var _yesToAll = yesToAll;
            var _noToAll = noToAll;
            var workItem = Task.Run(() =>
            {
                var result = base.ShouldContinue(query, caption, ref _yesToAll, ref _noToAll);
                return (result, _yesToAll, _noToAll);
            });
            ThreadAffinitiveSynchronizationContext.RunSynchronized(() => workItem);
            yesToAll = workItem.Result._yesToAll;
            noToAll = workItem.Result._noToAll;
            return workItem.Result.result;
        }

        /// <summary>
        /// Returns true if a transaction is available and active.
        /// <see cref="Cmdlet.TransactionAvailable()"/> for details.
        /// </summary>
        public new bool TransactionAvailable()
        {
            var workItem = Task.Run(() => base.TransactionAvailable());
            ThreadAffinitiveSynchronizationContext.RunSynchronized(() => workItem);
            return workItem.Result;
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
        ///		A synchronisation context that runs all calls scheduled on it (via <see cref="SynchronizationContext.Post"/>) on a single thread.
        /// </summary>
        /// <remarks>
        ///		With thanks to Stephen Toub.
        /// </remarks>
        public sealed class ThreadAffinitiveSynchronizationContext
            : SynchronizationContext, IDisposable
        {
            /// <summary>
            ///		A blocking collection (effectively a queue) of work items to execute, consisting of callback delegates and their callback state (if any).
            /// </summary>
            BlockingCollection<KeyValuePair<SendOrPostCallback, object>> _workItemQueue = new BlockingCollection<KeyValuePair<SendOrPostCallback, object>>();


            /// <summary>
            ///		Create a new thread-affinitive synchronisation context.
            /// </summary>
            ThreadAffinitiveSynchronizationContext()
            {
            }


            /// <summary>
            ///		Dispose of resources being used by the synchronisation context.
            /// </summary>
            void IDisposable.Dispose()
            {
                if (_workItemQueue != null)
                {
                    _workItemQueue.Dispose();
                    _workItemQueue = null;
                }
            }


            /// <summary>
            ///		Check if the synchronisation context has been disposed.
            /// </summary>
            void CheckDisposed()
            {
                if (_workItemQueue == null)
                    throw new ObjectDisposedException(GetType().Name);
            }


            /// <summary>
            ///		Run the message pump for the callback queue on the current thread.
            /// </summary>
            void RunMessagePump()
            {
                CheckDisposed();


                KeyValuePair<SendOrPostCallback, object> workItem;
                while (_workItemQueue.TryTake(out workItem, Timeout.InfiniteTimeSpan))
                {
                    workItem.Key(workItem.Value);


                    // Has the synchronisation context been disposed?
                    if (_workItemQueue == null)
                        break;
                }
            }


            /// <summary>
            ///		Terminate the message pump once all callbacks have completed.
            /// </summary>
            void TerminateMessagePump()
            {
                CheckDisposed();


                _workItemQueue.CompleteAdding();
            }


            /// <summary>
            ///		Dispatch an asynchronous message to the synchronization context.
            /// </summary>
            /// <param name="callback">
            ///		The <see cref="SendOrPostCallback"/> delegate to call in the synchronisation context.
            /// </param>
            /// <param name="callbackState">
            ///		Optional state data passed to the callback.
            /// </param>
            /// <exception cref="InvalidOperationException">
            ///		The message pump has already been started, and then terminated by calling <see cref="TerminateMessagePump"/>.
            /// </exception>
            public override void Post(SendOrPostCallback callback, object callbackState)
            {
                if (callback == null)
                    throw new ArgumentNullException(nameof(callback));


                CheckDisposed();


                try
                {
                    _workItemQueue.Add(
                        new KeyValuePair<SendOrPostCallback, object>(
                            key: callback,
                            value: callbackState
                        )
                    );
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
            ///		Run an asynchronous operation using the current thread as its synchronisation context.
            /// </summary>
            /// <param name="asyncOperation">
            ///		A <see cref="Func{TResult}"/> delegate representing the asynchronous operation to run.
            /// </param>
            public static void RunSynchronized(Action asyncOperation)
            {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
                RunSynchronized<object>(async () =>
                {
                    asyncOperation();
                    return null!;
                });
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
            }


            /// <summary>
            ///		Run an asynchronous operation using the current thread as its synchronisation context.
            /// </summary>
            /// <param name="asyncOperation">
            ///		A <see cref="Func{TResult}"/> delegate representing the asynchronous operation to run.
            /// </param>
            public static void RunSynchronized(Func<Task> asyncOperation)
            {
                RunSynchronized<object>(async () =>
                {
                    await asyncOperation();
                    return null!;
                });
            }


            /// <summary>
            ///		Run an asynchronous operation using the current thread as its synchronisation context.
            /// </summary>
            /// <typeparam name="TResult">
            ///		The operation result type.
            /// </typeparam>
            /// <param name="asyncOperation">
            ///		A <see cref="Func{TResult}"/> delegate representing the asynchronous operation to run.
            /// </param>
            /// <returns>
            ///		The operation result.
            /// </returns>
            public static TResult RunSynchronized<TResult>(Func<Task<TResult>> asyncOperation)
            {
                if (asyncOperation == null)
                    throw new ArgumentNullException(nameof(asyncOperation));

                /// Implement reentrancy
                if (Current is ThreadAffinitiveSynchronizationContext)
                {
                    return asyncOperation().GetAwaiter().GetResult();
                }

                SynchronizationContext savedContext = Current;
                try
                {
                    using (ThreadAffinitiveSynchronizationContext synchronizationContext = new ThreadAffinitiveSynchronizationContext())
                    {
                        SetSynchronizationContext(synchronizationContext);


                        Task<TResult> rootOperationTask = asyncOperation();
                        if (rootOperationTask == null)
                            throw new InvalidOperationException("The asynchronous operation delegate cannot return null.");


                        rootOperationTask.ContinueWith(
                            operationTask =>
                                synchronizationContext.TerminateMessagePump(),
                            scheduler:
                                TaskScheduler.Default
                        );


                        synchronizationContext.RunMessagePump();


                        try
                        {
                            return
                                rootOperationTask
                                    .GetAwaiter()
                                    .GetResult();
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
                finally
                {
                    SetSynchronizationContext(savedContext);
                }
            }
        }

    }
}
