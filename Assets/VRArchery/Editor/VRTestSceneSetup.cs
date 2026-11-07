using System;
using System.IO;
using System.Linq;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace VRArchery.Editor
{
    /// <summary>Creates only the approved Stage 1 assets through Unity's serialization APIs.</summary>
    public static class VRTestSceneSetup
    {
        public const string Root = "Assets/VRArchery";
        public const string ScenePath = Root + "/Scenes/VRTestScene.unity";
        public const string SamplePath = "Assets/Samples/XR Interaction Toolkit/3.4.1/XR Interaction Simulator";
        public const string ActionsPath = "Assets/Samples/XR Interaction Toolkit/3.4.1/Starter Assets/XRI Default Input Actions.inputactions";
        public const string ReportDirectory = "Logs/VRArcheryStage1";

        [MenuItem("VRArchery/Etapa 1/Preparar base de testes")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before preparing the test scene.");
            if (!File.Exists("Logs/VRArchery-stage1-backup-path.txt") ||
                !File.Exists(Path.Combine(File.ReadAllText("Logs/VRArchery-stage1-backup-path.txt").Trim(), "project-sources.zip")))
                throw new InvalidOperationException("A verified source backup is required before setup.");
            if (File.Exists(ScenePath))
                throw new InvalidOperationException("VRTestScene already exists. Existing work will not be overwritten.");

            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(
                "Packages/com.unity.xr.interaction.toolkit/package.json");
            if (package == null || package.version != "3.4.1")
                throw new InvalidOperationException("This setup expects the already installed XRI 3.4.1.");
            var simulatorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SamplePath + "/XR Interaction Simulator.prefab");
            if (simulatorPrefab == null)
                throw new InvalidOperationException("Import the XR Interaction Simulator sample from XRI 3.4.1 first.");

            var previous = SceneManager.GetActiveScene();
            bool canOpenResult = Enumerable.Range(0, SceneManager.sceneCount)
                .All(i => !SceneManager.GetSceneAt(i).isDirty);
            if (!AssetDatabase.CopyAsset("Assets/Scenes/BasicScene.unity", ScenePath))
                throw new IOException("Could not copy BasicScene. No original scene was edited.");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var origin = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<XROrigin>(true)).Single();
                var floor = scene.GetRootGameObjects().Single(g => g.name == "Plane");
                floor.name = "Chao de Testes";
                floor.transform.localScale = new Vector3(2f, 1f, 2f);
                floor.GetComponent<Renderer>().sharedMaterial = Material("Floor", new Color(0.15f, 0.20f, 0.25f));

                var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
                table.name = "Mesa de Testes";
                table.transform.position = new Vector3(0f, 0.75f, 0.85f);
                table.transform.localScale = new Vector3(1.3f, 0.1f, 0.65f);
                table.GetComponent<Renderer>().sharedMaterial = Material("Table", new Color(0.25f, 0.36f, 0.44f));

                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Cubo Agarravel";
                cube.transform.position = new Vector3(-0.25f, 0.95f, 0.85f);
                cube.transform.localScale = Vector3.one * 0.18f;
                cube.GetComponent<Renderer>().sharedMaterial = Material("GrabCube", new Color(1f, 0.46f, 0.12f));
                var body = cube.AddComponent<Rigidbody>();
                body.mass = 0.25f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                var grab = cube.AddComponent<XRGrabInteractable>();
                grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
                grab.throwOnDetach = true;
                PrefabUtility.SaveAsPrefabAssetAndConnect(cube, Root + "/Prefabs/GrabTestCube.prefab", InteractionMode.AutomatedAction);

                var panel = CreatePanel(origin.Camera);
                PrefabUtility.SaveAsPrefabAssetAndConnect(panel, Root + "/Prefabs/InteractionTestPanel.prefab", InteractionMode.AutomatedAction);
                var panelCanvas = panel.GetComponent<Canvas>();
                panelCanvas.worldCamera = origin.Camera;
                PrefabUtility.RecordPrefabInstancePropertyModifications(panelCanvas);

                // A scene-local EditorOnly parent is stripped by Unity during player builds.
                // Project-wide simulator settings and the original sample prefab remain untouched.
                var editorOnly = new GameObject("Editor Only - XR Interaction Simulator");
                editorOnly.tag = "EditorOnly";
                var simulator = (GameObject)PrefabUtility.InstantiatePrefab(simulatorPrefab, scene);
                simulator.transform.SetParent(editorOnly.transform, false);
                var simulatorComponent = simulator.GetComponentInChildren<XRInteractionSimulator>(true);
                simulatorComponent.cameraTransform = origin.Camera.transform;
                PrefabUtility.RecordPrefabInstancePropertyModifications(simulatorComponent);

                EditorSceneManager.SaveScene(scene);
                var inputResult = RepairMissingProjectActions();
                Directory.CreateDirectory(ReportDirectory);
                File.WriteAllText(ReportDirectory + "/setup.txt",
                    $"Scene: {ScenePath}\nXRI: {package.version}\nSimulator: scene-local, EditorOnly\n{inputResult}\n" +
                    "Original scenes and prefabs were not saved. Build scene list was not changed.\n");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded)
                    SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }

            if (canOpenResult)
                EditorSceneManager.OpenScene(ScenePath);
            AssetDatabase.Refresh();
            Debug.Log("[VRArchery] Stage 1 prepared. See Logs/VRArcheryStage1/setup.txt.");
        }

        static Material Material(string name, Color color)
        {
            var path = Root + "/Materials/" + name + ".mat";
            if (File.Exists(path))
                throw new IOException("Refusing to overwrite " + path);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("The installed URP Lit shader was not found.");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static GameObject CreatePanel(Camera camera)
        {
            var panel = new GameObject("Painel de Teste", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster));
            panel.layer = LayerMask.NameToLayer("UI");
            var rect = panel.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(900f, 500f);
            rect.position = new Vector3(0f, 1.6f, 1.8f);
            rect.localScale = Vector3.one * 0.0015f;
            var canvas = panel.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            panel.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;

            var background = UIObject("Fundo", panel.transform, Vector2.zero, new Vector2(900, 500));
            background.AddComponent<Image>().color = new Color(0.04f, 0.08f, 0.12f, 0.97f);
            Label(panel.transform, "Titulo", "VRARCHERY", new Vector2(0, 175), new Vector2(820, 65), 48);
            Label(panel.transform, "Instrucoes", "BASE DE TESTES VR\n\nAgarre o cubo laranja e solte sobre a mesa.\nAponte para o botao: o indicador verde deve acender.",
                new Vector2(0, 35), new Vector2(820, 190), 28);

            var buttonObject = UIObject("Botao Confirmar Interacao", panel.transform, new Vector2(-45, -135), new Vector2(550, 90));
            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.06f, 0.48f, 0.62f);
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            Label(buttonObject.transform, "Texto", "CONFIRMAR INTERACAO", Vector2.zero, new Vector2(530, 80), 28);

            var indicator = UIObject("Indicador Verde", panel.transform, new Vector2(315, -135), new Vector2(70, 70));
            indicator.AddComponent<Image>().color = new Color(0.15f, 1f, 0.4f);
            indicator.SetActive(false);
            UnityEventTools.AddBoolPersistentListener(button.onClick, indicator.SetActive, true);
            return panel;
        }

        static GameObject UIObject(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.layer = LayerMask.NameToLayer("UI");
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return obj;
        }

        static void Label(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize)
        {
            var label = UIObject(name, parent, position, size).AddComponent<TextMeshProUGUI>();
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/VRTemplateAssets/Fonts/Inter/Inter-Regular SDF.asset");
            label.text = value;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
        }

        static string RepairMissingProjectActions()
        {
            const string missingGuid = "985fdffee33a5944396199c6b7145dd4";
            bool hasStaleReference = File.ReadAllText("ProjectSettings/EditorBuildSettings.asset").Contains(missingGuid);
            if (!hasStaleReference || !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(missingGuid)) || InputSystem.actions != null)
                return "Project-wide Input Actions: no confirmed broken reference; no change.";
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            if (actions == null)
                throw new InvalidOperationException("The rig's existing InputActionAsset could not be loaded.");
            InputSystem.actions = actions;
            return "Confirmed missing project-wide InputActionAsset. Reassigned to the rig's existing XRI Default Input Actions.";
        }
    }
}
