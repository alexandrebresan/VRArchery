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
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using VRArchery.Arrows;
using VRArchery.Bow;

namespace VRArchery.Editor.Stage3
{
    public static class Stage3SceneSetup
    {
        public const string Root = "Assets/VRArchery";
        public const string ScenePath = Root + "/Scenes/ArrowTestScene.unity";
        public const string ArrowPath = Root + "/Prefabs/ArrowProjectile.prefab";
        public const string BowPath = Root + "/Prefabs/ArrowBow.prefab";
        public const string TargetPath = Root + "/Prefabs/StaticArrowTarget.prefab";

        [MenuItem("VRArchery/Etapa 3/Criar cena e prefabs")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (new[] { ScenePath, ArrowPath, BowPath, TargetPath }.Any(File.Exists))
                throw new IOException("Stage 3 assets already exist; existing work is never overwritten.");
            var backup = File.ReadAllText("Logs/VRArchery-stage3-backup-path.txt").Trim();
            if (!File.Exists(Path.Combine(backup, "project-sources.zip"))) throw new IOException("Stage 3 source backup missing.");
            bool canOpen = Enumerable.Range(0, SceneManager.sceneCount).All(i => !SceneManager.GetSceneAt(i).isDirty);
            var previous = SceneManager.GetActiveScene();
            if (!AssetDatabase.CopyAsset(Root + "/Scenes/BowTestScene.unity", ScenePath)) throw new IOException("Could not copy BowTestScene.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var shaft = Material("ArrowShaft", new Color(0.9f, 0.72f, 0.25f));
                var tip = Material("ArrowTip", new Color(0.75f, 0.82f, 0.9f));
                var fins = Material("ArrowFletching", new Color(0.9f, 0.12f, 0.08f));
                var face = Material("ArrowTargetFace", new Color(0.9f, 0.9f, 0.85f));
                var ring = Material("ArrowTargetRing", new Color(0.04f, 0.38f, 0.7f));
                var arrow = BuildArrow(shaft, tip, fins);
                PrefabUtility.SaveAsPrefabAsset(arrow.gameObject, ArrowPath);
                UnityEngine.Object.DestroyImmediate(arrow.gameObject);
                var target = BuildTarget(face, ring, fins);
                PrefabUtility.SaveAsPrefabAssetAndConnect(target, TargetPath, InteractionMode.AutomatedAction);
                target.transform.position = new Vector3(0, 1.45f, 6);

                var roots = scene.GetRootGameObjects();
                var bow = roots.SelectMany(g => g.GetComponentsInChildren<BowController>(true)).Single();
                var origin = roots.SelectMany(g => g.GetComponentsInChildren<XROrigin>(true)).Single();
                var pose = bow.transform.position;
                bow.gameObject.SetActive(false);
                var launcher = bow.gameObject.AddComponent<BowArrowLauncher>();
                Reference(launcher, "m_Bow", bow);
                Reference(launcher, "m_ArrowPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPath).GetComponent<ArrowProjectile>());
                bow.transform.position = Vector3.zero;
                bow.gameObject.SetActive(true);
                PrefabUtility.SaveAsPrefabAssetAndConnect(bow.gameObject, BowPath, InteractionMode.AutomatedAction);
                bow.transform.position = pose;
                launcher.PlayerRoot = origin.transform;
                EditorUtility.SetDirty(launcher);
                PrefabUtility.RecordPrefabInstancePropertyModifications(launcher);

                var panel = roots.Single(g => g.name == "Bow Test Panel");
                panel.name = "Arrow Test Panel";
                PositionPanel(panel.transform);
                var status = panel.GetComponentsInChildren<TMP_Text>().Single(t => t.name == "Draw Status");
                var bar = panel.GetComponentsInChildren<Image>().Single(i => i.name == "Draw Bar");
                Reference(bow.GetComponent<BowFeedback>(), "m_Status", status);
                Reference(bow.GetComponent<BowFeedback>(), "m_DrawBar", bar);
                PrefabUtility.RecordPrefabInstancePropertyModifications(bow.GetComponent<BowFeedback>());
                foreach (var label in panel.GetComponentsInChildren<TMP_Text>())
                {
                    if (label.name == "Title") label.text = "VRARCHERY / DISPARO DE FLECHAS";
                    if (label.name == "Instructions") label.text =
                        "Esquerda: segure o arco. Direita: puxe a corda.\nPuxe ao menos 8 cm e solte G para disparar.\nMire no alvo a 6 m; a gravidade curva a trajetoria.\nNova flecha automatica. Sem pontuacao.\nShift + G: esquerda | G: direita.";
                    PrefabUtility.RecordPrefabInstancePropertyModifications(label);
                }
                var stand = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stand.name = "Target Stand";
                stand.transform.position = new Vector3(0, 0.45f, 6.05f);
                stand.transform.localScale = new Vector3(0.13f, 0.9f, 0.13f);
                stand.GetComponent<Renderer>().sharedMaterial = shaft;
                var backstop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                backstop.name = "Arrow Backstop";
                backstop.transform.position = new Vector3(0, 1.35f, 6.5f);
                backstop.transform.localScale = new Vector3(4, 2.7f, 0.2f);
                backstop.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Table.mat");
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
            if (canOpen) EditorSceneManager.OpenScene(ScenePath);
            Directory.CreateDirectory(Stage3Tools.Reports);
            File.WriteAllText(Stage3Tools.Reports + "/setup.txt",
                "ArrowTestScene copied from BowTestScene. ArrowBow is a variant of BowPrototype.\n" +
                "Arrow prefab, static circular mesh target at 6m and backstop created. PlayerRoot assigned to the existing XR Origin.\n" +
                "Original scenes/prefabs, packages and global settings were not saved or changed.\n");
        }

        static void PositionPanel(Transform panel)
        {
            var rect = (RectTransform)panel;
            // RectTransform serializes X/Y through the anchored position.
            rect.anchoredPosition3D = new Vector3(-1.5f, 1.65f, 1.8f);
            rect.localRotation = Quaternion.Euler(0, -35, 0);
            EditorUtility.SetDirty(rect);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel);
        }

        [MenuItem("VRArchery/Etapa 3/Conferir ambiente de tiro")]
        public static void ArrangePanelAndValidate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                if (scene.isDirty) throw new InvalidOperationException("Save scene changes before arranging the test environment.");
                var roots = scene.GetRootGameObjects();
                var origin = roots.SelectMany(g => g.GetComponentsInChildren<XROrigin>(true)).Single();
                var launcher = roots.SelectMany(g => g.GetComponentsInChildren<BowArrowLauncher>(true)).Single();
                var simulator = roots.SelectMany(g => g.GetComponentsInChildren<XRInteractionSimulator>(true)).Single();
                var target = roots.Single(g => g.name == "Static Arrow Target");
                if (launcher.PlayerRoot != origin.transform) throw new InvalidOperationException("PlayerRoot reference did not persist.");
                if (!simulator.GetComponentsInParent<Transform>().Any(t => t.CompareTag("EditorOnly")))
                    throw new InvalidOperationException("Simulator must remain EditorOnly.");
                if (target.GetComponent<MeshCollider>() == null || target.GetComponent<Rigidbody>() != null)
                    throw new InvalidOperationException("Target must be a static mesh collider.");
                if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(launcher.gameObject) != BowPath)
                    throw new InvalidOperationException("Bow must remain connected to its ArrowBow variant.");
                var panel = roots.Single(g => g.name == "Arrow Test Panel");
                PositionPanel(panel.transform);
                EditorSceneManager.SaveScene(scene);
                var all = roots.SelectMany(g => g.GetComponentsInChildren<Transform>(true));
                if (all.Any(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0))
                    throw new InvalidOperationException("Missing script in ArrowTestScene.");
                File.WriteAllText(Stage3Tools.Reports + "/scene-validation.txt",
                    "PASS: XR Origin PlayerRoot reference persisted.\nPASS: simulator remains EditorOnly.\n" +
                    "PASS: circular target uses a static MeshCollider.\nPASS: ArrowBow variant link preserved.\n" +
                    "PASS: no missing scripts.\nPASS: panel moved left to clear the target's line of sight.\n");
            }
            finally
            {
                if (opened)
                {
                    if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        static ArrowProjectile BuildArrow(Material shaft, Material tip, Material fins)
        {
            var root = new GameObject("ArrowProjectile");
            var body = root.AddComponent<Rigidbody>();
            body.mass = 0.03f;
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.linearDamping = 0;
            var collider = root.AddComponent<CapsuleCollider>();
            collider.direction = 2;
            collider.radius = 0.012f;
            collider.height = 0.67f;
            collider.center = new Vector3(0, 0, 0.335f);
            Shape(root.transform, "Shaft", PrimitiveType.Cylinder, new Vector3(0, 0, 0.32f),
                new Vector3(0.014f, 0.3f, 0.014f), Quaternion.Euler(90, 0, 0), shaft);
            Shape(root.transform, "Tip Model", PrimitiveType.Sphere, new Vector3(0, 0, 0.635f),
                new Vector3(0.024f, 0.024f, 0.07f), Quaternion.identity, tip);
            for (int i = 0; i < 3; i++)
                Shape(root.transform, "Fletching " + i, PrimitiveType.Cube, new Vector3(0, 0, 0.09f),
                    new Vector3(0.07f, 0.003f, 0.12f), Quaternion.Euler(0, 0, i * 60), fins);
            var tipPoint = new GameObject("Tip Point").transform;
            tipPoint.SetParent(root.transform, false);
            tipPoint.localPosition = Vector3.forward * 0.67f;
            var projectile = root.AddComponent<ArrowProjectile>();
            Reference(projectile, "m_Tip", tipPoint);
            return projectile;
        }

        static GameObject BuildTarget(Material face, Material ring, Material center)
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            target.name = "Static Arrow Target";
            UnityEngine.Object.DestroyImmediate(target.GetComponent<Collider>());
            target.AddComponent<MeshCollider>().sharedMesh = target.GetComponent<MeshFilter>().sharedMesh;
            target.transform.rotation = Quaternion.Euler(90, 0, 0);
            target.transform.localScale = new Vector3(1.6f, 0.05f, 1.6f);
            target.GetComponent<Renderer>().sharedMaterial = face;
            Shape(target.transform, "Blue Ring", PrimitiveType.Cylinder, new Vector3(0, -1.015f, 0),
                new Vector3(0.67f, 0.01f, 0.67f), Quaternion.identity, ring);
            Shape(target.transform, "Red Center", PrimitiveType.Cylinder, new Vector3(0, -1.04f, 0),
                new Vector3(0.27f, 0.01f, 0.27f), Quaternion.identity, center);
            return target;
        }

        static void Shape(Transform parent, string name, PrimitiveType primitive, Vector3 position,
            Vector3 scale, Quaternion rotation, Material material)
        {
            var go = GameObject.CreatePrimitive(primitive);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.transform.localRotation = rotation;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material;
        }

        static Material Material(string name, Color color)
        {
            var path = Root + "/Materials/" + name + ".mat";
            if (File.Exists(path)) throw new IOException("Refusing to overwrite " + path);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
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
