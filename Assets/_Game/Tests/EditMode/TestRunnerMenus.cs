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
    /// Runs the EditMode suites inside the Editor that is already open, and writes each one's
    /// results where they can be read without the Test Runner window.
    ///
    /// EditMode tests need neither Play mode nor a second Unity instance, so the project lock
    /// held by the open Editor does not block them. The files exist so results are evidence
    /// that can be checked afterwards, not something only visible in a window.
    ///
    /// Each menu item filters to ONE test class rather than to the assembly, because both
    /// suites live in the same assembly and an assembly-wide filter would quietly write another
    /// suite's results into this one's file.
    /// </summary>
    public static class TestRunnerMenus
    {
        private const string AssemblyName = "LittleFarmStory.Tests.EditMode";

        [MenuItem("Little Farm Story/Run Economy Tests", false, 41)]
        public static void RunEconomyTests()
        {
            Run("economy", "LittleFarmStory.Tests.EconomyTests",
                "Documentation/ECONOMY_TEST_RESULTS.md", "Run Economy Tests");
        }

        [MenuItem("Little Farm Story/Run Persistence Tests", false, 43)]
        public static void RunPersistenceTests()
        {
            Run("persistence", "LittleFarmStory.Tests.PersistenceTests",
                "Documentation/PERSISTENCE_TEST_RESULTS.md", "Run Persistence Tests");
        }

        private static void Run(string label, string testClass, string resultsPath, string menuName)
        {
            TestRunnerApi api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new ResultWriter(api, label, resultsPath, menuName));

            Debug.Log("Little Farm Story: running " + label + " EditMode tests...");

            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                assemblyNames = new[] { AssemblyName },
                groupNames = new[] { "^" + Regex(testClass) }
            }));
        }

        /// <summary>Escapes the dots in a namespace so the group filter matches it literally.</summary>
        private static string Regex(string testClass)
        {
            return testClass.Replace(".", "\\.");
        }

        private class ResultWriter : ICallbacks
        {
            private readonly TestRunnerApi api;
            private readonly string label;
            private readonly string resultsPath;
            private readonly string menuName;
            private readonly List<ITestResultAdaptor> leaves = new List<ITestResultAdaptor>();

            public ResultWriter(TestRunnerApi owner, string suiteLabel, string path, string menu)
            {
                api = owner;
                label = suiteLabel;
                resultsPath = path;
                menuName = menu;
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
                report.AppendLine("# " + char.ToUpperInvariant(label[0]) + label.Substring(1) +
                                  " Test Results");
                report.AppendLine();
                report.AppendLine("Generated " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                                  " by `Little Farm Story/" + menuName + "`.");
                report.AppendLine("Executed by the Unity Test Framework inside the running Editor.");
                report.AppendLine();
                report.AppendLine("**" + passed + " passed, " + failed + " failed, " + other +
                                  " other, " + leaves.Count + " total.**");
                report.AppendLine();
                report.Append(body);

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(resultsPath));
                    File.WriteAllText(resultsPath, report.ToString(), Encoding.UTF8);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Little Farm Story: could not write " + resultsPath + ": " + e.Message);
                }

                string summary = "Little Farm Story: " + label + " tests - " + passed + " passed, " +
                                 failed + " failed, " + other + " other. Results in " + resultsPath;

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
