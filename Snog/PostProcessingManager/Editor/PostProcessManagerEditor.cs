using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;

[CustomEditor(typeof(PostProcessManager))]
[CanEditMultipleObjects]
public class PostProcessManagerEditor : Editor
{
    private SerializedProperty sp_targetVolume;
    private SerializedProperty sp_presets;

    private ReorderableList presetsList;
    private Editor presetInlineEditor;

    // UX foldouts
    private bool foldVolume = true;
    private bool foldPresets = true;
    private bool foldQuickEffects = true;
    private bool foldRuntime = true;

    // Session key prefix (per-target instance)
    private string sessionKeyPrefix;

    // Quick effect controls state
    private float quickBlend = 1f;
    private float quickDuration = 0f;
    private float quickBloom = 1f;
    private float quickVignette = 0.3f;
    private float quickChromatic = 0.1f;
    private float quickFilmGrain = 0.1f;
    private float quickLensDistortion = 0f;
    private float quickMotionBlur = 0.5f;
    private float quickDOF = 10f;
    private float quickPanini = 1f;
    private float quickWhiteTemp = 0f;
    private float quickWhiteTint = 0f;

    private void OnEnable()
    {
        sp_targetVolume = serializedObject.FindProperty("targetVolume");
        sp_presets = serializedObject.FindProperty("presets");

        // unique-per-target key so foldouts are stored per inspected object
        sessionKeyPrefix = $"PPM_{target.GetInstanceID()}";

        // Load foldout states from SessionState (editor session only)
        foldVolume = SessionState.GetBool(sessionKeyPrefix + "_foldVolume", true);
        foldPresets = SessionState.GetBool(sessionKeyPrefix + "_foldPresets", true);
        foldQuickEffects = SessionState.GetBool(sessionKeyPrefix + "_foldQuickEffects", true);
        foldRuntime = SessionState.GetBool(sessionKeyPrefix + "_foldRuntime", true);

        // Build ReorderableList for presets
        presetsList = new ReorderableList(serializedObject, sp_presets, true, true, true, true)
        {
            drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, "Presets");
            },
            drawElementCallback = DrawPresetElement,
            onAddCallback = OnAddPreset,
            onRemoveCallback = list =>
            {
                if (EditorUtility.DisplayDialog("Remove Preset", "Remove selected preset from manager's list?", "Remove", "Cancel"))
                {
                    ReorderableList.defaultBehaviours.DoRemoveButton(list);
                }
            },
            onSelectCallback = list =>
            {
                CreateInlinePresetEditor(list.index);
            },
            elementHeightCallback = index =>
            {
                return EditorGUIUtility.singleLineHeight + 6;
            }
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        PostProcessManager mgr = (PostProcessManager)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("PostProcess Manager", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Volume foldout
        foldVolume = EditorGUILayout.BeginFoldoutHeaderGroup(foldVolume, "Target Volume & Cache");
        // persist immediately to session
        SessionState.SetBool(sessionKeyPrefix + "_foldVolume", foldVolume);

        if (foldVolume)
        {
            EditorGUILayout.PropertyField(sp_targetVolume, new GUIContent("Target Volume", "Assign the Volume this manager will control."));

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Cache"))
            {
                // call RefreshProfile to re-cache components
                var vol = sp_targetVolume.objectReferenceValue as Volume;
                mgr.RefreshProfile(vol);
            }
            if (GUILayout.Button("Clear Target"))
            {
                sp_targetVolume.objectReferenceValue = null;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox("Assign a Volume (global or local). Use Refresh Cache after changing the profile at runtime or in editor to rebind components.", MessageType.Info);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space();

        // Presets foldout (reorderable list + inline editor)
        foldPresets = EditorGUILayout.BeginFoldoutHeaderGroup(foldPresets, "Presets");
        SessionState.SetBool(sessionKeyPrefix + "_foldPresets", foldPresets);

        if (foldPresets)
        {
            presetsList.DoLayoutList();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create New Preset Asset"))
            {
                CreateNewPresetAssetAndAdd(mgr);
            }
            if (GUILayout.Button("Add Existing Preset (Select)"))
            {
                // open object picker for PostProcessPreset
                EditorGUIUtility.ShowObjectPicker<PostProcessPreset>(null, false, "", 12345);
            }
            EditorGUILayout.EndHorizontal();

            // Handle object picker callback
            if (Event.current.commandName == "ObjectSelectorClosed")
            {
                UnityEngine.Object picked = EditorGUIUtility.GetObjectPickerObject();
                if (picked is PostProcessPreset pAsset)
                {
                    serializedObject.Update();
                    sp_presets.arraySize++;
                    sp_presets.GetArrayElementAtIndex(sp_presets.arraySize - 1).objectReferenceValue = pAsset;
                    serializedObject.ApplyModifiedProperties();
                }
            }

            // Inline preview/editor for selected preset
            int idx = presetsList.index;
            if (idx >= 0 && idx < sp_presets.arraySize)
            {
                var elementProp = sp_presets.GetArrayElementAtIndex(idx);
                var presetAsset = elementProp.objectReferenceValue as PostProcessPreset;
                if (presetAsset != null)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Selected Preset Editor", EditorStyles.boldLabel);
                    if (presetInlineEditor == null)
                        CreateInlinePresetEditor(idx);

                    if (presetInlineEditor != null)
                    {
                        presetInlineEditor.OnInspectorGUI();
                    }

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Preview on Manager Target"))
                    {
                        mgr.ApplyPresetInstantAtIndex(idx, 1f);
                    }
                    if (GUILayout.Button("Apply (blend)"))
                    {
                        mgr.ApplyPresetAtIndex(idx, quickBlend, quickDuration);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space();

        // Quick Effects foldout
        foldQuickEffects = EditorGUILayout.BeginFoldoutHeaderGroup(foldQuickEffects, "Quick Effect Controls");
        SessionState.SetBool(sessionKeyPrefix + "_foldQuickEffects", foldQuickEffects);

        if (foldQuickEffects)
        {
            EditorGUILayout.HelpBox("Quick controls: adjust a parameter and press Apply to call the manager's API. This is convenience for tuning in-play.", MessageType.None);

            quickBlend = EditorGUILayout.Slider("Blend", quickBlend, 0f, 1f);
            quickDuration = EditorGUILayout.FloatField("Duration (s)", quickDuration);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Bloom / VFX", EditorStyles.boldLabel);
            quickBloom = EditorGUILayout.Slider("Bloom Intensity", quickBloom, 0f, 10f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply Bloom"))
            {
                mgr.ApplyBloom(quickBloom, quickBloom, quickBlend, quickDuration);
            }
            if (GUILayout.Button("Instant Bloom"))
            {
                mgr.ApplyBloom(quickBloom, quickBloom, quickBlend, 0f);
            }
            EditorGUILayout.EndHorizontal();

            quickVignette = EditorGUILayout.Slider("Vignette", quickVignette, 0f, 1f);
            if (GUILayout.Button("Apply Vignette"))
                mgr.ApplyVignette(quickVignette, quickBlend, quickDuration);

            quickChromatic = EditorGUILayout.Slider("Chromatic Aberration", quickChromatic, 0f, 1f);
            if (GUILayout.Button("Apply Chromatic"))
                mgr.ApplyChromatic(quickChromatic, quickBlend, quickDuration);

            quickFilmGrain = EditorGUILayout.Slider("Film Grain", quickFilmGrain, 0f, 1f);
            if (GUILayout.Button("Apply Film Grain"))
                mgr.ApplyFilmGrain(quickFilmGrain, quickBlend, quickDuration);

            quickLensDistortion = EditorGUILayout.Slider("Lens Distortion", quickLensDistortion, -1f, 1f);
            if (GUILayout.Button("Apply Lens Distortion"))
                mgr.ApplyLensDistortion(quickLensDistortion, quickBlend, quickDuration);

            quickMotionBlur = EditorGUILayout.Slider("Motion Blur", quickMotionBlur, 0f, 1f);
            if (GUILayout.Button("Apply Motion Blur"))
                mgr.ApplyMotionBlur(quickMotionBlur, quickBlend, quickDuration);

            EditorGUILayout.Space();
            quickDOF = EditorGUILayout.FloatField("DOF Focus Distance", quickDOF);
            if (GUILayout.Button("Apply DOF"))
                mgr.ApplyDOFFocus(quickDOF, quickBlend, quickDuration);

            quickPanini = EditorGUILayout.FloatField("Panini Distance", quickPanini);
            if (GUILayout.Button("Apply Panini"))
                mgr.ApplyPaniniProjection(quickPanini, quickBlend, quickDuration);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("White Balance", EditorStyles.boldLabel);
            quickWhiteTemp = EditorGUILayout.FloatField("Temperature", quickWhiteTemp);
            quickWhiteTint = EditorGUILayout.FloatField("Tint", quickWhiteTint);
            if (GUILayout.Button("Apply White Balance"))
                mgr.ApplyWhiteBalance(quickWhiteTemp, quickWhiteTint, quickBlend, quickDuration);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space();

        // Runtime / active coroutines foldout
        foldRuntime = EditorGUILayout.BeginFoldoutHeaderGroup(foldRuntime, "Runtime / Debug");
        SessionState.SetBool(sessionKeyPrefix + "_foldRuntime", foldRuntime);

        if (foldRuntime)
        {
            EditorGUILayout.LabelField("Active blends / coroutines (reflection)", EditorStyles.boldLabel);
            // Use reflection to read private 'activeCoroutines' dictionary
            var dict = GetActiveCoroutines(mgr);
            if (dict != null)
            {
                EditorGUILayout.LabelField($"Active coroutines: {dict.Count}");
                foreach (var k in dict.Keys)
                {
                    EditorGUILayout.LabelField("• " + k);
                }
            }
            else
            {
                EditorGUILayout.LabelField("No runtime data available (inspector can still call manager methods).");
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Stop All Blends"))
            {
                mgr.StopAllActive();
            }
            if (GUILayout.Button("Cache Components"))
            {
                // attempt to re-cache using RefreshProfile with current targetVolume
                var vol = sp_targetVolume.objectReferenceValue as Volume;
                mgr.RefreshProfile(vol);
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawPresetElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        var element = sp_presets.GetArrayElementAtIndex(index);
        rect.y += 2;
        rect.height = EditorGUIUtility.singleLineHeight;
        EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, rect.height), element, GUIContent.none);
    }

    private void OnAddPreset(ReorderableList list)
    {
        // Add a null slot and select it
        serializedObject.Update();
        sp_presets.arraySize++;
        sp_presets.GetArrayElementAtIndex(sp_presets.arraySize - 1).objectReferenceValue = null;
        serializedObject.ApplyModifiedProperties();
        list.index = sp_presets.arraySize - 1;
    }

    private void CreateInlinePresetEditor(int index)
    {
        if (index < 0 || index >= sp_presets.arraySize)
        {
            presetInlineEditor = null;
            return;
        }
        var prop = sp_presets.GetArrayElementAtIndex(index);
        var asset = prop.objectReferenceValue as UnityEngine.Object;
        if (asset == null)
        {
            presetInlineEditor = null;
            return;
        }
        if (presetInlineEditor == null || !ReferenceEquals(presetInlineEditor.target, asset))
        {
            if (presetInlineEditor != null)
                DestroyImmediate(presetInlineEditor);

            presetInlineEditor = Editor.CreateEditor(asset);
        }
    }

    private void CreateNewPresetAssetAndAdd(PostProcessManager mgr)
    {
        // Create a new PostProcessPreset asset in the currently selected folder or root Assets
        string path = "Assets";
        var obj = Selection.activeObject;
        if (obj != null)
        {
            string p = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(p))
            {
                if (System.IO.Directory.Exists(p))
                    path = p;
                else
                    path = System.IO.Path.GetDirectoryName(p);
            }
        }

        string uniquePath = AssetDatabase.GenerateUniqueAssetPath($"{path}/NewPostProcessPreset.asset");
        var newPreset = ScriptableObject.CreateInstance<PostProcessPreset>();
        AssetDatabase.CreateAsset(newPreset, uniquePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Add to manager's list
        serializedObject.Update();
        sp_presets.arraySize++;
        sp_presets.GetArrayElementAtIndex(sp_presets.arraySize - 1).objectReferenceValue = newPreset;
        serializedObject.ApplyModifiedProperties();

        // open inspector for new asset
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = newPreset;
    }

    private Dictionary<string, UnityEngine.Coroutine> GetActiveCoroutines(PostProcessManager mgr)
    {
        // Use reflection to read private Dictionary<string, Coroutine> activeCoroutines
        try
        {
            var f = typeof(PostProcessManager).GetField("activeCoroutines", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f != null)
            {
                var val = f.GetValue(mgr) as Dictionary<string, UnityEngine.Coroutine>;
                return val;
            }
        }
        catch { /* swallow reflection errors */ }
        return null;
    }

    private void OnDisable()
    {
        if (presetInlineEditor != null)
        {
            DestroyImmediate(presetInlineEditor);
        }
    }
}
