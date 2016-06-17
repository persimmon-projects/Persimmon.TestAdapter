using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.Win32;
using Persimmon.VisualStudio.TestRunner;
using Persimmon.VisualStudio.TestExplorer.Sinks;

namespace Persimmon.VisualStudio.TestExplorer
{
    /// <summary>
    /// Persimmon test explorer adapter class.
    /// </summary>
    [FileExtension(".dll")]
    [ExtensionUri(Constant.ExtensionUriString)]
    [DefaultExecutorUri(Constant.ExtensionUriString)]
    public sealed class TestAdapter : ITestDiscoverer, ITestExecutor
    {
        #region Fields
        private static readonly HashSet<string> excludeAssemblies_ =
            new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
            {
                "Persimmon", "Persimmon.Runner", "Persimmon.Console", "Persimmon.Script", "Persimmon.Sample"
            };
        private static readonly object lock_ = new object();
        private static bool ready_;

        private readonly Version version_ = typeof(TestAdapter).Assembly.GetName().Version;
        private readonly ConcurrentQueue<CancellationTokenSource> cancellationTokens_ =
            new ConcurrentQueue<CancellationTokenSource>();
        #endregion

        #region WaitingForAttachDebuggerIfRequired
        private static bool IsDebuggable(RegistryView view)
        {
            using (var hklmKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
            {
                var subKey = hklmKey.OpenSubKey(@"SOFTWARE\Persimmon\VisualStudio.TestExplorer", false);
                if (subKey != null)
                {
                    using (var persimmonKey = subKey)
                    {
                        try
                        {
                            var value = Convert.ToInt32(persimmonKey.GetValue("Debug"));
                            return value != 0;
                        }
                        catch
                        {
                        }
                    }
                }
            }

#if DEBUG
            return true;
#else
            return false;
#endif
        }

        private void WaitingForAttachDebuggerIfRequired()
        {
            lock (lock_)
            {
                if (ready_ == false)
                {
                    var isDebuggable =
                        IsDebuggable(RegistryView.Registry64) |
                        IsDebuggable(RegistryView.Registry32);

                    if (isDebuggable == true)
                    {
                        NativeMethods.MessageBox(
                            IntPtr.Zero,
                            "Waiting for attach debugger ...",
                            string.Format("Persimmon ({0})", Process.GetCurrentProcess().Id),
                            NativeMethods.MessageBoxOptions.IconWarning | NativeMethods.MessageBoxOptions.OkOnly);
                    }

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

                var filteredSources =
                    sources.Where(path => !excludeAssemblies_.Contains(Path.GetFileNameWithoutExtension(path)));

#if false
                foreach (var task in filteredSources.Select(
                    targetAssemblyPath => testExecutor.DiscoverAsync(targetAssemblyPath, sink)))
                {
                    await task;
                }
#else
                await Task.WhenAll(filteredSources.Select(
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

        /// <summary>
        /// Discovery test definitions.
        /// </summary>
        /// <param name="sources">Source assembly paths.</param>
        /// <param name="discoveryContext">Using this information context.</param>
        /// <param name="logger">Output logger (connected to debugger view).</param>
        /// <param name="discoverySink">Results sink interface.</param>
        public void DiscoverTests(
            IEnumerable<string> sources,
            IDiscoveryContext discoveryContext,
            IMessageLogger logger,
            ITestCaseDiscoverySink discoverySink)
        {
            this.WaitingForAttachDebuggerIfRequired();

            this.DiscoverTestsAsync(sources, discoveryContext, logger, discoverySink).Wait();
        }
        #endregion

        #region RunTests (Overall)
        private async Task RunTestsAsync(
            IEnumerable<string> sources,
            IRunContext runContext,
            IFrameworkHandle frameworkHandle)
        {
            this.WaitingForAttachDebuggerIfRequired();

            frameworkHandle.SendMessage(
                TestMessageLevel.Informational,
                string.Format("Persimmon Test Explorer {0} run tests started", version_));
            try
            {
                var testExecutor = new TestExecutor();
                var sink = new TestRunSink(runContext, frameworkHandle);

                var filteredSources =
                    sources.Where(path => !excludeAssemblies_.Contains(Path.GetFileNameWithoutExtension(path)));

                // Register cancellation token.
                var cts = new CancellationTokenSource();
                cancellationTokens_.Enqueue(cts);

                // Start tests.
                await Task.WhenAll(filteredSources.Select(targetAssemblyPath =>
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

        /// <summary>
        /// Execute tests.
        /// </summary>
        /// <param name="sources">Source assembly paths.</param>
        /// <param name="runContext">Using this execution context.</param>
        /// <param name="frameworkHandle">Testing framework handler.</param>
        public void RunTests(
            IEnumerable<string> sources,
            IRunContext runContext,
            IFrameworkHandle frameworkHandle)
        {
            this.WaitingForAttachDebuggerIfRequired();

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

        /// <summary>
        /// Execute tests.
        /// </summary>
        /// <param name="tests">Target test cases.</param>
        /// <param name="runContext">Using this execution context.</param>
        /// <param name="frameworkHandle">Testing framework handler.</param>
        public void RunTests(
            IEnumerable<TestCase> tests,
            IRunContext runContext,
            IFrameworkHandle frameworkHandle)
        {
            this.WaitingForAttachDebuggerIfRequired();

            this.RunTestsAsync(tests, runContext, frameworkHandle).Wait();
        }
        #endregion

        #region Cancel
        /// <summary>
        /// Cancel request for current executing tests (RunTests).
        /// </summary>
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
