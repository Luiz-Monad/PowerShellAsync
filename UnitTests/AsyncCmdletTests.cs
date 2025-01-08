using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Services.Description;
using NUnit.Framework.Interfaces;
using TTRider.PowerShellAsync;
using TTRider.PowerShellAsync.UnitTests.Infrastructure;

namespace TTRider.PowerShellAsync.UnitTests
{
    [TestFixture]
    public class TestPsBase
    {
        private static Runspace? runspace;

        static TestPsBase()
        {
            //AsyncCmdlet.ThreadAffinitiveSynchronizationContext.TraceWriteLine = (s, o) => TestContext.Out.WriteLine(s, o);
            runspace = RunspaceFactory.CreateRunspace();
            runspace.Open();
            ImportModule();
        }

        private static void ImportModule()
        {
            RunCommand(ps =>
            {
                var path = new Uri(typeof(TestWriteObject).Assembly.Location);
                ps.AddCommand("Import-Module").AddArgument(path.LocalPath);
            });
        }

        private static (List<string> result, PowerShell powershell) RunCommand(Action<PowerShell> prepareAction, PsCommandContext? context = null)
        {
            var ps = PowerShell.Create();
            ps.Runspace = runspace;

            prepareAction(ps);

            var ret = new List<string>();

            var settings = new PSInvocationSettings {
                Host = new TestPsHost(context ?? new PsCommandContext())
            };

            foreach (var result in ps.Invoke(Array.Empty<object>(), settings))
            {
                Trace.WriteLine(result);
                ret.Add(result.ToString());
            }

            return (ret, ps);
        }

        private Predicate<object> stringMatcher(object o) => (object o2) => o.ToString() == o2.ToString();

        [Test]
        public void WriteObject()
        {
            var output = RunCommand(ps => ps.AddCommand("Test-WriteObject"));
            Assert.That(output.result, Is.EquivalentTo(TestData.Objects));
        }

        [Test]
        public void PropertyAccess()
        {
            var output = RunCommand(ps => ps.AddCommand("Test-PropertyAccess"));
            Assert.That(output.result.Count, Is.EqualTo(0));
        }

        [Test]
        public void SyncProcessing()
        {
            var output = RunCommand(ps =>
            {
                ps.AddCommand("Get-Date");
                ps.AddCommand("Test-SyncProcessing");
            });
            Assert.That(output.result, Is.EquivalentTo(TestData.Processing.Objects));
        }

        [Test]
        public void WriteAll()
        {
            var context = new PsCommandContext();
            var output = RunCommand(ps =>
            {
                ps.AddCommand("Test-WriteAll");
                ps.AddParameter("Verbose");
                ps.AddParameter("Debug");
            }, context);

            Assert.That(output.result, Is.EquivalentTo(TestData.Objects));

            Assert.That(context.Lines, Is.Empty); // without out-default results don't go to the console
            Assert.That(context.DebugLines, Has.One.EqualTo(TestData.Debug));
            Assert.That(context.WarningLines, Has.One.EqualTo(TestData.Warning));
            Assert.That(context.VerboseLines, Has.One.EqualTo(TestData.Verbose));

            Assert.That(context.ErrorLines, Is.Empty); // without out-default errors don't go to the console
            Assert.That(context.ProgressRecords, Has.One.EqualTo(TestData.ProgressRecord));

            Assert.That(output.powershell.Streams.Error, Has.One.Matches(stringMatcher(TestData.ErrorRecord)));
            Assert.That(output.powershell.Streams.Debug, Has.One.Matches(stringMatcher(TestData.DebugRecord)));
            Assert.That(output.powershell.Streams.Progress, Has.One.Matches(stringMatcher(TestData.ProgressRecord)));
            Assert.That(output.powershell.Streams.Warning, Has.One.Matches(stringMatcher(TestData.WarningRecord)));
            Assert.That(output.powershell.Streams.Information, Has.One.Matches(stringMatcher(TestData.InformationRecord)));

        }

        [Test]
        public void SynchronizationContext()
        {
            var context = new PsCommandContext();
            var output = RunCommand(ps => ps.AddCommand("Test-SynchronisationContext"), context).result;

            Assert.That(output.Count, Is.EqualTo(2));

            var initialProcessId = output[0];
            var finalProcessId = output[1];

            Assert.That(finalProcessId.ToString(), Is.EqualTo(initialProcessId.ToString()));
        }

        [Test, Repeat(5)]
        public void Cancellation()
        {
            TestCancellation.Started.Reset();
            var context = new PsCommandContext();
            var output = RunCommand(ps =>
            {
                ps.AddCommand("Test-Cancellation");
                ThreadPool.QueueUserWorkItem(async _ =>
                {
                    TestCancellation.Started.Wait();
                    await Task.Delay(TimeSpan.FromMilliseconds(50));
                    ps.Stop();
                });
            }, context);

            Assert.That(output.powershell.InvocationStateInfo.Reason, Is.InstanceOf<PipelineStoppedException>());
        }

        [Test, Repeat(5)]
        public void CancellationCooperative()
        {
            TestCancellationCooperative.Started.Reset();
            var context = new PsCommandContext();
            var output = RunCommand(ps =>
            {
                ps.AddCommand("Test-CancellationCooperative");
                ThreadPool.QueueUserWorkItem(async _ =>
                {
                    TestCancellationCooperative.Started.Wait();
                    await Task.Delay(TimeSpan.FromMilliseconds(50));
                    ps.Stop();
                });
            }, context);

            Assert.That(output.powershell.InvocationStateInfo.Reason, Is.InstanceOf<PipelineStoppedException>());
        }

        [Test]
        public void Switches()
        {
            var context = new PsCommandContext();
            var output = RunCommand(ps => ps.AddCommand("Test-Switches"), context);
            output.powershell.Stop();

            Assert.That(output.result, Has.One.EqualTo(TestData.ShouldProcessSwitch));
        }

        [Test]
        public void Exception()
        {
            var context = new PsCommandContext();
            var exception = Assert.Throws<CmdletInvocationException>(() =>
            {
                RunCommand(ps => ps.AddCommand("Test-Exception"), context);
            });

            Assert.That(exception, Has.InnerException.EqualTo(TestData.InvocationException.InnerException));
        }
    }

    [Cmdlet("Test", "WriteObject")]
    public class TestWriteObject : AsyncCmdlet
    {

        protected override Task ProcessRecordAsync()
        {
            this.WriteObject(TestData.Objects[0]);
            return Task.Run(() =>
            {
                this.WriteObject(TestData.Objects.Skip(1).ToArray(), true);
            });
        }
    }


    [Cmdlet("Test", "PropertyAccess")]
    public class TestPropertyAccess : AsyncCmdlet
    {
        protected override Task ProcessRecordAsync()
        {
            return Task.Run(() =>
            {
                var commandOrigin = this.CommandOrigin;
                var commandRuntime = this.CommandRuntime;
                var events = this.Events;
                ProviderInfo pi;
                var psp = this.GetResolvedProviderPathFromPSPath(@"/", out pi);
                var pathInfo = this.CurrentProviderLocation(pi.Name);
                var psp2 = this.GetUnresolvedProviderPathFromPSPath(@"/");
                var varErr = this.GetVariableValue("$error");
                var varErr2 = this.GetVariableValue("$error", "default");
                var host = this.Host;
                var invokeCommand = this.InvokeCommand;
                var invokeProvider = this.InvokeProvider;
                var jobRepository = this.JobRepository;
                var myInvoke = this.MyInvocation;
                var parameterSetName = this.ParameterSetName;
                var sessionState = this.SessionState;
                var stopping = this.Stopping;
                var transactionAvailable = this.TransactionAvailable();
            });
        }
    }


    [Cmdlet("Test", "SyncProcessing")]
    public class TestSyncProcessing : AsyncCmdlet
    {
        [Parameter(ValueFromPipeline = true, Mandatory = true)]
        public object? Item { get; set; }

        protected override Task BeginProcessingAsync()
        {
            this.WriteObject(TestData.Processing.Begin);
            return base.BeginProcessingAsync();
        }

        protected override Task EndProcessingAsync()
        {
            this.WriteObject(TestData.Processing.End);
            return base.EndProcessingAsync();
        }

        protected override Task StopProcessingAsync()
        {
            this.WriteObject(TestData.Processing.Stop);
            return base.StopProcessingAsync();
        }

        protected override Task ProcessRecordAsync()
        {
            this.WriteObject(TestData.Processing.Record);
            return base.ProcessRecordAsync();
        }
    }


    [Cmdlet("Test", "WriteAll")]
    public class TestWriteAll : AsyncCmdlet
    {
        protected override Task ProcessRecordAsync()
        {
            return Task.Run(() =>
            {
                this.WriteCommandDetail(TestData.CommandDetail);
                this.WriteDebug(TestData.Debug);
                this.WriteError(TestData.ErrorRecord);
                this.WriteObject(TestData.Objects[0]);
                this.WriteObject(TestData.Objects.Skip(1).ToArray(), true);
                this.WriteProgress(TestData.ProgressRecord);
                this.WriteVerbose(TestData.Verbose);
                this.WriteWarning(TestData.Warning);
                this.WriteInformation(TestData.InformationRecord);
            });
        }
    }


    [Cmdlet("Test", "SynchronisationContext")]
    public class TestSynchronisationContext : AsyncCmdlet
    {
        protected override async Task ProcessRecordAsync()
        {
            this.WriteObject(Thread.CurrentThread.ManagedThreadId);

            await Task.Delay(1);

            this.WriteObject(Thread.CurrentThread.ManagedThreadId);
        }
    }


    [Cmdlet("Test", "Cancellation")]
    public class TestCancellation : AsyncCmdlet
    {
        public static readonly ManualResetEventSlim Started = new();
        protected override async Task ProcessRecordAsync()
        {
            Started.Set();
            // we have to allow the state machine to run the next enumerator
            // because cancelling of Task is implicitly cooperative.
            while (true)
                await Task.Delay(1);
        }
    }


    [Cmdlet("Test", "CancellationCooperative")]
    public class TestCancellationCooperative : AsyncCmdlet
    {
        public static readonly ManualResetEventSlim Started = new();
        protected override async Task ProcessRecordAsync(CancellationToken cancellationToken)
        {
            Started.Set();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }


    [Cmdlet("Test", "Switches")]
    public class TestSwitches : AsyncCmdlet
    {
        protected override async Task ProcessRecordAsync()
        {
            if (this.ShouldProcess(TestData.ShouldProcessSwitch))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1));
                this.WriteObject(TestData.ShouldProcessSwitch);
            }
        }
    }


    [Cmdlet("Test", "Exception")]
    public class TestException : AsyncCmdlet
    {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        protected override async Task ProcessRecordAsync()
        {
            throw TestData.InvocationException.InnerException!;
        }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }


    public class TestData
    {
        public class Processing
        {
            public static readonly string Begin = "BeginProcessingAsync";
            public static readonly string End = "EndProcessingAsync";
            public static readonly string Stop = "StopProcessingAsync";
            public static readonly string Record = "ProcessRecordAsync";
            public static readonly string[] Objects = [Begin, Record, End];
        }

        public static readonly string CommandDetail = "WriteCommandDetail";
        public static readonly string Debug = "WriteDebug";
        public static readonly string Verbose = "WriteVerbose";
        public static readonly string Warning = "WriteWarning";
        public static readonly string[] Objects = ["WriteObject00", "WriteObject01", "WriteObject02", "WriteObject03"];

        public static readonly ErrorRecord ErrorRecord = new(new Exception(), "errorId", ErrorCategory.SyntaxError, "targetObject");
        public static readonly ProgressRecord ProgressRecord = new(0, "activity", "statusDescription");
        public static readonly DebugRecord DebugRecord = new(Debug);
        public static readonly WarningRecord WarningRecord = new(Warning);
        public static readonly InformationRecord InformationRecord = new("messageData", "source");

        public static readonly string ShouldProcessSwitch = "ShouldProcessSwitch";

        public static readonly CmdletInvocationException InvocationException = new(String.Empty, new Exception());
    }

}