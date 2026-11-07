using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using VRArchery.Bow;

namespace VRArchery.Editor.Stage2
{
    public static class Stage2StringRepair
    {
        [MenuItem("VRArchery/Etapa 2/Aplicar correcao da corda")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before repairing the prefab.");
            var backup = File.ReadAllText("Logs/VRArchery-stage2-fix-backup-path.txt").Trim();
            if (!File.Exists(Path.Combine(backup, "project-sources.zip")))
                throw new IOException("A source backup is required before editing serialized assets.");
            var root = PrefabUtility.LoadPrefabContents(Stage2SceneSetup.PrefabPath);
            try
            {
                var bow = root.GetComponent<BowController>();
                // Preserve the rig's Ignore-trigger policy and all string selection restrictions.
                bow.String.GetComponent<SphereCollider>().isTrigger = false;
                var marker = bow.CenterPoint.GetComponentInChildren<Renderer>();
                var diagnostics = root.GetComponent<BowStringDiagnostics>();
                if (diagnostics == null) diagnostics = root.AddComponent<BowStringDiagnostics>();
                Reference(diagnostics, "m_Bow", bow);
                Reference(diagnostics, "m_Marker", marker);
                Reference(root.GetComponent<BowFeedback>(), "m_StringDiagnostics", diagnostics);
                var material = marker.sharedMaterial;
                material.SetColor("_BaseColor", Color.red);
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
                PrefabUtility.SaveAsPrefabAsset(root, Stage2SceneSetup.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var panelPath = Stage2SceneSetup.Root + "/Prefabs/BowStatusPanel.prefab";
            var panel = PrefabUtility.LoadPrefabContents(panelPath);
            try
            {
                foreach (var label in panel.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (label.name == "Draw Status")
                    {
                        label.fontSize = 28;
                        label.rectTransform.sizeDelta = new Vector2(930, 230);
                        label.rectTransform.anchoredPosition = new Vector2(0, -130);
                    }
                    if (label.name == "Instructions")
                        label.text = "Esquerda: segure o arco pela empunhadura azul.\nDireita: aproxime do ponto da corda.\nVerde: pronta | Vermelho: bloqueada | Amarelo: puxando\nPuxe para tras do arco. Solte para retornar.\nNo simulador: Shift + G (esquerda); G (direita).";
                }
                PrefabUtility.SaveAsPrefabAsset(panel, panelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(panel); }
            Directory.CreateDirectory(Stage2Tools.Reports);
            File.WriteAllText(Stage2Tools.Reports + "/repair.txt",
                "String collider changed to non-trigger to match the unchanged template NearFar casters.\n" +
                "Installed red/green/yellow diagnostics and panel messages, without changing the rig or selection rules.\n");
        }

        static void Reference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
