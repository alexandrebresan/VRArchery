using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRArchery.Editor.Stage3
{
    [InitializeOnLoad]
    public static class Stage3Tools
    {
        public const string Reports = "Logs/VRArcheryStage3";
        public const string Requests = "Library/VRArcheryStage3";
        static readonly TestRunnerApi Runner;
        static double nextPoll;
        static double previewReady;
        static bool previewRequested;

        static Stage3Tools()
        {
            Runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            Runner.RegisterCallbacks(new Results());
            EditorApplication.update += Poll;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
                SessionState.GetBool("VRArchery.Stage2.TestsRunning", false) || SessionState.GetBool("VRArchery.Stage3.Active", false)) return;
            nextPoll = EditorApplication.timeSinceStartup + 0.5;
            var preview = Requests + "/preview.request";
            if (File.Exists(preview))
            {
                File.Delete(preview);
                CapturePlayPreview();
                return;
            }
            var layout = Requests + "/layout.request";
            if (File.Exists(layout))
            {
                AssetDatabase.Refresh();
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                File.Delete(layout);
                try { Stage3SceneSetup.ArrangePanelAndValidate(); }
                catch (Exception error)
                {
                    Directory.CreateDirectory(Reports);
                    File.WriteAllText(Reports + "/scene-error.txt", error.ToString());
                    Debug.LogException(error);
                }
                return;
            }
            var setup = Requests + "/setup.request";
            if (File.Exists(setup))
            {
                AssetDatabase.Refresh();
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                File.Delete(setup);
                try { Stage3SceneSetup.Create(); }
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

        [MenuItem("VRArchery/Etapa 3/Testes Edit Mode")]
        public static void EditTests() => Run("edit-stage3-final");
        [MenuItem("VRArchery/Etapa 3/Testes Play Mode")]
        public static void PlayTests() => Run("play-stage3-final");

        [MenuItem("VRArchery/Etapa 3/Capturar preview em Play Mode")]
        public static void CapturePlayPreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Exit Play Mode and save scene changes before capturing a preview.");
            Directory.CreateDirectory(Reports);
            EditorSceneManager.OpenScene(Stage3SceneSetup.ScenePath);
            SessionState.SetBool("VRArchery.Stage3.Preview", true);
            EditorApplication.isPlaying = true;
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("VRArchery.Stage3.Preview", false))
            {
                previewReady = EditorApplication.timeSinceStartup + 2;
                previewRequested = false;
                EditorApplication.update += PreviewTick;
            }
            if (state == PlayModeStateChange.ExitingPlayMode) EditorApplication.update -= PreviewTick;
        }

        static void PreviewTick()
        {
            if (EditorApplication.timeSinceStartup < previewReady) return;
            string path = Reports + "/scene-preview.png";
            if (!previewRequested)
            {
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(path));
                previewRequested = true;
                previewReady = EditorApplication.timeSinceStartup + 1;
                return;
            }
            if (!File.Exists(path)) File.WriteAllText(Reports + "/preview-error.txt", "Unity did not produce the Game View preview in time.");
            SessionState.SetBool("VRArchery.Stage3.Preview", false);
            EditorApplication.update -= PreviewTick;
            EditorApplication.isPlaying = false;
        }

        static void Run(string label)
        {
            Directory.CreateDirectory(Reports);
            SessionState.SetString("VRArchery.Stage3.Label", label);
            // Existing Stage2 callbacks also listen to the shared runner. Give them a unique
            // label so these runs never overwrite the historical Stage2 validation reports.
            SessionState.SetString("VRArchery.Stage2.TestLabel", label);
            SessionState.SetBool("VRArchery.Stage3.Active", true);
            bool play = label.StartsWith("play", StringComparison.Ordinal);
            Runner.Execute(new ExecutionSettings(new Filter
            {
                testMode = play ? TestMode.PlayMode : TestMode.EditMode,
                assemblyNames = new[] { play ? "VRArchery.PlayModeTests" : "VRArchery.EditModeTests" }
            }));
        }

        sealed class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                if (!SessionState.GetBool("VRArchery.Stage3.Active", false)) return;
                SessionState.SetBool("VRArchery.Stage3.Active", false);
                string label = SessionState.GetString("VRArchery.Stage3.Label", "tests");
                Directory.CreateDirectory(Reports);
                TestRunnerApi.SaveResultToFile(result, Reports + "/" + label + ".xml");
                File.WriteAllText(Reports + "/" + label + ".txt",
                    $"Result: {result.TestStatus}\nPassed: {result.PassCount}\nFailed: {result.FailCount}\nSkipped: {result.SkipCount}\n");
                Debug.Log($"[VRArchery Stage3] {label}: {result.PassCount} passed, {result.FailCount} failed.");
            }
        }
    }
}
