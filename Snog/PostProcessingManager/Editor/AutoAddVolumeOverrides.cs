#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class AutoAddVolumeOverrides
{
    // Toggle in EditorPrefs if you want to disable it
    private const string kPrefKey = "Snog.AutoAddVolumeOverrides.Enabled";
    static AutoAddVolumeOverrides()
    {
        // default on
        if (!EditorPrefs.HasKey(kPrefKey)) EditorPrefs.SetBool(kPrefKey, true);
        // subscribe once
        Selection.selectionChanged += OnSelectionChanged;
        EditorApplication.playModeStateChanged += _ => { /* no-op to keep compiler quiet if needed */ };
    }

    private static void OnSelectionChanged()
    {
        if (!EditorPrefs.GetBool(kPrefKey, true)) return;

        var go = Selection.activeGameObject;
        if (go == null) return;

        var vol = go.GetComponent<Volume>();
        if (vol == null) return;

        EnsureVolumeHasOverrides(vol);
    }

    // Types to ensure on the profile (full type names, URP namespace)
    // You can add or remove full names here if you want different overrides
    private static readonly string[] requiredTypeNames = new[]
    {
        "UnityEngine.Rendering.Universal.ColorAdjustments",
        "UnityEngine.Rendering.Universal.Tonemapping",
        "UnityEngine.Rendering.Universal.Bloom",
        "UnityEngine.Rendering.Universal.MotionBlur",
        "UnityEngine.Rendering.Universal.Vignette",
        "UnityEngine.Rendering.Universal.DepthOfField",
        "UnityEngine.Rendering.Universal.ChromaticAberration",
        "UnityEngine.Rendering.Universal.LensDistortion",
        "UnityEngine.Rendering.Universal.FilmGrain",
        "UnityEngine.Rendering.Universal.PaniniProjection"
    };

    private static void EnsureVolumeHasOverrides(Volume vol)
    {
        if (vol == null) return;

        // Create an instance profile if none exists
        if (vol.profile == null)
        {
            var newProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            newProfile.name = vol.gameObject.name + " Profile (Auto)";
            Undo.RecordObject(vol, "Assign VolumeProfile");
            vol.profile = newProfile;
            EditorUtility.SetDirty(vol);
            EditorSceneManager.MarkSceneDirty(vol.gameObject.scene);
        }

        var profile = vol.profile;
        if (profile == null) return;

        // Get the internal 'components' collection via reflection (works across URP versions)
        var componentsList = GetComponentsList(profile);
        if (componentsList == null) return;

        bool changed = false;

        foreach (var typeName in requiredTypeNames)
        {
            var t = FindTypeByName(typeName);
            if (t == null) continue;

            if (!HasComponentOfType(componentsList, t))
            {
                // Record undo on profile before changing
                Undo.RecordObject(profile, "Add Volume Override");
                var added = AddComponentToProfile(profile, componentsList, t);
                if (added != null)
                {
                    // try to set .active = true if available
                    var activeField = added.GetType().GetField("active");
                    if (activeField != null && activeField.FieldType == typeof(bool))
                        activeField.SetValue(added, true);

                    // If component has 'SetAllOverridesTo' or similar you could set defaults here
                    changed = true;
                    EditorUtility.SetDirty(profile);
                }
            }
        }

        if (changed)
        {
            // If profile is an asset in project -> save assets
            if (AssetDatabase.Contains(profile))
            {
                AssetDatabase.SaveAssets();
            }
            EditorSceneManager.MarkSceneDirty(vol.gameObject.scene);
        }
    }

    // Try to find a Type across loaded assemblies using a full type name (namespace + type)
    private static Type FindTypeByName(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            catch { /* ignore assemblies that throw */ }
        }
        return null;
    }

    // Return the internal components list (IList) from a VolumeProfile
    private static IList GetComponentsList(VolumeProfile profile)
    {
        var type = typeof(VolumeProfile);
        // try common field/property names
        FieldInfo fi = type.GetField("components", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        if (fi != null)
        {
            var val = fi.GetValue(profile) as IList;
            if (val != null) return val;
        }

        // try property
        PropertyInfo pi = type.GetProperty("components", BindingFlags.Public | BindingFlags.Instance);
        if (pi != null)
        {
            var val = pi.GetValue(profile) as IList;
            if (val != null) return val;
        }

        return null;
    }

    private static bool HasComponentOfType(IList componentsList, Type t)
    {
        foreach (var c in componentsList)
        {
            if (c == null) continue;
            var ct = c.GetType();
            if (ct == t || ct.IsSubclassOf(t)) return true;
        }
        return false;
    }

    // Adds a VolumeComponent of type 't' to the profile using Add<T>() reflection when present,
    // otherwise creates an instance and appends to the internal list.
    private static object AddComponentToProfile(VolumeProfile profile, IList componentsList, Type t)
    {
        // Try generic Add<T>() method
        var addMethod = typeof(VolumeProfile).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.IsGenericMethod && m.Name == "Add" && m.GetGenericArguments().Length == 1);
        if (addMethod != null)
        {
            try
            {
                var generic = addMethod.MakeGenericMethod(t);
                var result = generic.Invoke(profile, null);
                return result;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"AutoAddVolumeOverrides: reflection Add<T>() failed for {t.Name}: {e.Message}");
            }
        }

        // Fallback: create instance and insert into components list
        try
        {
            var inst = ScriptableObject.CreateInstance(t);
            // Add to list if possible
            componentsList.Add(inst);
            return inst;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"AutoAddVolumeOverrides: fallback creation failed for {t.Name}: {e.Message}");
            return null;
        }
    }

    // Public utility: toggle auto behavior
    [MenuItem("Snog/PostProcessManager/AutoAddVolumeOverrides/Disable Auto Add")]
    private static void DisableAutoAdd()
    {
        EditorPrefs.SetBool(kPrefKey, false);
        Debug.Log($"AutoAddVolumeOverrides: disabled");
    }

    [MenuItem("Snog/PostProcessManager/AutoAddVolumeOverrides/Enable Auto Add")]
    private static void EnableAutoAdd()
    {
        EditorPrefs.SetBool(kPrefKey, true);
        Debug.Log($"AutoAddVolumeOverrides: enabled");
    }
}
#endif
