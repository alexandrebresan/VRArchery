using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace VRArchery.Editor.Stage2
{
    [InitializeOnLoad]
    public static class Stage2Tools
    {
        public const string Reports = "Logs/VRArcheryStage2";
        public const string Requests = "Library/VRArcheryStage2";
        static readonly TestRunnerApi Runner;
        static double nextPoll;

        static Stage2Tools()
        {
            Runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            Runner.RegisterCallbacks(new Results());
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
                SessionState.GetBool("VRArchery.Stage2.TestsRunning", false)) return;
            nextPoll = EditorApplication.timeSinceStartup + 0.5;
            var repairPath = Requests + "/repair.request";
            if (File.Exists(repairPath))
            {
                AssetDatabase.Refresh();
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                File.Delete(repairPath);
                try { Stage2StringRepair.Apply(); }
                catch (Exception error)
                {
                    Directory.CreateDirectory(Reports);
                    File.WriteAllText(Reports + "/repair-error.txt", error.ToString());
                    Debug.LogException(error);
                }
                return;
            }
            var setupPath = Requests + "/setup.request";
            if (File.Exists(setupPath))
            {
                File.Delete(setupPath);
                try { Stage2SceneSetup.Create(); }
                catch (Exception error)
                {
                    Directory.CreateDirectory(Reports);
                    File.WriteAllText(Reports + "/setup-error.txt", error.ToString());
                    Debug.LogException(error);
                }
                return;
            }
            var path = Requests + "/tests.request";
            if (!File.Exists(path)) return;
            AssetDatabase.Refresh();
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            var label = File.ReadAllText(path).Trim();
            File.Delete(path);
            Run(label);
        }

        [MenuItem("VRArchery/Etapa 2/Testes Edit Mode")]
        public static void EditTests() => Run("edit-green");
        [MenuItem("VRArchery/Etapa 2/Testes Play Mode")]
        public static void PlayTests() => Run("play-green");

        static void Run(string label)
        {
            Directory.CreateDirectory(Reports);
            SessionState.SetString("VRArchery.Stage2.TestLabel", label);
            bool play = label.StartsWith("play", StringComparison.Ordinal);
            Runner.Execute(new ExecutionSettings(new Filter
            {
                testMode = play ? TestMode.PlayMode : TestMode.EditMode,
                assemblyNames = new[] { play ? "VRArchery.PlayModeTests" : "VRArchery.EditModeTests" }
            }));
        }

        sealed class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) => SessionState.SetBool("VRArchery.Stage2.TestsRunning", true);
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                SessionState.SetBool("VRArchery.Stage2.TestsRunning", false);
                var label = SessionState.GetString("VRArchery.Stage2.TestLabel", "tests");
                Directory.CreateDirectory(Reports);
                TestRunnerApi.SaveResultToFile(result, Reports + "/" + label + ".xml");
                File.WriteAllText(Reports + "/" + label + ".txt",
                    $"Result: {result.TestStatus}\nPassed: {result.PassCount}\nFailed: {result.FailCount}\nSkipped: {result.SkipCount}\n");
                Debug.Log($"[VRArchery Stage2] {label}: {result.PassCount} passed, {result.FailCount} failed.");
            }
        }
    }
}
