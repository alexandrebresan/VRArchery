using System;
using System.IO;
using System.Linq;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;
using VRArchery.Bow;

namespace VRArchery.Editor.Stage2
{
    public static class Stage2SceneSetup
    {
        public const string Root = "Assets/VRArchery";
        public const string ScenePath = Root + "/Scenes/BowTestScene.unity";
        public const string PrefabPath = Root + "/Prefabs/BowPrototype.prefab";

        [MenuItem("VRArchery/Etapa 2/Criar cena e prefab")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(ScenePath) || File.Exists(PrefabPath))
                throw new InvalidOperationException("Exit Play Mode. Existing Stage 2 scene/prefab will never be overwritten.");
            var backup = File.ReadAllText("Logs/VRArchery-stage2-backup-path.txt").Trim();
            if (!File.Exists(Path.Combine(backup, "project-sources.zip"))) throw new IOException("Stage 2 source backup missing.");
            bool canOpenResult = Enumerable.Range(0, SceneManager.sceneCount).All(i => !SceneManager.GetSceneAt(i).isDirty);
            var previous = SceneManager.GetActiveScene();
            if (!AssetDatabase.CopyAsset(Root + "/Scenes/VRTestScene.unity", ScenePath)) throw new IOException("Could not copy Stage 1 scene.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var roots = scene.GetRootGameObjects();
                foreach (var obj in roots.Where(g => g.name == "Cubo Agarravel" || g.name == "Painel de Teste")) obj.SetActive(false);
                var origin = roots.SelectMany(g => g.GetComponentsInChildren<XROrigin>(true)).Single();
                var bow = BuildBow();
                PrefabUtility.SaveAsPrefabAssetAndConnect(bow.gameObject, PrefabPath, InteractionMode.AutomatedAction);
                bow.transform.position = new Vector3(-0.25f, 1.45f, 0.75f);
                var status = BuildPanel(origin.Camera, out var bar);
                PrefabUtility.SaveAsPrefabAssetAndConnect(status.transform.root.gameObject, Root + "/Prefabs/BowStatusPanel.prefab", InteractionMode.AutomatedAction);
                var canvas = status.GetComponentInParent<Canvas>();
                canvas.worldCamera = origin.Camera;
                PrefabUtility.RecordPrefabInstancePropertyModifications(canvas);
                var feedback = bow.GetComponent<BowFeedback>();
                Reference(feedback, "m_Status", status);
                Reference(feedback, "m_DrawBar", bar);
                PrefabUtility.RecordPrefabInstancePropertyModifications(feedback);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
            if (canOpenResult) EditorSceneManager.OpenScene(ScenePath);
            Directory.CreateDirectory(Stage2Tools.Reports);
            File.WriteAllText(Stage2Tools.Reports + "/setup.txt", "Created BowTestScene from VRTestScene and reusable BowPrototype/BowStatusPanel prefabs.\nStage 1, packages, global settings and original template assets were not modified.\n");
        }

        static BowController BuildBow()
        {
            var wood = Material("BowWood", new Color(0.48f, 0.22f, 0.075f));
            var handleMaterial = Material("BowGrip", new Color(0.04f, 0.5f, 0.65f));
            var stringMaterial = Material("BowString", new Color(1f, 0.92f, 0.65f), true);
            var markerMaterial = Material("BowInteractionPoint", Color.red, true);
            var root = new GameObject("BowPrototype");
            root.SetActive(false);
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var gripCollider = root.AddComponent<BoxCollider>();
            gripCollider.center = new Vector3(0, 0, 0.025f);
            gripCollider.size = new Vector3(0.08f, 0.22f, 0.10f);
            var grip = root.AddComponent<BowGripInteractable>();
            grip.colliders.Clear();
            grip.colliders.Add(gripCollider);
            grip.selectMode = InteractableSelectMode.Single;
            grip.movementType = XRBaseInteractable.MovementType.Kinematic;
            grip.throwOnDetach = false;
            grip.useDynamicAttach = false;
            grip.attachEaseInTime = 0f;
            grip.smoothPosition = grip.smoothRotation = false;
            grip.attachTransform = Point("Grip Attach", root.transform, new Vector3(0, 0, 0.025f));

            Segment(root.transform, "Handle", new Vector3(0, -0.11f, 0.025f), new Vector3(0, 0.11f, 0.025f), 0.03f, handleMaterial);
            var top = new Vector3(0, 0.58f, -0.12f);
            var bottom = new Vector3(0, -0.58f, -0.12f);
            Segment(root.transform, "Upper Limb A", new Vector3(0, 0.1f, 0.025f), new Vector3(0, 0.32f, 0.08f), 0.025f, wood);
            Segment(root.transform, "Upper Limb B", new Vector3(0, 0.32f, 0.08f), top, 0.021f, wood);
            Segment(root.transform, "Lower Limb A", new Vector3(0, -0.1f, 0.025f), new Vector3(0, -0.32f, 0.08f), 0.025f, wood);
            Segment(root.transform, "Lower Limb B", new Vector3(0, -0.32f, 0.08f), bottom, 0.021f, wood);
            var upperTip = Point("Upper Tip", root.transform, top);
            var lowerTip = Point("Lower Tip", root.transform, bottom);
            var rest = Point("String Rest", root.transform, new Vector3(0, 0, -0.12f));
            var center = Point("String Center", root.transform, rest.localPosition);
            var stringBody = center.gameObject.AddComponent<Rigidbody>();
            stringBody.isKinematic = true;
            stringBody.useGravity = false;
            var stringCollider = center.gameObject.AddComponent<SphereCollider>();
            stringCollider.radius = 0.065f;
            // The template NearFar sphere/curve casters ignore triggers.
            stringCollider.isTrigger = false;
            var bowString = center.gameObject.AddComponent<BowStringInteractable>();
            bowString.selectMode = InteractableSelectMode.Single;
            bowString.colliders.Clear();
            bowString.colliders.Add(stringCollider);
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "String Interaction Marker";
            marker.transform.SetParent(center, false);
            marker.transform.localScale = Vector3.one * 0.06f;
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.GetComponent<Renderer>().sharedMaterial = markerMaterial;
            var line = Point("String Visual", root.transform, Vector3.zero).gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 3;
            line.SetPositions(new[] { top, rest.localPosition, bottom });
            line.startWidth = line.endWidth = 0.006f;
            line.numCapVertices = 3;
            line.sharedMaterial = stringMaterial;

            var controller = root.AddComponent<BowController>();
            Reference(controller, "m_Grip", grip);
            Reference(controller, "m_String", bowString);
            Reference(controller, "m_RestPoint", rest);
            Reference(controller, "m_CenterPoint", center);
            Reference(grip, "m_Bow", controller);
            Reference(bowString, "m_Bow", controller);
            var feedback = root.AddComponent<BowFeedback>();
            Reference(feedback, "m_Bow", controller);
            Reference(feedback, "m_StringLine", line);
            Reference(feedback, "m_UpperTip", upperTip);
            Reference(feedback, "m_LowerTip", lowerTip);
            var diagnostics = root.AddComponent<BowStringDiagnostics>();
            Reference(diagnostics, "m_Bow", controller);
            Reference(diagnostics, "m_Marker", marker.GetComponent<Renderer>());
            Reference(feedback, "m_StringDiagnostics", diagnostics);
            root.SetActive(true);
            return controller;
        }

        static TMP_Text BuildPanel(Camera camera, out Image bar)
        {
            var go = new GameObject("Bow Test Panel", typeof(RectTransform), typeof(Canvas), typeof(TrackedDeviceGraphicRaycaster));
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1000, 680);
            rect.position = new Vector3(0, 1.85f, 1.9f);
            rect.localScale = Vector3.one * 0.0013f;
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            go.GetComponent<Canvas>().worldCamera = camera;
            var background = UI("Background", go.transform, Vector2.zero, rect.sizeDelta).AddComponent<Image>();
            background.color = new Color(0.035f, 0.065f, 0.1f, 0.97f);
            Label(go.transform, "Title", "VRARCHERY / ARCO E CORDA", new Vector2(0, 270), new Vector2(950, 70), 40);
            Label(go.transform, "Instructions", "Esquerda: segure o arco pela empunhadura azul.\nDireita: aproxime do ponto da corda.\nVerde: pronta | Vermelho: bloqueada | Amarelo: puxando\nPuxe para tras do arco. Solte para retornar.\nNo simulador: Shift + G (esquerda); G (direita).",
                new Vector2(0, 100), new Vector2(930, 210), 28);
            var status = Label(go.transform, "Draw Status", "Disponivel\nPuxada: 0%  (0.00 m)\nUltima puxada: --", new Vector2(0, -130), new Vector2(930, 230), 28);
            var rail = UI("Bar Background", go.transform, new Vector2(0, -270), new Vector2(800, 24)).AddComponent<Image>();
            rail.color = new Color(0.1f, 0.2f, 0.26f);
            bar = UI("Draw Bar", rail.transform, Vector2.zero, new Vector2(800, 24)).AddComponent<Image>();
            // A filled image requires a sprite; use the built-in UI sprite, no external assets.
            bar.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            bar.type = Image.Type.Filled;
            bar.fillMethod = Image.FillMethod.Horizontal;
            bar.fillAmount = 0;
            bar.color = new Color(0.1f, 0.9f, 0.65f);
            return status;
        }

        static TMP_Text Label(Transform parent, string name, string text, Vector2 position, Vector2 size, float fontSize)
        {
            var label = UI(name, parent, position, size).AddComponent<TextMeshProUGUI>();
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/VRTemplateAssets/Fonts/Inter/Inter-Regular SDF.asset");
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        static GameObject UI(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return go;
        }

        static Transform Point(string name, Transform parent, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            return go.transform;
        }

        static void Segment(Transform parent, string name, Vector3 from, Vector3 to, float radius, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = (from + to) * 0.5f;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
            go.transform.localScale = new Vector3(radius * 2, Vector3.Distance(from, to) * 0.5f, radius * 2);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material;
        }

        static Material Material(string name, Color color, bool unlit = false)
        {
            var path = Root + "/Materials/" + name + ".mat";
            if (File.Exists(path)) throw new IOException("Refusing to overwrite " + path);
            var material = new Material(Shader.Find("Universal Render Pipeline/" + (unlit ? "Unlit" : "Lit"))) { name = name };
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void Reference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
