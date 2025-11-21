// PostProcessPresetEditor.cs
// Replace your existing file with this version.
// Editor for PostProcessPreset: preview, apply (instant/permanent with Undo), and clean inspector layout.

using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[CustomEditor(typeof(PostProcessPreset))]
public class PostProcessPresetEditor : Editor
{
    // Target
    private PostProcessPreset preset;
    private SerializedObject so;

    // Serialized properties (attempt to bind; if not found the GUI falls back to reflection)
    private SerializedProperty sp_useColorAdjustments, sp_colorTint, sp_saturation;
    private SerializedProperty sp_useBloom, sp_bloomIntensity, sp_bloomThreshold;
    private SerializedProperty sp_useVignette, sp_vignetteIntensity;
    private SerializedProperty sp_useChromatic, sp_chromaticIntensity;
    private SerializedProperty sp_useFilmGrain, sp_filmGrainIntensity;
    private SerializedProperty sp_useLensDistortion, sp_lensDistortionIntensity;
    private SerializedProperty sp_useMotionBlur, sp_motionBlurIntensity;
    private SerializedProperty sp_usePanini, sp_paniniDistance;
    private SerializedProperty sp_useDOF, sp_dofFocusDistance;
    private SerializedProperty sp_useWhiteBalance, sp_whiteBalanceTemperature, sp_whiteBalanceTint;
    private SerializedProperty sp_useLiftGammaGain, sp_lift, sp_gamma, sp_gain;
    private SerializedProperty sp_useSMH, sp_shadows, sp_midtones, sp_highlights;

    // UI state
    private bool foldColors = true;
    private bool foldBloom = true;
    private bool foldVfx = true;
    private bool foldColorGrading = true;

    // Preview / Volume state
    private Volume previewVolume;
    private VolumeProfile originalProfile;
    private VolumeProfile tempProfile;
    private int volumeSelectionIndex = -1;
    private Volume[] sceneVolumes = new Volume[0];

    // Safety: reflection cached fields for fallback
    private FieldInfo fi_colorTint, fi_saturation;
    private FieldInfo fi_bloomIntensity, fi_bloomThreshold;
    private FieldInfo fi_vignetteIntensity;
    // (You can cache more if your preset evolves)

    private void OnEnable()
    {
        preset = target as PostProcessPreset;
        so = serializedObject;

        // Bind serialized properties — names must match your PostProcessPreset fields.
        // We attempt to FindProperty and if not present we gracefully continue.
        sp_useColorAdjustments = FindProp("useColorAdjustments");
        sp_colorTint = FindProp("colorTint");
        sp_saturation = FindProp("saturation");

        sp_useBloom = FindProp("useBloom");
        sp_bloomIntensity = FindProp("bloomIntensity");
        sp_bloomThreshold = FindProp("bloomThreshold");

        sp_useVignette = FindProp("useVignette");
        sp_vignetteIntensity = FindProp("vignetteIntensity");

        sp_useChromatic = FindProp("useChromatic");
        sp_chromaticIntensity = FindProp("chromaticIntensity");

        sp_useFilmGrain = FindProp("useFilmGrain");
        sp_filmGrainIntensity = FindProp("filmGrainIntensity");

        sp_useLensDistortion = FindProp("useLensDistortion");
        sp_lensDistortionIntensity = FindProp("lensDistortionIntensity");

        sp_useMotionBlur = FindProp("useMotionBlur");
        sp_motionBlurIntensity = FindProp("motionBlurIntensity");

        sp_usePanini = FindProp("usePanini");
        sp_paniniDistance = FindProp("paniniDistance");

        sp_useDOF = FindProp("useDOF");
        sp_dofFocusDistance = FindProp("dofFocusDistance");

        sp_useWhiteBalance = FindProp("useWhiteBalance");
        sp_whiteBalanceTemperature = FindProp("whiteBalanceTemperature");
        sp_whiteBalanceTint = FindProp("whiteBalanceTint");

        sp_useLiftGammaGain = FindProp("useLiftGammaGain");
        sp_lift = FindProp("lift");
        sp_gamma = FindProp("gamma");
        sp_gain = FindProp("gain");

        sp_useSMH = FindProp("useSMH");
        sp_shadows = FindProp("shadows");
        sp_midtones = FindProp("midtones");
        sp_highlights = FindProp("highlights");

        // Reflection fallbacks (cache) for fields that might not be serialized as properties
        var t = preset?.GetType();
        if (t != null)
        {
            fi_colorTint = t.GetField("colorTint", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            fi_saturation = t.GetField("saturation", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            fi_bloomIntensity = t.GetField("bloomIntensity", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            fi_bloomThreshold = t.GetField("bloomThreshold", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            fi_vignetteIntensity = t.GetField("vignetteIntensity", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }

        RefreshVolumeList();
    }

    public override void OnInspectorGUI()
    {
        so.Update();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("PostProcess Preset", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Preview & apply presets to a Volume in scene (non-destructive preview + Undo-aware permanent apply).", EditorStyles.miniLabel);
        EditorGUILayout.Space();

        DrawVolumePreviewControls();
        EditorGUILayout.Space();

        // Foldouts for sections; draw only when the section toggle/property exists.
        DrawColorAdjustmentsSection();
        DrawBloomSection();
        DrawVfxSection();
        DrawColorGradingSection();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Preview creates a temporary duplicate of the selected VolumeProfile and assigns it to the Volume (non-destructive). Use Revert Preview to restore the original profile. Use Apply Permanently to write changes to the original profile (Undo supported).", MessageType.None);

        so.ApplyModifiedProperties();
    }

    private void DrawVolumePreviewControls()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Preview Target", EditorStyles.boldLabel);

        // Top row actions
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh Volumes", GUILayout.Width(130)))
        {
            RefreshVolumeList();
        }
        if (GUILayout.Button("Find First Global", GUILayout.Width(130)))
        {
            FindFirstGlobalVolume();
        }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Ping Preset", GUILayout.Width(90)))
        {
            EditorGUIUtility.PingObject(preset);
        }
        EditorGUILayout.EndHorizontal();

        // Volume popup
        string[] names = sceneVolumes.Select(v => v.gameObject.name + " (" + v.gameObject.scene.name + ")").ToArray();
        if (names.Length == 0)
        {
            EditorGUILayout.HelpBox("No Volume found in opened scenes. Add a Volume to a GameObject to preview.", MessageType.Info);
        }
        else
        {
            // keep index safe
            if (volumeSelectionIndex < 0 || volumeSelectionIndex >= names.Length) volumeSelectionIndex = 0;
            int newIndex = EditorGUILayout.Popup("Preview Volume", volumeSelectionIndex, names);
            if (newIndex != volumeSelectionIndex)
            {
                volumeSelectionIndex = newIndex;
                previewVolume = sceneVolumes[volumeSelectionIndex];
            }

            EditorGUILayout.ObjectField("Preview Volume (scene)", previewVolume, typeof(Volume), true);
        }

        // Action buttons
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Preview", GUILayout.Height(28)))
        {
            DoPreview();
        }
        if (GUILayout.Button("Revert Preview", GUILayout.Height(28)))
        {
            RevertPreview();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply Instant (to selected Volume)", GUILayout.Height(24)))
        {
            ApplyInstantToSelectedVolume();
        }
        if (GUILayout.Button("Apply Permanently (with Undo)", GUILayout.Height(24)))
        {
            ApplyPermanentToSelectedVolume();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawColorAdjustmentsSection()
    {
        foldColors = EditorGUILayout.Foldout(foldColors, "Color Adjustments", true);
        if (!foldColors) return;

        EditorGUI.indentLevel++;
        if (sp_useColorAdjustments != null)
            EditorGUILayout.PropertyField(sp_useColorAdjustments, new GUIContent("Enable Color Adjustments"));
        else
            DrawBoolFieldFallback("useColorAdjustments", "Enable Color Adjustments");

        // If enabled (via serialized prop or reflection), draw inner fields
        bool enabled = GetBoolValue(sp_useColorAdjustments, "useColorAdjustments");
        if (enabled)
        {
            EditorGUI.indentLevel++;
            // Color Tint
            if (sp_colorTint != null)
                EditorGUILayout.PropertyField(sp_colorTint, new GUIContent("Color Tint"));
            else
                DrawColorFieldFallback("colorTint", "Color Tint");

            // Saturation
            if (sp_saturation != null)
                EditorGUILayout.PropertyField(sp_saturation, new GUIContent("Saturation"));
            else
                DrawFloatFieldFallback("saturation", "Saturation");

            EditorGUI.indentLevel--;
        }
        EditorGUI.indentLevel--;
    }

    private void DrawBloomSection()
    {
        foldBloom = EditorGUILayout.Foldout(foldBloom, "Bloom", true);
        if (!foldBloom) return;

        EditorGUI.indentLevel++;
        if (sp_useBloom != null)
            EditorGUILayout.PropertyField(sp_useBloom, new GUIContent("Enable Bloom"));
        else
            DrawBoolFieldFallback("useBloom", "Enable Bloom");

        bool enabled = GetBoolValue(sp_useBloom, "useBloom");
        if (enabled)
        {
            EditorGUI.indentLevel++;
            if (sp_bloomIntensity != null)
                EditorGUILayout.PropertyField(sp_bloomIntensity, new GUIContent("Intensity"));
            else
                DrawFloatFieldFallback("bloomIntensity", "Intensity");

            if (sp_bloomThreshold != null)
                EditorGUILayout.PropertyField(sp_bloomThreshold, new GUIContent("Threshold"));
            else
                DrawFloatFieldFallback("bloomThreshold", "Threshold");

            EditorGUI.indentLevel--;
        }
        EditorGUI.indentLevel--;
    }

    private void DrawVfxSection()
    {
        foldVfx = EditorGUILayout.Foldout(foldVfx, "VFX", true);
        if (!foldVfx) return;

        EditorGUI.indentLevel++;

        // Vignette
        if (sp_useVignette != null)
            EditorGUILayout.PropertyField(sp_useVignette, new GUIContent("Vignette"));
        else
            DrawBoolFieldFallback("useVignette", "Vignette");

        if (GetBoolValue(sp_useVignette, "useVignette"))
        {
            EditorGUI.indentLevel++;
            if (sp_vignetteIntensity != null)
                EditorGUILayout.PropertyField(sp_vignetteIntensity, new GUIContent("Intensity"));
            else
                DrawFloatFieldFallback("vignetteIntensity", "Intensity");
            EditorGUI.indentLevel--;
        }

        // Chromatic Aberration
        if (sp_useChromatic != null)
            EditorGUILayout.PropertyField(sp_useChromatic, new GUIContent("Chromatic Aberration"));
        else
            DrawBoolFieldFallback("useChromatic", "Chromatic Aberration");

        if (GetBoolValue(sp_useChromatic, "useChromatic"))
        {
            EditorGUI.indentLevel++;
            if (sp_chromaticIntensity != null)
                EditorGUILayout.PropertyField(sp_chromaticIntensity, new GUIContent("Intensity"));
            else
                DrawFloatFieldFallback("chromaticIntensity", "Intensity");
            EditorGUI.indentLevel--;
        }

        // Film Grain
        if (sp_useFilmGrain != null)
            EditorGUILayout.PropertyField(sp_useFilmGrain, new GUIContent("Film Grain"));
        else
            DrawBoolFieldFallback("useFilmGrain", "Film Grain");

        if (GetBoolValue(sp_useFilmGrain, "useFilmGrain"))
        {
            EditorGUI.indentLevel++;
            if (sp_filmGrainIntensity != null)
                EditorGUILayout.PropertyField(sp_filmGrainIntensity, new GUIContent("Intensity"));
            else
                DrawFloatFieldFallback("filmGrainIntensity", "Intensity");
            EditorGUI.indentLevel--;
        }

        // Lens Distortion
        if (sp_useLensDistortion != null)
            EditorGUILayout.PropertyField(sp_useLensDistortion, new GUIContent("Lens Distortion"));
        else
            DrawBoolFieldFallback("useLensDistortion", "Lens Distortion");

        if (GetBoolValue(sp_useLensDistortion, "useLensDistortion"))
        {
            EditorGUI.indentLevel++;
            if (sp_lensDistortionIntensity != null)
                EditorGUILayout.PropertyField(sp_lensDistortionIntensity, new GUIContent("Intensity"));
            else
                DrawFloatFieldFallback("lensDistortionIntensity", "Intensity");
            EditorGUI.indentLevel--;
        }

        // Motion Blur
        if (sp_useMotionBlur != null)
            EditorGUILayout.PropertyField(sp_useMotionBlur, new GUIContent("Motion Blur"));
        else
            DrawBoolFieldFallback("useMotionBlur", "Motion Blur");

        if (GetBoolValue(sp_useMotionBlur, "useMotionBlur"))
        {
            EditorGUI.indentLevel++;
            if (sp_motionBlurIntensity != null)
                EditorGUILayout.PropertyField(sp_motionBlurIntensity, new GUIContent("Intensity"));
            else
                DrawFloatFieldFallback("motionBlurIntensity", "Intensity");
            EditorGUI.indentLevel--;
        }

        // Panini
        if (sp_usePanini != null)
            EditorGUILayout.PropertyField(sp_usePanini, new GUIContent("Panini Projection"));
        else
            DrawBoolFieldFallback("usePanini", "Panini Projection");

        if (GetBoolValue(sp_usePanini, "usePanini"))
        {
            EditorGUI.indentLevel++;
            if (sp_paniniDistance != null)
                EditorGUILayout.PropertyField(sp_paniniDistance, new GUIContent("Distance"));
            else
                DrawFloatFieldFallback("paniniDistance", "Distance");
            EditorGUI.indentLevel--;
        }

        EditorGUI.indentLevel--;
    }

    private void DrawColorGradingSection()
    {
        foldColorGrading = EditorGUILayout.Foldout(foldColorGrading, "Color Grading / Tonal", true);
        if (!foldColorGrading) return;

        EditorGUI.indentLevel++;

        // DOF
        if (sp_useDOF != null)
            EditorGUILayout.PropertyField(sp_useDOF, new GUIContent("Depth of Field"));
        else
            DrawBoolFieldFallback("useDOF", "Depth of Field");

        if (GetBoolValue(sp_useDOF, "useDOF"))
        {
            EditorGUI.indentLevel++;
            if (sp_dofFocusDistance != null)
                EditorGUILayout.PropertyField(sp_dofFocusDistance, new GUIContent("Focus Distance"));
            else
                DrawFloatFieldFallback("dofFocusDistance", "Focus Distance");
            EditorGUI.indentLevel--;
        }

        // White Balance
        if (sp_useWhiteBalance != null)
            EditorGUILayout.PropertyField(sp_useWhiteBalance, new GUIContent("White Balance"));
        else
            DrawBoolFieldFallback("useWhiteBalance", "White Balance");

        if (GetBoolValue(sp_useWhiteBalance, "useWhiteBalance"))
        {
            EditorGUI.indentLevel++;
            if (sp_whiteBalanceTemperature != null)
                EditorGUILayout.PropertyField(sp_whiteBalanceTemperature, new GUIContent("Temperature"));
            else
                DrawFloatFieldFallback("whiteBalanceTemperature", "Temperature");
            if (sp_whiteBalanceTint != null)
                EditorGUILayout.PropertyField(sp_whiteBalanceTint, new GUIContent("Tint"));
            else
                DrawFloatFieldFallback("whiteBalanceTint", "Tint");
            EditorGUI.indentLevel--;
        }

        // Lift/Gamma/Gain
        if (sp_useLiftGammaGain != null)
            EditorGUILayout.PropertyField(sp_useLiftGammaGain, new GUIContent("Lift / Gamma / Gain"));
        else
            DrawBoolFieldFallback("useLiftGammaGain", "Lift / Gamma / Gain");

        if (GetBoolValue(sp_useLiftGammaGain, "useLiftGammaGain"))
        {
            EditorGUI.indentLevel++;
            if (sp_lift != null)
                EditorGUILayout.PropertyField(sp_lift, new GUIContent("Lift"));
            else
                DrawColorFieldFallback("lift", "Lift");
            if (sp_gamma != null)
                EditorGUILayout.PropertyField(sp_gamma, new GUIContent("Gamma"));
            else
                DrawColorFieldFallback("gamma", "Gamma");
            if (sp_gain != null)
                EditorGUILayout.PropertyField(sp_gain, new GUIContent("Gain"));
            else
                DrawColorFieldFallback("gain", "Gain");
            EditorGUI.indentLevel--;
        }

        // Shadows / Midtones / Highlights
        if (sp_useSMH != null)
            EditorGUILayout.PropertyField(sp_useSMH, new GUIContent("Shadows / Midtones / Highlights"));
        else
            DrawBoolFieldFallback("useSMH", "Shadows / Midtones / Highlights");

        if (GetBoolValue(sp_useSMH, "useSMH"))
        {
            EditorGUI.indentLevel++;
            if (sp_shadows != null) EditorGUILayout.PropertyField(sp_shadows, new GUIContent("Shadows"));
            else DrawColorFieldFallback("shadows", "Shadows");
            if (sp_midtones != null) EditorGUILayout.PropertyField(sp_midtones, new GUIContent("Midtones"));
            else DrawColorFieldFallback("midtones", "Midtones");
            if (sp_highlights != null) EditorGUILayout.PropertyField(sp_highlights, new GUIContent("Highlights"));
            else DrawColorFieldFallback("highlights", "Highlights");
            EditorGUI.indentLevel--;
        }

        EditorGUI.indentLevel--;
    }

    // ---------- Preview / Apply logic ----------

    private void RefreshVolumeList()
    {
        // Use the simplest compatible approach to find Volumes in open scenes
        sceneVolumes = FindObjectsByType<Volume>(FindObjectsSortMode.None).Where(v => v != null).ToArray();
        if (sceneVolumes.Length > 0)
        {
            if (volumeSelectionIndex < 0 || volumeSelectionIndex >= sceneVolumes.Length) volumeSelectionIndex = 0;
            previewVolume = sceneVolumes[volumeSelectionIndex];
        }
        else
        {
            previewVolume = null;
            volumeSelectionIndex = -1;
        }
    }

    private void FindFirstGlobalVolume()
    {
        RefreshVolumeList();
        previewVolume = sceneVolumes.FirstOrDefault(v => v.isGlobal);
        if (previewVolume != null)
        {
            volumeSelectionIndex = Array.IndexOf(sceneVolumes, previewVolume);
            EditorUtility.SetDirty(previewVolume);
        }
    }

    private void DoPreview()
    {
        if (previewVolume == null)
        {
            EditorUtility.DisplayDialog("Preview failed", "No Volume selected for preview. Create a Volume in the scene first.", "OK");
            return;
        }

        // Store originalProfile on first preview
        if (originalProfile == null)
        {
            originalProfile = previewVolume.profile;
            if (originalProfile == null)
            {
                EditorUtility.DisplayDialog("Preview failed", "Selected Volume has no Profile assigned.", "OK");
                return;
            }
            tempProfile = Instantiate(originalProfile);
            tempProfile.name = originalProfile.name + " (Preview)";
            previewVolume.profile = tempProfile;
        }
        else if (tempProfile == null)
        {
            // defensive: ensure temp exists if originalProfile existed
            tempProfile = Instantiate(originalProfile);
            tempProfile.name = originalProfile.name + " (Preview)";
            previewVolume.profile = tempProfile;
        }

        // Apply preset fully to tempProfile
        ApplyPresetToProfile(tempProfile, preset, 1f);

        // mark things dirty
        EditorUtility.SetDirty(tempProfile);
        EditorSceneManager.MarkSceneDirty(previewVolume.gameObject.scene);
    }

    private void RevertPreview()
    {
        if (previewVolume == null || originalProfile == null) return;

        // reassign original and destroy temp
        previewVolume.profile = originalProfile;
        if (tempProfile != null)
        {
            DestroyImmediate(tempProfile, true);
        }
        tempProfile = null;
        originalProfile = null;
        EditorSceneManager.MarkSceneDirty(previewVolume.gameObject.scene);
    }

    private void ApplyInstantToSelectedVolume()
    {
        if (previewVolume == null)
        {
            EditorUtility.DisplayDialog("Apply failed", "No Volume selected. Choose a Volume to apply to.", "OK");
            return;
        }

        var targetProfile = previewVolume.profile;
        if (targetProfile == null)
        {
            EditorUtility.DisplayDialog("Apply failed", "Selected Volume has no profile assigned.", "OK");
            return;
        }

        // Non-undo instant apply (useful for quick testing)
        ApplyPresetToProfile(targetProfile, preset, 1f);
        EditorUtility.SetDirty(targetProfile);
        if (AssetDatabase.Contains(targetProfile))
            AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(previewVolume.gameObject.scene);
    }

    private void ApplyPermanentToSelectedVolume()
    {
        if (previewVolume == null)
        {
            EditorUtility.DisplayDialog("Apply failed", "No Volume selected. Choose a Volume to apply to.", "OK");
            return;
        }

        var targetProfile = previewVolume.profile;
        if (targetProfile == null)
        {
            EditorUtility.DisplayDialog("Apply failed", "Selected Volume has no profile assigned.", "OK");
            return;
        }

        // Confirm
        if (!EditorUtility.DisplayDialog("Apply Preset Permanently", $"Apply preset '{preset.name}' permanently to profile '{targetProfile.name}'? You can Undo (Ctrl+Z).", "Apply", "Cancel"))
            return;

        // Record undo on the profile asset (or instance) so user can revert
        Undo.RecordObject(targetProfile, $"Apply Preset {preset.name}");
        ApplyPresetToProfile(targetProfile, preset, 1f);
        EditorUtility.SetDirty(targetProfile);

        // If profile is an Asset in project, save changes
        if (AssetDatabase.Contains(targetProfile))
        {
            AssetDatabase.SaveAssets();
        }

        EditorSceneManager.MarkSceneDirty(previewVolume.gameObject.scene);
    }

    // Core: apply preset values directly onto the provided VolumeProfile (blend 0..1)
    private void ApplyPresetToProfile(VolumeProfile profile, PostProcessPreset p, float blend)
    {
        if (profile == null || p == null) return;

        // Utility local to attempt to get a component safely
        bool TryGet<T>(out T component) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out component)) return true;
            component = null;
            return false;
        }

        try
        {
            // Color Adjustments
            if (p.useColorAdjustments && TryGet<ColorAdjustments>(out var ca))
            {
                ca.colorFilter.value = Color.Lerp(ca.colorFilter.value, p.colorTint, blend);
                ca.saturation.value = Mathf.Lerp(ca.saturation.value, p.saturation, blend);
                EditorUtility.SetDirty(ca);
            }

            // Bloom
            if (p.useBloom && TryGet<Bloom>(out var bloom))
            {
                // Some URP versions expose intensity/threshold differently; we assume common API
                bloom.intensity.value = Mathf.Lerp(bloom.intensity.value, p.bloomIntensity, blend);
                bloom.threshold.value = Mathf.Lerp(bloom.threshold.value, p.bloomThreshold, blend);
                EditorUtility.SetDirty(bloom);
            }

            // Vignette
            if (p.useVignette && TryGet<Vignette>(out var vin))
            {
                vin.intensity.value = Mathf.Lerp(vin.intensity.value, p.vignetteIntensity, blend);
                EditorUtility.SetDirty(vin);
            }

            // Chromatic
            if (p.useChromatic && TryGet<ChromaticAberration>(out var chr))
            {
                chr.intensity.value = Mathf.Lerp(chr.intensity.value, p.chromaticIntensity, blend);
                EditorUtility.SetDirty(chr);
            }

            // Film grain
            if (p.useFilmGrain && TryGet<FilmGrain>(out var fg))
            {
                fg.intensity.value = Mathf.Lerp(fg.intensity.value, p.filmGrainIntensity, blend);
                EditorUtility.SetDirty(fg);
            }

            // Lens Distortion
            if (p.useLensDistortion && TryGet<LensDistortion>(out var ld))
            {
                ld.intensity.value = Mathf.Lerp(ld.intensity.value, p.lensDistortionIntensity, blend);
                EditorUtility.SetDirty(ld);
            }

            // Motion Blur (URP versions vary; if missing, it's skipped)
            if (p.useMotionBlur)
            {
                if (TryGet<UnityEngine.Rendering.Universal.MotionBlur>(out var mb))
                {
                    // some MotionBlur components use different property names; try common one
                    mb.intensity.value = Mathf.Lerp(mb.intensity.value, p.motionBlurIntensity, blend);
                    EditorUtility.SetDirty(mb);
                }
            }

            // Panini Projection
            if (p.usePanini && TryGet<PaniniProjection>(out var pp))
            {
                pp.distance.value = Mathf.Lerp(pp.distance.value, p.paniniDistance, blend);
                EditorUtility.SetDirty(pp);
            }

            // DOF
            if (p.useDOF && TryGet<DepthOfField>(out var dof))
            {
                dof.focusDistance.value = Mathf.Lerp(dof.focusDistance.value, p.dofFocusDistance, blend);
                EditorUtility.SetDirty(dof);
            }

            // White Balance
            if (p.useWhiteBalance && TryGet<WhiteBalance>(out var wb))
            {
                wb.temperature.value = Mathf.Lerp(wb.temperature.value, p.whiteBalanceTemperature, blend);
                wb.tint.value = Mathf.Lerp(wb.tint.value, p.whiteBalanceTint, blend);
                EditorUtility.SetDirty(wb);
            }

            // Lift/Gamma/Gain
            if (p.useLiftGammaGain && TryGet<LiftGammaGain>(out var lgg))
            {
                lgg.lift.value = Color.Lerp(lgg.lift.value, p.lift, blend);
                lgg.gamma.value = Color.Lerp(lgg.gamma.value, p.gamma, blend);
                lgg.gain.value = Color.Lerp(lgg.gain.value, p.gain, blend);
                EditorUtility.SetDirty(lgg);
            }

            // Shadows / Midtones / Highlights
            if (p.useSMH && TryGet<ShadowsMidtonesHighlights>(out var smh))
            {
                smh.shadows.value = Color.Lerp(smh.shadows.value, p.shadows, blend);
                smh.midtones.value = Color.Lerp(smh.midtones.value, p.midtones, blend);
                smh.highlights.value = Color.Lerp(smh.highlights.value, p.highlights, blend);
                EditorUtility.SetDirty(smh);
            }
        }
        catch (Exception e)
        {
            // Catch and log to avoid breaking editor if APIs differ between URP versions
            Debug.LogWarning($"ApplyPresetToProfile encountered an issue: {e.Message}");
        }
    }

    private void OnDisable()
    {
        // Clean up temporary profile created for preview (so scenes are not left altered)
        if (previewVolume != null && tempProfile != null && originalProfile != null)
        {
            previewVolume.profile = originalProfile;
            DestroyImmediate(tempProfile, true);
        }
        originalProfile = null;
        tempProfile = null;
    }

    // ---------- Helpers & fallbacks ----------

    private SerializedProperty FindProp(string name)
    {
        try
        {
            return so.FindProperty(name);
        }
        catch { return null; }
    }

    private bool GetBoolValue(SerializedProperty prop, string fieldName)
    {
        if (prop != null) return prop.boolValue;
        // reflection fallback
        if (preset == null) return false;
        var f = preset.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null && f.FieldType == typeof(bool))
            return (bool)f.GetValue(preset);
        return false;
    }

    private void DrawBoolFieldFallback(string fieldName, string label)
    {
        if (so == null || preset == null) return;
        var f = preset.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null && f.FieldType == typeof(bool))
        {
            bool val = (bool)f.GetValue(preset);
            bool newVal = EditorGUILayout.Toggle(label, val);
            if (newVal != val)
            {
                Undo.RecordObject(preset, $"Edit {label}");
                f.SetValue(preset, newVal);
                EditorUtility.SetDirty(preset);
            }
        }
    }

    private void DrawColorFieldFallback(string fieldName, string label)
    {
        if (so == null || preset == null) return;
        var f = preset.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null && f.FieldType == typeof(Color))
        {
            Color val = (Color)f.GetValue(preset);
            Color newVal = EditorGUILayout.ColorField(label, val);
            if (newVal != val)
            {
                Undo.RecordObject(preset, $"Edit {label}");
                f.SetValue(preset, newVal);
                EditorUtility.SetDirty(preset);
            }
        }
    }

    private void DrawFloatFieldFallback(string fieldName, string label)
    {
        if (so == null || preset == null) return;
        var f = preset.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null && (f.FieldType == typeof(float) || f.FieldType == typeof(double)))
        {
            float val = Convert.ToSingle(f.GetValue(preset));
            float newVal = EditorGUILayout.FloatField(label, val);
            if (!Mathf.Approximately(newVal, val))
            {
                Undo.RecordObject(preset, $"Edit {label}");
                f.SetValue(preset, Convert.ChangeType(newVal, f.FieldType));
                EditorUtility.SetDirty(preset);
            }
        }
    }
}
