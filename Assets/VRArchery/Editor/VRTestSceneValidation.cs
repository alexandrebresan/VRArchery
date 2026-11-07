using System;
using System.IO;
using System.Linq;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace VRArchery.Editor
{
    /// <summary>Editor-only setup handoff and repeatable structural/Play Mode smoke checks.</summary>
    [InitializeOnLoad]
    public static class VRTestSceneValidation
    {
        const string RequestDirectory = "Library/VRArcheryStage1";
        static double nextPoll;
        static double playReadyAt;

        static VRTestSceneValidation()
        {
            EditorApplication.update += CheckRequests;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        // Request files are local one-shot setup/test flags, never scripts or commands.
        static void CheckRequests()
        {
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            nextPoll = EditorApplication.timeSinceStartup + 0.5;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (Consume("setup.request"))
            {
                try { VRTestSceneSetup.Create(); Validate(); }
                catch (Exception exception) { Failure("setup", exception); }
            }
            else if (Consume("validate.request"))
            {
                try { Validate(); }
                catch (Exception exception) { Failure("validation", exception); }
            }
            else if (Consume("playtest.request"))
            {
                try { RunPlaySmoke(); }
                catch (Exception exception) { Failure("playmode", exception); }
            }
        }

        static bool Consume(string name)
        {
            var path = Path.Combine(RequestDirectory, name);
            if (!File.Exists(path))
                return false;
            File.Delete(path);
            return true;
        }

        [MenuItem("VRArchery/Etapa 1/Validar cena")]
        public static void Validate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Use the Play Mode smoke check for runtime validation.");
            var scene = SceneManager.GetSceneByPath(VRTestSceneSetup.ScenePath);
            bool openedForValidation = !scene.isLoaded;
            var previous = SceneManager.GetActiveScene();
            if (openedForValidation)
                scene = EditorSceneManager.OpenScene(VRTestSceneSetup.ScenePath, OpenSceneMode.Additive);
            var report = new StringBuilder("VRArchery Stage 1 - scene validation\n");
            try
            {
                var objects = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                    .Select(t => t.gameObject).ToArray();
                Check(objects.SelectMany(g => g.GetComponents<XROrigin>()).Count() == 1, "One existing XR Origin", report);
                Check(objects.SelectMany(g => g.GetComponents<XRInteractionManager>()).Count() == 1, "One interaction manager", report);
                Check(objects.SelectMany(g => g.GetComponents<EventSystem>()).Count() == 1, "One EventSystem", report);
                Check(objects.Any(g => g.GetComponent<XRUIInputModule>() != null), "XR UI input module", report);
                Check(objects.Any(g => g.GetComponent<TrackedDeviceGraphicRaycaster>() != null), "World-space XR UI raycaster", report);
                var origin = objects.SelectMany(g => g.GetComponents<XROrigin>()).Single();
                Check(objects.SelectMany(g => g.GetComponents<Canvas>()).Any(c =>
                    c.renderMode == RenderMode.WorldSpace && c.worldCamera == origin.Camera), "Panel camera reference persisted", report);
                Check(PrefabUtility.GetCorrespondingObjectFromSource(origin) != null, "XR Origin remains a prefab instance", report);
                var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(VRTestSceneSetup.ActionsPath);
                Check(origin.GetComponent<InputActionManager>().actionAssets.Contains(actions), "Existing rig Input Actions assigned", report);
                Check(InputSystem.actions == actions, "Project-wide Input Actions resolve to the existing asset", report);
                var cube = objects.Single(g => g.name == "Cubo Agarravel");
                Check(cube.GetComponent<XRGrabInteractable>() != null && cube.GetComponent<Rigidbody>() != null &&
                    cube.GetComponent<Collider>() != null, "Grab cube has interaction, physics and collider", report);
                var simulator = objects.SelectMany(g => g.GetComponents<XRInteractionSimulator>()).Single();
                Check(simulator.GetComponentsInParent<Transform>(true).Any(t => t.CompareTag("EditorOnly")),
                    "Simulator hierarchy is EditorOnly and stripped from player builds", report);
                Check(simulator.cameraTransform == origin.Camera.transform, "Simulator uses the rig camera", report);
                Check(objects.All(g => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(g) == 0), "No missing scripts", report);
                var button = objects.SelectMany(g => g.GetComponents<Button>()).Single();
                Check(button.onClick.GetPersistentEventCount() == 1 && button.onClick.GetPersistentTarget(0) != null,
                    "Button has a persistent feedback listener", report);
                var indicator = objects.Single(g => g.name == "Indicador Verde");
                Check(!indicator.activeSelf, "Feedback starts off", report);
                Directory.CreateDirectory(VRTestSceneSetup.ReportDirectory);
                File.WriteAllText(VRTestSceneSetup.ReportDirectory + "/validation.txt", report.ToString());
                Debug.Log("[VRArchery] Scene validation passed.");
            }
            finally
            {
                if (openedForValidation)
                {
                    if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [MenuItem("VRArchery/Etapa 1/Teste automatico em Play Mode")]
        public static void RunPlaySmoke()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before starting this check.");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save your scene changes before running the smoke check.");
            EditorSceneManager.OpenScene(VRTestSceneSetup.ScenePath);
            SessionState.SetBool("VRArchery.Stage1.PlaySmoke", true);
            EditorApplication.isPlaying = true;
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("VRArchery.Stage1.PlaySmoke", false))
            {
                playReadyAt = EditorApplication.timeSinceStartup + 3;
                EditorApplication.update += CompletePlaySmoke;
            }
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                SessionState.SetBool("VRArchery.Stage1.PlaySmoke", false);
                EditorApplication.update -= CompletePlaySmoke;
            }
        }

        static void CompletePlaySmoke()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < playReadyAt)
                return;
            EditorApplication.update -= CompletePlaySmoke;
            try
            {
                var report = new StringBuilder("VRArchery Stage 1 - Play Mode smoke check\n");
                var origin = UnityEngine.Object.FindAnyObjectByType<XROrigin>();
                Check(origin != null, "XR Origin running", report);
                Check(UnityEngine.Object.FindObjectsByType<XRInteractionSimulator>().Length == 1,
                    "Exactly one simulator running", report);
                report.AppendLine("Input devices: " + string.Join(", ", InputSystem.devices.Select(d => d.name + " [" + d.layout + "]")));
                Check(InputSystem.devices.Count(d => d.layout.Contains("Simulated")) >= 3, "Simulated HMD and two controllers created", report);
                Check(origin.GetComponent<InputActionManager>().actionAssets.All(a => a.enabled), "Rig actions enabled", report);

                var cube = GameObject.Find("Cubo Agarravel").GetComponent<XRGrabInteractable>();
                var interactor = origin.GetComponentsInChildren<XRBaseInteractor>()
                    .First(i => i.isActiveAndEnabled && i is IXRSelectInteractor && !i.name.Contains("Gaze"));
                interactor.StartManualInteraction((IXRSelectInteractable)cube);
                Check(cube.isSelected, "XRI manual selection enters grab", report);
                interactor.EndManualInteraction();
                Check(!cube.isSelected, "XRI manual selection releases grab", report);

                var panel = GameObject.Find("Painel de Teste");
                var indicator = panel.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Indicador Verde").gameObject;
                Check(!indicator.activeSelf, "Feedback initially off in Play Mode", report);
                panel.GetComponentInChildren<Button>().onClick.Invoke();
                Check(indicator.activeSelf, "Button event activates green feedback", report);
                indicator.SetActive(false);
                report.AppendLine("LIMIT: manual XRI selection and Button.onClick validate wiring, not keyboard/mouse aiming or human ergonomics.");
                File.WriteAllText(VRTestSceneSetup.ReportDirectory + "/playmode.txt", report.ToString());
                Debug.Log("[VRArchery] Play Mode smoke check passed.");
            }
            catch (Exception exception) { Failure("playmode", exception); }
            finally { EditorApplication.isPlaying = false; }
        }

        static void Check(bool condition, string description, StringBuilder report)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + description + "\n" + report);
            report.AppendLine("PASS: " + description);
        }

        static void Failure(string step, Exception exception)
        {
            Directory.CreateDirectory(VRTestSceneSetup.ReportDirectory);
            File.WriteAllText(VRTestSceneSetup.ReportDirectory + "/" + step + "-error.txt", exception.ToString());
            Debug.LogException(exception);
        }
    }
}
