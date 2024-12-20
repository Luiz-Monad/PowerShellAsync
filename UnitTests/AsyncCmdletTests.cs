using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;
using TTRider.PowerShellAsync.UnitTests.Infrastructure;

namespace TTRider.PowerShellAsync.UnitTests
{
    [TestFixture]
    public class TestPsBase
    {
        private static Runspace? runspace;

        [SetUp()]
        public static void SetUp()
        {
            runspace = RunspaceFactory.CreateRunspace();
            runspace.Open();
            ImportModule();
        }

        [TearDown()]
        public static void TearDown()
        {
            runspace!.Close();
        }

        public static void ImportModule()
        {
            RunCommand(ps =>
            {
                var path = new Uri(typeof(TestWriteObject).Assembly.Location);
                ps.AddCommand("Import-Module").AddArgument(path.LocalPath);
            });
        }

        public static List<PSObject> RunCommand(Action<PowerShell> prepareAction, PsCommandContext? context = null)
        {
            var ps = PowerShell.Create();
            ps.Runspace = runspace;

            prepareAction(ps);

            var ret = new List<PSObject>();

            var settings = new PSInvocationSettings {
                Host = new TestPsHost(context ?? new PsCommandContext())
            };

            foreach (var result in ps.Invoke(new Object[0], settings))
            {
                Trace.WriteLine(result);
                ret.Add(result);
            }
            return ret;
        }

        [Test]
        public void WriteObject()
        {
            var output = RunCommand(ps => ps.AddCommand("Test-TTRiderPSAWriteObject"));
            Assert.That(string.Join("\r\n", output), Is.EqualTo("WriteObject00\r\nWriteObject01\r\nWriteObject02\r\nWriteObject03"));
        }

        [Test]
        public void PropertyAccess()
        {
            var output = RunCommand(ps => ps.AddCommand("Test-TTRiderPSAPropertyAccess"));
            Assert.That(output.Count, Is.EqualTo(0));
        }

        [Test]
        public void SyncProcessing()
        {
            var output = RunCommand(ps => ps.AddCommand("Test-TTRiderPSASyncProcessing"));
            Assert.That(string.Join("\r\n", output), Is.EqualTo("BeginProcessingAsync\r\nProcessRecordAsync\r\nEndProcessingAsync"));
        }

        [Test]
        public void WriteAll()
        {
            var context = new PsCommandContext();
            var output = RunCommand(ps =>
            {
                ps.AddCommand("Test-TTRiderPSAWriteAll");
                ps.AddParameter("Verbose");
                ps.AddParameter("Debug");
            }, context);

            Assert.That(string.Join("\r\n", output), Is.EqualTo("WriteObject00\r\nWriteObject01\r\nWriteObject02\r\nWriteObject03"));

            Assert.That(string.Join("\r\n", context.DebugLines), Is.EqualTo("WriteDebug"));

            Assert.That(string.Join("\r\n", context.WarningLines), Is.EqualTo("WriteWarning"));

            Assert.That(string.Join("\r\n", context.VerboseLines), Is.EqualTo("WriteVerbose"));

            Assert.That(context.ProgressRecords.Count, Is.EqualTo(1));

        }

        [Test]
        public void SynchronizationContext()
        {
            var context = new PsCommandContext();
            var output = RunCommand(ps => ps.AddCommand("Test-TTRiderPSSynchronisationContext"), context);

            Assert.That(output.Count, Is.EqualTo(2));

            var initialProcessId = output[0];
            var finalProcessId = output[1];

            Assert.That(finalProcessId.ToString(), Is.EqualTo(initialProcessId.ToString()));
        }
    }



    [Cmdlet("Test", "TTRiderPSAWriteObject")]
    public class TestWriteObject : AsyncCmdlet
    {
        protected override Task ProcessRecordAsync()
        {
            return Task.Run(() =>
            {
                this.WriteObject("WriteObject00");
                this.WriteObject(new[] { "WriteObject01", "WriteObject02", "WriteObject03" }, true);
            });
        }
    }


    [Cmdlet("Test", "TTRiderPSAPropertyAccess")]
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
                var psp = this.GetResolvedProviderPathFromPSPath(@"c:\", out pi);
                var pathInfo = this.CurrentProviderLocation(pi.Name);
                var psp2 = this.GetUnresolvedProviderPathFromPSPath(@"c:\");
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


    [Cmdlet("Test", "TTRiderPSASyncProcessing")]
    public class TestSyncProcessing : AsyncCmdlet
    {
        protected override Task BeginProcessingAsync()
        {
            this.WriteObject("BeginProcessingAsync");
            return base.BeginProcessingAsync();
        }

        protected override Task EndProcessingAsync()
        {
            this.WriteObject("EndProcessingAsync");
            return base.EndProcessingAsync();
        }

        protected override Task StopProcessingAsync()
        {
            this.WriteObject("StopProcessingAsync");
            return base.StopProcessingAsync();
        }

        protected override Task ProcessRecordAsync()
        {
            this.WriteObject("ProcessRecordAsync");
            return base.ProcessRecordAsync();
        }
    }


    [Cmdlet("Test", "TTRiderPSAWriteAll")]
    public class TestWriteAll : AsyncCmdlet
    {
        protected override Task ProcessRecordAsync()
        {
            return Task.Run(() =>
            {
                this.WriteCommandDetail("WriteCommandDetail");
                this.WriteDebug("WriteDebug");
                this.WriteError(new ErrorRecord(new Exception(), "errorId", ErrorCategory.SyntaxError, "targetObject"));
                this.WriteObject("WriteObject00");
                this.WriteObject(new[] { "WriteObject01", "WriteObject02", "WriteObject03" }, true);
                this.WriteProgress(new ProgressRecord(0, "activity", "statusDescription"));
                this.WriteVerbose("WriteVerbose");
                this.WriteWarning("WriteWarning");
            });
        }
    }


    [Cmdlet("Test", "TTRiderPSSynchronisationContext")]
    public class TestSynchronisationContext : AsyncCmdlet
    {
        protected override async Task ProcessRecordAsync()
        {
            this.WriteObject(Thread.CurrentThread.ManagedThreadId);

            await Task.Delay(1);

            this.WriteObject(Thread.CurrentThread.ManagedThreadId);
        }
    }
}