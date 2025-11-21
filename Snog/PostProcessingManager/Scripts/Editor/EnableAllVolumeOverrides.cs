#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Snog.PostProcessingManager.Runtime;

namespace Snog.PostProcessingManager.Editor
{
    public static class EnableAllVolumeOverrides
    {
        private static readonly string[] requiredTypeNames = PostProcessOverrideConfig.RequiredTypeNames;

        [MenuItem("Snog/PostProcessManager/Volumes/Ensure Overrides For Selected Volume")]
        public static void EnsureOverridesForSelectedVolume()
        {
            var go = Selection.activeGameObject;
            if (go == null) { Debug.LogWarning("Select a GameObject with a Volume."); return; }
            var vol = go.GetComponent<Volume>();
            if (vol == null) { Debug.LogWarning("Selected object doesn't have a Volume component."); return; }

            EnsureProfile(vol);
            if (vol.profile == null) return;

            EnsureOverridesOnProfile(vol.profile, vol.gameObject.scene.path);
            EditorSceneManager.MarkSceneDirty(vol.gameObject.scene);
        }

        // ensure the volume has a profile (instance profile)
        private static void EnsureProfile(Volume vol)
        {
            if (vol.profile != null) return;
            Undo.RecordObject(vol, "Assign VolumeProfile");
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = vol.gameObject.name + " Profile (Auto)";
            vol.profile = profile;
            EditorUtility.SetDirty(vol);
        }

        public static void EnsureOverridesOnProfile(VolumeProfile profile, string scenePathForLog = null)
        {
            if (profile == null) return;

            bool anyChange = false;

            // Reflection helpers for generic methods on VolumeProfile
            MethodInfo genericAddMethod = GetGenericMethod(profile.GetType(), "Add");
            MethodInfo genericHasMethod = GetGenericMethod(profile.GetType(), "Has");
            MethodInfo genericTryGetMethod = GetGenericMethod(profile.GetType(), "TryGet");

            foreach (var typeName in requiredTypeNames)
            {
                var t = FindTypeByName(typeName);
                if (t == null) continue;

                bool has = false;
                object existing = null;

                // Try Has<T>()
                if (genericHasMethod != null)
                {
                    try
                    {
                        var hasGeneric = genericHasMethod.MakeGenericMethod(t);
                        has = (bool)hasGeneric.Invoke(profile, null);
                    }
                    catch { has = false; }
                }

                // Try TryGet<T>(out T)
                if (!has && genericTryGetMethod != null)
                {
                    try
                    {
                        var tryGet = genericTryGetMethod.MakeGenericMethod(t);
                        var parameters = new object[] { null };
                        bool got = (bool)tryGet.Invoke(profile, parameters);
                        if (got)
                        {
                            existing = parameters[0];
                            has = true;
                        }
                    }
                    catch { /* ignore */ }
                }

                // If generic Add<T>() available and not present, add it
                if (!has && genericAddMethod != null)
                {
                    try
                    {
                        Undo.RecordObject(profile, "Add Volume Override");
                        var addGeneric = genericAddMethod.MakeGenericMethod(t);
                        existing = addGeneric.Invoke(profile, null);
                        anyChange = true;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"EnableAllVolumeOverrides: Add<{t.Name}> failed: {e.Message}");
                    }
                }

                // If generic methods weren't available, fallback to components list
                if (!has && genericAddMethod == null)
                {
                    var componentsList = GetComponentsList(profile);
                    if (componentsList != null)
                    {
                        bool found = false;
                        foreach (var c in componentsList)
                        {
                            if (c == null) continue;
                            var ct = c.GetType();
                            if (ct == t || ct.IsSubclassOf(t)) { found = true; existing = c; break; }
                        }
                        if (!found)
                        {
                            try
                            {
                                Undo.RecordObject(profile, "Add Volume Override (fallback)");
                                var inst = ScriptableObject.CreateInstance(t);
                                componentsList.Add(inst);
                                existing = inst;
                                anyChange = true;
                            }
                            catch (Exception e)
                            {
                                Debug.LogWarning($"EnableAllVolumeOverrides: fallback create {t.Name} failed: {e.Message}");
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning("EnableAllVolumeOverrides: couldn't access VolumeProfile.components and no generic Add<T>() found - aborting fallback for some types.");
                    }
                }

                // Now ensure existing component is active and enable its parameters overrideState
                if (existing is VolumeComponent comp)
                {
                    // record undo for the component
                    Undo.RecordObject(comp, "Enable Volume Component & Parameters");

                    // set active = true if possible
                    try { comp.active = true; } catch { /* ignore */ }

                    // set overrideState on fields that are VolumeParameter
                    var fields = comp.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var f in fields)
                    {
                        if (!typeof(VolumeParameter).IsAssignableFrom(f.FieldType)) continue;
                        if (f.GetValue(comp) is VolumeParameter vp)
                        {
                            vp.overrideState = true;
                            anyChange = true;
                        }
                    }

                    // Also check properties
                    var props = comp.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var p in props)
                    {
                        if (!p.CanRead) continue;
                        if (!typeof(VolumeParameter).IsAssignableFrom(p.PropertyType)) continue;
                        try
                        {
                            if (p.GetValue(comp) is VolumeParameter vp2)
                            {
                                vp2.overrideState = true;
                                anyChange = true;
                            }
                        }
                        catch { }
                    }

                    EditorUtility.SetDirty(comp);
                }
            } // foreach type

            if (anyChange)
            {
                EditorUtility.SetDirty(profile);
                if (AssetDatabase.Contains(profile))
                    AssetDatabase.SaveAssets();

                Debug.Log($"EnsureOverridesOnProfile: ensured overrides on '{profile.name}'{(scenePathForLog!=null ? $" in scene {scenePathForLog}" : "")}.");
            }
            else
            {
                Debug.Log($"EnsureOverridesOnProfile: nothing to change for profile '{profile.name}'.");
            }
        }

        // --------- Reflection helpers ----------

        private static MethodInfo GetGenericMethod(Type type, string name)
        {
            // find a generic method with the given name and one generic argument
            return type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.IsGenericMethod && m.Name == name && m.GetGenericArguments().Length == 1);
        }

        private static Type FindTypeByName(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = asm.GetType(fullName, false);
                    if (t != null) return t;
                }
                catch { }
            }
            return null;
        }

        // Try to get the internal components list if present (fallback)
        private static IList GetComponentsList(VolumeProfile profile)
        {
            var t = typeof(VolumeProfile);
            var fi = t.GetField("components", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fi != null) return fi.GetValue(profile) as IList;
            var pi = t.GetProperty("components", BindingFlags.Public | BindingFlags.Instance);
            if (pi != null) return pi.GetValue(profile) as IList;
            return null;
        }
    }
    #endif
}