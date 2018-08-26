﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
#if NETCORE
using System.Runtime.Loader;
#else
using System.Security.Policy;
#endif
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Persimmon.TestRunner.Internals;

namespace Persimmon.TestRunner
{
    /// <summary>
    /// Test assembly load/execute facade.
    /// </summary>
    public sealed class TestExecutor
    {
        private readonly string testRunnerAssemblyPath_;
        private readonly string testDiscovererPath_;
        private static readonly string testDiscovererTypeName_ =
            "Persimmon.TestDiscoverer.Discoverer";

        /// <summary>
        /// Constructor.
        /// </summary>
        public TestExecutor()
        {

#if !NETCORE
            var callerAssembly = Assembly.GetCallingAssembly();
#endif

            testRunnerAssemblyPath_ =
                this.GetType()
#if NETCORE
                    .GetTypeInfo()
#endif
                    .Assembly.Location;

            testDiscovererPath_ = Path.Combine(
                Path.GetDirectoryName(
#if NETCORE
                    testRunnerAssemblyPath_
#else
                    callerAssembly.Location
#endif
                ),
                "Persimmon.TestDiscoverer.dll");
        }

        /// <summary>
        /// Test execute target assembly.
        /// </summary>
        /// <typeparam name="T">Target instance castable type (MBR or interface)</typeparam>
        /// <typeparam name="U">Result type</typeparam>
        /// <param name="targetAssemblyPath">Target instantiate assembly path</param>
        /// <param name="targetClassName">Targe instantiate class name (MBR derived)</param>
        /// <param name="applicationAssemblyPath">Target application assembly path (Inclusive in ApplicationBase path)</param>
        /// <param name="action">Action</param>
        /// <returns>Result</returns>
        private Task<U> InternalExecuteAsync<T, U>(
            string targetAssemblyPath,
            string targetClassName,
            string applicationAssemblyPath,
            Func<T, U> action)
            where T : class
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(targetAssemblyPath));
            Debug.Assert(!string.IsNullOrWhiteSpace(targetClassName));
            Debug.Assert(!string.IsNullOrWhiteSpace(applicationAssemblyPath));
            Debug.Assert(action != null);

            return Task.Run(() =>
            {
#if NETCORE
                var type = Assembly.Load(new AssemblyName(Path.GetFileNameWithoutExtension(targetAssemblyPath))).GetType(targetClassName);
                var targetInstance = (T)Activator.CreateInstance(type);
                return action(targetInstance);
#else
                // Strategy: Shadow copy information:
                //   https://msdn.microsoft.com/en-us/library/ms404279%28v=vs.110%29.aspx

                // ApplicationBasePath: Important:
                //   Change from current AppDomain.ApplicationBase,
                //   may be stable execution test assemblies.

                // Execution context id (for diagnose).
                var contextId = Guid.NewGuid();

                var applicationBasePath = Path.GetDirectoryName(applicationAssemblyPath);

                // Shadow copy target paths.
                var shadowCopyTargets = string.Join(
                    ";",
                    new[]
                    {
                        Path.GetDirectoryName(targetAssemblyPath),
                        applicationBasePath
                    }.Distinct());

                // AppDomain name.
                var separatedAppDomainName = string.Format(
                    "{0}-{1}",
                    this.GetType().FullName,
                    contextId);

                // AppDomainSetup informations.
                var separatedAppDomainSetup = new AppDomainSetup
                {
                    ApplicationName = separatedAppDomainName,
                    ApplicationBase = applicationBasePath,
                    ShadowCopyFiles = "true",
                    ShadowCopyDirectories = shadowCopyTargets
                };

                // If test assembly has configuration file, try to set.
                var configurationFilePath = applicationAssemblyPath + ".config";
                if (File.Exists(configurationFilePath))
                {
                    Debug.WriteLine(string.Format(
                        "Persimmon test runner: Try to set configuration file: Path={0}", configurationFilePath));

                    separatedAppDomainSetup.ConfigurationFile = configurationFilePath;
                }

                // Derived current evidence.
                // (vstest agent may be full trust...)
                var separatedAppDomainEvidence = new Evidence(AppDomain.CurrentDomain.Evidence);

                // Create AppDomain.
                var separatedAppDomain = AppDomain.CreateDomain(
                    separatedAppDomainName,
                    separatedAppDomainEvidence,
                    separatedAppDomainSetup);

                try
                {
                    // Create remote object (MBR) instance into new AppDomain,
                    //   and get remote reference.
                    var targetInstance = (T)separatedAppDomain.CreateInstanceFromAndUnwrap(
                        targetAssemblyPath,
                        targetClassName);

                    ///////////////////////////////////////////////////////////////////////////////////////////
                    // Execute via remote AppDomain

                    return action(targetInstance);
                }
                finally
                {
                    // Discard AppDomain.
                    AppDomain.Unload(separatedAppDomain);
                }
#endif
            });
        }

        /// <summary>
        /// Test execute target assembly.
        /// </summary>
        /// <param name="targetAssemblyPath">Target assembly path</param>
        /// <param name="sink">Execution logger interface</param>
        public async Task DiscoverAsync(
            string targetAssemblyPath,
            ITestDiscoverSink sink)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(targetAssemblyPath));
            Debug.Assert(sink != null);

            try
            {
                // FIXME: discoverer can not execute... Please update FSharp.Core.Service
                // Step1: Parse F# source code and discover AST tree, retreive symbol informations.
                //var symbols = await this.InternalExecuteAsync<IDiscoverer, SymbolInformation[]>(
                //    testDiscovererPath_,
                //    testDiscovererTypeName_,
                //    testDiscovererPath_,
                //    discoverer => discoverer.Discover(targetAssemblyPath));
#if NETCORE
                var symbols = new SymbolInformationDiscoverer(targetAssemblyPath)
                    .Discover();

                // Take last item, most deepest information.
                var grouped = symbols
#else
                var grouped = new SymbolInformation[] { }
#endif
                    .GroupBy(symbol => symbol.SymbolName)
#if DEBUG
                    .ToArray()
#endif
                    ;

                var symbolDictionary = grouped.
                    ToDictionary(g => g.Key, g => g.Last());

#if DEBUG
                foreach (var g in grouped.Where(g => g.Count() >= 2))
                {
                    Debug.WriteLine(string.Format(
                        "Discover: Duplicate symbol: SymbolName={0}, Entries=[{1}]",
                        g.Key,
                        string.Join(",", g.Select(si => string.Format("{0}({1},{2})", Path.GetFileName(si.FileName), si.MinLineNumber, si.MinColumnNumber)))));
                }

                var fileName = Path.GetFileName(targetAssemblyPath);
                foreach (var entry in symbolDictionary)
                {
                    Debug.WriteLine(string.Format(
                        "Discover: FileName={0}, SymbolName={1}, Position={2},{3}",
                        fileName,
                        entry.Key,
                        entry.Value.MinLineNumber,
                        entry.Value.MinColumnNumber));
                }
#endif

                // Step2: Traverse target test assembly, retreive test cases and push to Visual Studio.
                await this.InternalExecuteAsync<RemotableTestExecutor, bool>(
                    testRunnerAssemblyPath_,
                    typeof(RemotableTestExecutor).FullName,
                    targetAssemblyPath,
                    executor =>
                    {
                        executor.Discover(
                            targetAssemblyPath,
                            new DiscoverSinkTrampoline(targetAssemblyPath, sink, symbolDictionary));
                        return true;
                    });
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
                sink.Message(true, ex.Message);
            }
        }

        /// <summary>
        /// Test execute target assembly.
        /// </summary>
        /// <param name="targetAssemblyPath">Target assembly path</param>
        /// <param name="testCases">Target test cases.</param>
        /// <param name="sink">Execution logger interface</param>
        /// <param name="token">CancellationToken</param>
        public async Task RunAsync(
            string targetAssemblyPath,
            ICollection<TestCase> testCases,
            ITestRunSink sink,
            CancellationToken token)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(targetAssemblyPath));
            Debug.Assert(testCases != null);
            Debug.Assert(sink != null);
            Debug.Assert(token != null);

            try
            {
                var fullyQualifiedTestNames = testCases.Select(testCase => testCase.FullyQualifiedName).ToArray();
                var testCaseDicts = testCases.ToDictionary(testCase => testCase.FullyQualifiedName);

                await this.InternalExecuteAsync<RemotableTestExecutor, bool>(
                    testRunnerAssemblyPath_,
                    typeof(RemotableTestExecutor).FullName,
                    targetAssemblyPath,
                    executor =>
                    {
                        executor.Run(
                            targetAssemblyPath,
                            fullyQualifiedTestNames,
                            new RunSinkTrampoline(targetAssemblyPath, sink, testCaseDicts),
                            token);
                        return true;
                    });
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
                sink.Message(true, ex.Message);
            }
        }
    }
}
