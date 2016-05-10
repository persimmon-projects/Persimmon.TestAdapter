using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using Persimmon.VisualStudio.TestRunner;
using Persimmon.VisualStudio.TestExplorer.Sinks;

namespace Persimmon.VisualStudio.TestExplorer
{
    [FileExtension(".dll")]
    [ExtensionUri(Constant.ExtensionUriString)]
    [DefaultExecutorUri(Constant.ExtensionUriString)]
    public sealed class TestAdapter : ITestDiscoverer, ITestExecutor
    {
        #region Fields
        private static readonly object lock_ = new object();
        private static bool ready_;

        private readonly Version version_ = typeof(TestAdapter).Assembly.GetName().Version;
        private readonly ConcurrentQueue<CancellationTokenSource> cancellationTokens_ =
            new ConcurrentQueue<CancellationTokenSource>();
        #endregion

        #region WaitingForAttachDebugger
        [Conditional("DEBUG")]
        private void WaitingForAttachDebugger()
        {
            lock (lock_)
            {
                if (ready_ == false)
                {
                    NativeMethods.MessageBox(
                        IntPtr.Zero,
                        "Waiting for attach debugger ...",
                        string.Format("Persimmon ({0})", Process.GetCurrentProcess().Id),
                        NativeMethods.MessageBoxOptions.IconWarning | NativeMethods.MessageBoxOptions.OkOnly);
                    ready_ = true;
                }
            }
        }
        #endregion

        #region DiscoverTests
        private async Task DiscoverTestsAsync(
            IEnumerable<string> sources,
            IDiscoveryContext discoveryContext,
            IMessageLogger logger,
            ITestCaseDiscoverySink discoverySink)
        {
            logger.SendMessage(
                TestMessageLevel.Informational,
                string.Format("Persimmon Test Explorer {0} discovering tests started", version_));

            try
            {
                var testExecutor = new TestExecutor();
                var sink = new TestDiscoverySink(discoveryContext, logger, discoverySink);

#if false
                foreach (var task in sources.Select(
                    targetAssemblyPath => testExecutor.DiscoverAsync(targetAssemblyPath, sink)))
                {
                    await task;
                }
#else
                await Task.WhenAll(sources.Select(
                    targetAssemblyPath => testExecutor.DiscoverAsync(targetAssemblyPath, sink)));
#endif
            }
            catch (Exception ex)
            {
                logger.SendMessage(
                    TestMessageLevel.Error,
                    ex.ToString());
            }
            finally
            {
                logger.SendMessage(
                    TestMessageLevel.Informational,
                    string.Format("Persimmon Test Explorer {0} discovering tests finished", version_));
            }
        }

        public void DiscoverTests(
            IEnumerable<string> sources,
            IDiscoveryContext discoveryContext,
            IMessageLogger logger,
            ITestCaseDiscoverySink discoverySink)
        {
            this.WaitingForAttachDebugger();

            this.DiscoverTestsAsync(sources, discoveryContext, logger, discoverySink).Wait();
        }
        #endregion

        #region RunTests (Overall)
        private async Task RunTestsAsync(
            IEnumerable<string> sources,
            IRunContext runContext,
            IFrameworkHandle frameworkHandle)
        {
            this.WaitingForAttachDebugger();

            frameworkHandle.SendMessage(
                TestMessageLevel.Informational,
                string.Format("Persimmon Test Explorer {0} run tests started", version_));
            try
            {
                var testExecutor = new TestExecutor();
                var sink = new TestRunSink(runContext, frameworkHandle);

                // Register cancellation token.
                var cts = new CancellationTokenSource();
                cancellationTokens_.Enqueue(cts);

                // Start tests.
                await Task.WhenAll(sources.Select(targetAssemblyPath =>
                    testExecutor.RunAsync(targetAssemblyPath, new TestCase[0], sink, cts.Token)));
            }
            catch (Exception ex)
            {
                frameworkHandle.SendMessage(
                    TestMessageLevel.Error,
                    ex.ToString());
            }
            finally
            {
                frameworkHandle.SendMessage(
                    TestMessageLevel.Informational,
                    string.Format("Persimmon Test Explorer {0} run tests finished", version_));
            }
        }

        public void RunTests(
            IEnumerable<string> sources,
            IRunContext runContext,
            IFrameworkHandle frameworkHandle)
        {
            this.WaitingForAttachDebugger();

            this.RunTestsAsync(sources, runContext, frameworkHandle).Wait();
        }
        #endregion

        #region RunTests
        private async Task RunTestsAsync(
            IEnumerable<TestCase> tests,
            IRunContext runContext,
            IFrameworkHandle frameworkHandle)
        {
            frameworkHandle.SendMessage(
                TestMessageLevel.Informational,
                string.Format("Persimmon Test Explorer {0} run tests started", version_));

            try
            {
                var testExecutor = new TestExecutor();
                var sink = new TestRunSink(runContext, frameworkHandle);

                // Register cancellation token.
                var cts = new CancellationTokenSource();
                cancellationTokens_.Enqueue(cts);

                // Start tests.
                await Task.WhenAll(tests.GroupBy(testCase => testCase.Source).
                    Select(g => testExecutor.RunAsync(g.Key, g.ToArray(), sink, cts.Token)));
            }
            catch (Exception ex)
            {
                frameworkHandle.SendMessage(
                    TestMessageLevel.Error,
                    ex.ToString());
            }
            finally
            {
                frameworkHandle.SendMessage(
                    TestMessageLevel.Informational,
                    string.Format("Persimmon Test Explorer {0} run tests finished", version_));
            }
        }

        public void RunTests(
            IEnumerable<TestCase> tests,
            IRunContext runContext,
            IFrameworkHandle frameworkHandle)
        {
            this.WaitingForAttachDebugger();

            this.RunTestsAsync(tests, runContext, frameworkHandle).Wait();
        }
        #endregion

        #region Cancel
        public void Cancel()
        {
            // Cancel all tasks.
            foreach (var cts in cancellationTokens_)
            {
                cts.Cancel();
            }
        }
        #endregion
    }
}
