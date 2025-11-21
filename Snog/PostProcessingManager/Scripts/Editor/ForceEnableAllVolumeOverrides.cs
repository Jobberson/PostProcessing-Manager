#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Snog.PostProcessingManager.Editor; // Add this at the top

public static class ForceEnableAllVolumeOverrides
{
    private static readonly string[] requiredTypeNames = PostProcessOverrideConfig.RequiredTypeNames;
    
    [MenuItem("Snog/PostProcessManager/Volumes/Enable All Override Settings For Selected Volume")]
    public static void ForceEnableForSelectedVolume()
    {

        var go = Selection.activeGameObject;
        if (go == null) { Debug.LogWarning("Select a GameObject with a Volume."); return; }
        var vol = go.GetComponent<Volume>();
        if (vol == null) { Debug.LogWarning("Selected object doesn't have a Volume."); return; }

        // Ensure profile instance
        if (vol.profile == null)
        {
            Undo.RecordObject(vol, "Assign VolumeProfile");
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = vol.gameObject.name + " Profile (Auto)";
            vol.profile = profile;
            EditorUtility.SetDirty(vol);
        }

        var profileToEdit = vol.profile;
        if (profileToEdit == null) return;

        bool anyChange = false;

        // Try to use public generic Add<T>/TryGet<T>() if we need to add components - but here
        // we mainly want to enable existing ones.
        IList componentsList = GetComponentsList(profileToEdit);

        if (componentsList == null)
        {
            // If components list isn't accessible, attempt to call common TryGet<T> to locate components
            Debug.Log("VolumeProfile.components not accessible. We will try to enable using TryGet<T> for common types.");\

            foreach (var tn in requiredTypeNames)
            {
                var t = FindTypeByName(tn);
                if (t == null) continue;

                var tryGet = typeof(VolumeProfile).GetMethods()
                    .FirstOrDefault(m => m.IsGenericMethod && m.Name == "TryGet");
                if (tryGet != null)
                {
                    try
                    {
                        var gm = tryGet.MakeGenericMethod(t);
                        var args = new object[] { null };
                        var got = (bool)gm.Invoke(profileToEdit, args);
                        if (got && args[0] is VolumeComponent comp)
                        {
                            if (EnableComponentAndParams(comp)) anyChange = true;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"TryGet<{t.Name}> failed: {e.Message}");
                    }
                }
            }
        }
        else
        {
            // iterate the accessible components list
            for (int i = 0; i < componentsList.Count; i++)
            {
                var cobj = componentsList[i];
                if (cobj == null) continue;
                if (cobj is VolumeComponent comp)
                {
                    if (EnableComponentAndParams(comp)) anyChange = true;
                }
                else
                {
                    // If the list yields ScriptableObjects or raw objects, attempt to treat them as VolumeComponent via reflection
                    var compType = cobj.GetType();
                    if (typeof(VolumeComponent).IsAssignableFrom(compType))
                    {
                        var vc = cobj as VolumeComponent;
                        if (vc != null && EnableComponentAndParams(vc)) anyChange = true;
                    }
                }
            }
        }

        if (anyChange)
        {
            EditorUtility.SetDirty(profileToEdit);
            if (AssetDatabase.Contains(profileToEdit))
                AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(vol.gameObject.scene);
            Debug.Log($"ForceEnableAllVolumeOverrides: enabled overrides on profile '{profileToEdit.name}'.");
        }
        else
        {
            Debug.Log($"ForceEnableAllVolumeOverrides: nothing changed for profile '{profileToEdit.name}'.");
        }
    }

    // Enable a component and all VolumeParameter fields/properties inside it. Returns true if any change made.
    private static bool EnableComponentAndParams(VolumeComponent comp)
    {
        bool changed = false;
        if (comp == null) return false;

        Undo.RecordObject(comp, "Enable Volume Component & Parameters");

        try
        {
            // attempt to set active
            var activeProp = comp.GetType().GetProperty("active", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (activeProp != null && activeProp.CanWrite)
            {
                var cur = (bool)activeProp.GetValue(comp);
                if (!cur) { activeProp.SetValue(comp, true); changed = true; }
            }
            else
            {
                // try field
                var activeField = comp.GetType().GetField("active", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (activeField != null && activeField.FieldType == typeof(bool))
                {
                    var cur = (bool)activeField.GetValue(comp);
                    if (!cur) { activeField.SetValue(comp, true); changed = true; }
                }
            }
        }
        catch { /* ignore */ }

        // Fields
        var fields = comp.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var f in fields)
        {
            try
            {
                if (!typeof(VolumeParameter).IsAssignableFrom(f.FieldType)) continue;
                var vp = f.GetValue(comp) as VolumeParameter;
                if (vp == null) continue;
                if (!vp.overrideState)
                {
                    vp.overrideState = true;
                    changed = true;
                }
            }
            catch { }
        }

        // Properties
        var props = comp.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var p in props)
        {
            try
            {
                if (!p.CanRead) continue;
                if (!typeof(VolumeParameter).IsAssignableFrom(p.PropertyType)) continue;
                var vp = p.GetValue(comp, null) as VolumeParameter;
                if (vp == null) continue;
                if (!vp.overrideState)
                {
                    vp.overrideState = true;
                    changed = true;
                }
            }
            catch { }
        }

        if (changed) EditorUtility.SetDirty(comp);
        return changed;
    }

    // Reflection helpers
    private static IList GetComponentsList(VolumeProfile profile)
    {
        var t = typeof(VolumeProfile);
        var fi = t.GetField("components", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fi != null) return fi.GetValue(profile) as IList;
        var pi = t.GetProperty("components", BindingFlags.Public | BindingFlags.Instance);
        if (pi != null) return pi.GetValue(profile) as IList;
        return null;
    }

    private static Type FindTypeByName(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try { var t = asm.GetType(fullName, false); if (t != null) return t; } catch { }
        }
        return null;
    }
}
#endif
