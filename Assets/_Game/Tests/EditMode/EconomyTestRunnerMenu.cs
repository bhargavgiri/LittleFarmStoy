using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace LittleFarmStory.Tests
{
    /// <summary>
    /// Runs the EditMode economy tests inside the Editor that is already open, and writes the
    /// results where they can be read without the Test Runner window.
    ///
    /// EditMode tests need neither Play mode nor a second Unity instance, so the project lock
    /// held by the open Editor does not block them. The file exists so results are evidence
    /// that can be checked afterwards, not something only visible in a window.
    /// </summary>
    public static class EconomyTestRunnerMenu
    {
        private const string AssemblyName = "LittleFarmStory.Tests.EditMode";
        private const string ResultsPath = "Documentation/ECONOMY_TEST_RESULTS.md";

        [MenuItem("Little Farm Story/Run Economy Tests", false, 41)]
        public static void Run()
        {
            TestRunnerApi api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new ResultWriter(api));

            Debug.Log("Little Farm Story: running economy EditMode tests...");

            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                assemblyNames = new[] { AssemblyName }
            }));
        }

        private class ResultWriter : ICallbacks
        {
            private readonly TestRunnerApi api;
            private readonly List<ITestResultAdaptor> leaves = new List<ITestResultAdaptor>();

            public ResultWriter(TestRunnerApi owner)
            {
                api = owner;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
                leaves.Clear();
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.Test != null && !result.Test.IsSuite)
                {
                    leaves.Add(result);
                }
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                int passed = 0;
                int failed = 0;
                int other = 0;

                StringBuilder body = new StringBuilder();

                for (int i = 0; i < leaves.Count; i++)
                {
                    ITestResultAdaptor leaf = leaves[i];
                    string verdict;

                    switch (leaf.TestStatus)
                    {
                        case TestStatus.Passed:
                            passed++;
                            verdict = "PASS";
                            break;

                        case TestStatus.Failed:
                            failed++;
                            verdict = "FAIL";
                            break;

                        default:
                            other++;
                            verdict = leaf.TestStatus.ToString().ToUpperInvariant();
                            break;
                    }

                    body.AppendLine("- **" + verdict + "** `" + leaf.Name + "` (" +
                                    (leaf.Duration * 1000.0).ToString("0") + " ms)");

                    if (leaf.TestStatus != TestStatus.Passed && !string.IsNullOrEmpty(leaf.Message))
                    {
                        body.AppendLine("  - " + leaf.Message.Trim().Replace("\n", "\n    "));
                    }
                }

                StringBuilder report = new StringBuilder();
                report.AppendLine("# Economy Test Results");
                report.AppendLine();
                report.AppendLine("Generated " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                                  " by `Little Farm Story/Run Economy Tests`.");
                report.AppendLine("Executed by the Unity Test Framework inside the running Editor.");
                report.AppendLine();
                report.AppendLine("**" + passed + " passed, " + failed + " failed, " + other +
                                  " other, " + leaves.Count + " total.**");
                report.AppendLine();
                report.Append(body);

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(ResultsPath));
                    File.WriteAllText(ResultsPath, report.ToString(), Encoding.UTF8);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Little Farm Story: could not write " + ResultsPath + ": " + e.Message);
                }

                string summary = "Little Farm Story: economy tests - " + passed + " passed, " +
                                 failed + " failed, " + other + " other. Results in " + ResultsPath;

                if (failed > 0 || leaves.Count == 0)
                {
                    Debug.LogError(summary);
                }
                else
                {
                    Debug.Log(summary);
                }

                api.UnregisterCallbacks(this);
            }
        }
    }
}
