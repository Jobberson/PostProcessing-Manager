using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.SceneManagement;
using Snog.PostProcessingManager.Runtime;

namespace Snog.PostProcessingManager.Editor
{
    [CustomEditor(typeof(PostProcessManager))]
    [CanEditMultipleObjects]
    public class PostProcessManagerEditor : Editor
    {
        private SerializedProperty sp_targetVolume;
        private SerializedProperty sp_presets;
        private ReorderableList presetsList;
        private PostProcessManager ppm;

        void OnEnable()
        {
            ppm = (PostProcessManager)target;
            sp_targetVolume = serializedObject.FindProperty("targetVolume");
            sp_presets = serializedObject.FindProperty("presets");

            // Setup ReorderableList for presets
            presetsList = new ReorderableList(serializedObject, sp_presets, true, true, true, true)
            {
                drawHeaderCallback = rect =>
                {
                    rect.x += 6;
                    EditorGUI.LabelField(rect, "Presets");
                },

                drawElementCallback = (rect, index, isActive, isFocused) =>
                    {
                        var element = sp_presets.GetArrayElementAtIndex(index);
                        rect.y += 2;
                        var rLabel = new Rect(rect.x, rect.y, rect.width - 80, EditorGUIUtility.singleLineHeight);
                        var rButtons = new Rect(rect.x + rect.width - 76, rect.y, 76, EditorGUIUtility.singleLineHeight);

                        EditorGUI.PropertyField(rLabel, element, GUIContent.none);

                        if (GUI.Button(rButtons, "Apply"))
                        {
                            ApplyPresetAtIndex(index);
                        }
                    },

                onAddCallback = list =>
                    {
                        sp_presets.arraySize++;
                        serializedObject.ApplyModifiedProperties();
                    },

                onRemoveCallback = list =>
                    {
                        sp_presets.DeleteArrayElementAtIndex(list.index);
                        serializedObject.ApplyModifiedProperties();
                    }
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(sp_targetVolume, new GUIContent("Target Volume", "Volume that the manager will modify at runtime"));

            EditorGUILayout.Space();

            // Presets list (with search)
            EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
            if (sp_presets != null)
            {
                if (sp_presets.arraySize == 0)
                {
                    EditorGUILayout.HelpBox("No presets found. Add presets to begin.", MessageType.Info);
                }
                presetsList.DoLayoutList();
            }

            EditorGUILayout.Space();

            // Quick actions
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create Preset"))
            {
                CreatePresetAsset();
            }
            if (GUILayout.Button("Find Target Volume"))
            {
                FindOrAssignNearestVolume();
            }
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        private void ApplyPresetAtIndex(int index)
        {
            if (index < 0 || index >= sp_presets.arraySize) return;
            var presetProp = sp_presets.GetArrayElementAtIndex(index);
            var preset = presetProp.objectReferenceValue as PostProcessPreset;
            if (preset == null)
            {
                EditorUtility.DisplayDialog("Apply Preset", "Selected preset is null or not a PostProcessPreset asset.", "OK");
                return;
            }

            // Confirm destructive action
            if (!EditorUtility.DisplayDialog("Apply Preset", $"Apply preset '{preset.name}' to the manager? This will change the manager's live settings.", "Apply", "Cancel"))
                return;

            // Use a safe wrapper that records undo
            var manager = (PostProcessManager)target;
            manager.ApplyPresetWithUndo(preset, "Apply PostProcess Preset from Inspector");

            // mark dirty and mark scene dirty if necessary
            EditorUtility.SetDirty(manager);
            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        }

        private void CreatePresetAsset()
        {
            var preset = CreateInstance<PostProcessPreset>();
            preset.name = "New PostProcess Preset";
            string path = "Assets/NewPostProcessPreset.asset";
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = preset;
        }

        private void FindOrAssignNearestVolume()
        {
            // Try to find a Volume in the scene and assign it
            var manager = (PostProcessManager)target;
            if (manager == null) return;

            Volume nearest = null;
            float best = float.MaxValue;
            foreach (var v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                var d = (v.transform.position - manager.transform.position).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    nearest = v;
                }
            }

            if (nearest != null)
            {
                Undo.RecordObject(manager, "Assign Target Volume");
                manager.targetVolume = nearest;
                EditorUtility.SetDirty(manager);
            }
            else
            {
                EditorUtility.DisplayDialog("Find Volume", "No Volume found in the scene. Create a Volume first.", "OK");
            }
        }

        [MenuItem("GameObject/Snog/PostProcessManager/ManagerObject", false, 10)]
        static void CreateManager(MenuCommand menuCommand)
        {
            var go = new GameObject("PostProcessManager");
            Undo.RegisterCreatedObjectUndo(go, "Create PostProcessManager");
            var ppm = go.AddComponent<PostProcessManager>();
            // Add a Volume child by default
            var volGO = new GameObject("PostProcessVolume");
            volGO.transform.SetParent(go.transform, false);
            var volume = volGO.AddComponent<Volume>();
            volume.isGlobal = true;
            ppm.targetVolume = volume;
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            Selection.activeObject = go;
        }
    }
}