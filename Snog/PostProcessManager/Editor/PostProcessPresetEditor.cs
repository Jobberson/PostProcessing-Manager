// Place this file under an Editor folder: Assets/.../Editor/PostProcessPresetEditor.cs

using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[CustomEditor(typeof(PostProcessPreset))]
public class PostProcessPresetEditor : Editor
{
    private PostProcessPreset preset;
    private SerializedObject so;

    // Serialized properties (matches your preset fields)
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

    // Editor UX state
    private bool foldColors = true;
    private bool foldBloom = true;
    private bool foldVfx = true;
    private bool foldColorGrading = true;

    // Preview state
    private Volume previewVolume;
    private VolumeProfile originalProfile;
    private VolumeProfile tempProfile;
    private int volumeSelectionIndex = -1;
    private Volume[] sceneVolumes = new Volume[0];

    private void OnEnable()
    {
        preset = (PostProcessPreset)target;
        so = serializedObject;

        // Bind properties (names must match your PostProcessPreset fields)
        sp_useColorAdjustments = so.FindProperty("useColorAdjustments");
        sp_colorTint = so.FindProperty("colorTint");
        sp_saturation = so.FindProperty("saturation");

        sp_useBloom = so.FindProperty("useBloom");
        sp_bloomIntensity = so.FindProperty("bloomIntensity");
        sp_bloomThreshold = so.FindProperty("bloomThreshold");

        sp_useVignette = so.FindProperty("useVignette");
        sp_vignetteIntensity = so.FindProperty("vignetteIntensity");

        sp_useChromatic = so.FindProperty("useChromatic");
        sp_chromaticIntensity = so.FindProperty("chromaticIntensity");

        sp_useFilmGrain = so.FindProperty("useFilmGrain");
        sp_filmGrainIntensity = so.FindProperty("filmGrainIntensity");

        sp_useLensDistortion = so.FindProperty("useLensDistortion");
        sp_lensDistortionIntensity = so.FindProperty("lensDistortionIntensity");

        sp_useMotionBlur = so.FindProperty("useMotionBlur");
        sp_motionBlurIntensity = so.FindProperty("motionBlurIntensity");

        sp_usePanini = so.FindProperty("usePanini");
        sp_paniniDistance = so.FindProperty("paniniDistance");

        sp_useDOF = so.FindProperty("useDOF");
        sp_dofFocusDistance = so.FindProperty("dofFocusDistance");

        sp_useWhiteBalance = so.FindProperty("useWhiteBalance");
        sp_whiteBalanceTemperature = so.FindProperty("whiteBalanceTemperature");
        sp_whiteBalanceTint = so.FindProperty("whiteBalanceTint");

        sp_useLiftGammaGain = so.FindProperty("useLiftGammaGain");
        sp_lift = so.FindProperty("lift");
        sp_gamma = so.FindProperty("gamma");
        sp_gain = so.FindProperty("gain");

        sp_useSMH = so.FindProperty("useSMH");
        sp_shadows = so.FindProperty("shadows");
        sp_midtones = so.FindProperty("midtones");
        sp_highlights = so.FindProperty("highlights");

        RefreshVolumeList();
    }

    public override void OnInspectorGUI()
    {
        so.Update();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("PostProcess Preset", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Preview & apply presets to a Volume in scene.", EditorStyles.miniLabel);
        EditorGUILayout.Space();

        // Volume selector + preview controls
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Preview Target", EditorStyles.boldLabel);

        // Refresh volumes button
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh Volumes", GUILayout.Width(120)))
        {
            RefreshVolumeList();
        }

        if (GUILayout.Button("Find First Global", GUILayout.Width(120)))
        {
            FindFirstGlobalVolume();
        }
        EditorGUILayout.EndHorizontal();

        // Volume popup
        string[] names = sceneVolumes.Select(v => v.gameObject.name + " (" + v.gameObject.scene.name + ")").ToArray();
        if (names.Length == 0)
        {
            EditorGUILayout.HelpBox("No Volume found in current scenes. Add a Volume to a GameObject to preview.", MessageType.Info);
        }
        else
        {
            if (volumeSelectionIndex < 0 || volumeSelectionIndex >= names.Length) volumeSelectionIndex = 0;
            int newIndex = EditorGUILayout.Popup("Preview Volume", volumeSelectionIndex, names);
            if (newIndex != volumeSelectionIndex)
            {
                volumeSelectionIndex = newIndex;
                previewVolume = sceneVolumes[volumeSelectionIndex];
            }

            // Show currently selected
            EditorGUILayout.ObjectField("Preview Volume (scene)", previewVolume, typeof(Volume), true);
        }

        // Buttons: Preview, Revert, Apply Instant, Apply (permanent with Undo)
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

        EditorGUILayout.Space();

        // Draw foldouts for preset sections (compact UX)
        if (foldColors)
        {
            EditorGUILayout.PropertyField(sp_useColorAdjustments, new GUIContent("Enable"));
            if (sp_useColorAdjustments.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(sp_colorTint, new GUIContent("Color Tint"));
                EditorGUILayout.PropertyField(sp_saturation, new GUIContent("Saturation"));
                EditorGUI.indentLevel--;
            }
        }

        if (foldBloom)
        {
            EditorGUILayout.PropertyField(sp_useBloom, new GUIContent("Enable"));
            if (sp_useBloom.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(sp_bloomIntensity, new GUIContent("Intensity"));
                EditorGUILayout.PropertyField(sp_bloomThreshold, new GUIContent("Threshold"));
                EditorGUI.indentLevel--;
            }
        }

        if (foldVfx)
        {
            // Vignette
            EditorGUILayout.PropertyField(sp_useVignette, new GUIContent("Vignette"));
            if (sp_useVignette.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(sp_vignetteIntensity, new GUIContent("Intensity"));
                EditorGUI.indentLevel--;
            }

            // Chromatic
            EditorGUILayout.PropertyField(sp_useChromatic, new GUIContent("Chromatic Aberration"));
            if (sp_useChromatic.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(sp_chromaticIntensity, new GUIContent("Intensity"));
                EditorGUI.indentLevel--;
            }

            // Film grain
            EditorGUILayout.PropertyField(sp_useFilmGrain, new GUIContent("Film Grain"));
            if (sp_useFilmGrain.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(sp_filmGrainIntensity, new GUIContent("Intensity"));
                EditorGUI.indentLevel--;
            }

            // Lens distortion
            EditorGUILayout.PropertyField(sp_useLensDistortion, new GUIContent("Lens Distortion"));
            if (sp_useLensDistortion.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(sp_lensDistortionIntensity, new GUIContent("Intensity"));
                EditorGUI.indentLevel--;
            }

            // Motion blur
            EditorGUILayout.PropertyField(sp_useMotionBlur, new GUIContent("Motion Blur"));
            if (sp_useMotionBlur.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(sp_motionBlurIntensity, new GUIContent("Intensity"));
                EditorGUI.indentLevel--;
            }

            // Panini
            EditorGUILayout.PropertyField(sp_usePanini, new GUIContent("Panini Projection"));
            if (sp_usePanini.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(sp_paniniDistance, new GUIContent("Distance"));
                EditorGUI.indentLevel--;
            }
        }

        if (foldColorGrading)
        {
            // DOF
            EditorGUILayout.PropertyField(sp_useDOF, new GUIContent("Depth of Field"));
            if (sp_useDOF.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(sp_dofFocusDistance, new GUIContent("Focus Distance"));
                EditorGUI.indentLevel--;
            }

            // White Balance
            EditorGUILayout.PropertyField(sp_useWhiteBalance, new GUIContent("White Balance"));
            if (sp_useWhiteBalance.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(sp_whiteBalanceTemperature, new GUIContent("Temperature"));
                EditorGUILayout.PropertyField(sp_whiteBalanceTint, new GUIContent("Tint"));
                EditorGUI.indentLevel--;
            }

            // Lift/Gamma/Gain
            EditorGUILayout.PropertyField(sp_useLiftGammaGain, new GUIContent("Lift / Gamma / Gain"));
            if (sp_useLiftGammaGain.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(sp_lift, new GUIContent("Lift"));
                EditorGUILayout.PropertyField(sp_gamma, new GUIContent("Gamma"));
                EditorGUILayout.PropertyField(sp_gain, new GUIContent("Gain"));
                EditorGUI.indentLevel--;
            }

            // SMH
            EditorGUILayout.PropertyField(sp_useSMH, new GUIContent("Shadows / Midtones / Highlights"));
            if (sp_useSMH.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(sp_shadows, new GUIContent("Shadows"));
                EditorGUILayout.PropertyField(sp_midtones, new GUIContent("Midtones"));
                EditorGUILayout.PropertyField(sp_highlights, new GUIContent("Highlights"));
                EditorGUI.indentLevel--;
            }
        }
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Preview creates a temporary duplicate of the selected VolumeProfile and assigns it to the Volume (non-destructive). Use Revert Preview to restore the original profile. Use Apply Permanently to write changes to the original profile (Undo supported).", MessageType.None);

        so.ApplyModifiedProperties();
    }

    private void RefreshVolumeList()
    {
        // Find all Volumes in all open scenes
        sceneVolumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
        if (sceneVolumes.Length > 0)
        {
            // Keep selection index valid
            if (volumeSelectionIndex < 0 || volumeSelectionIndex >= sceneVolumes.Length)
            {
                volumeSelectionIndex = 0;
            }
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
            volumeSelectionIndex = System.Array.IndexOf(sceneVolumes, previewVolume);
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

        if (originalProfile == previewVolume.profile) // already previewing but not duplicated
        {
            // Create temp duplicate
            tempProfile = Instantiate(originalProfile);
            tempProfile.name = originalProfile.name + " (Preview)";
            previewVolume.profile = tempProfile;
        }

        // If not yet storing original, capture it
        if (originalProfile == null)
        {
            originalProfile = previewVolume.profile;
            // duplicate original into temp and assign, to avoid modifying the asset
            tempProfile = Instantiate(originalProfile);
            tempProfile.name = originalProfile.name + " (Preview)";
            previewVolume.profile = tempProfile;
        }

        ApplyPresetToProfile(tempProfile, preset, 1f);
        // Mark scene dirty
        EditorSceneManager.MarkSceneDirty(previewVolume.gameObject.scene);
        EditorUtility.SetDirty(tempProfile);
    }

    private void RevertPreview()
    {
        if (previewVolume == null || originalProfile == null) return;

        // Reassign original profile
        previewVolume.profile = originalProfile;

        // Destroy the tempProfile instance in editor
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

        // If preview is active and tempProfile present, write to that profile directly (non-Undo)
        VolumeProfile targetProfile = previewVolume.profile;
        if (targetProfile == null)
        {
            EditorUtility.DisplayDialog("Apply failed", "Selected Volume has no profile assigned.", "OK");
            return;
        }

        ApplyPresetToProfile(targetProfile, preset, 1f);
        EditorUtility.SetDirty(targetProfile);
        EditorSceneManager.MarkSceneDirty(previewVolume.gameObject.scene);
    }

    private void ApplyPermanentToSelectedVolume()
    {
        if (previewVolume == null)
        {
            EditorUtility.DisplayDialog("Apply failed", "No Volume selected. Choose a Volume to apply to.", "OK");
            return;
        }

        VolumeProfile targetProfile = previewVolume.profile;
        if (targetProfile == null)
        {
            EditorUtility.DisplayDialog("Apply failed", "Selected Volume has no profile assigned.", "OK");
            return;
        }

        // Record for undo and apply
        Undo.RecordObject(targetProfile, "Apply PostProcessPreset");
        ApplyPresetToProfile(targetProfile, preset, 1f);
        EditorUtility.SetDirty(targetProfile);
        AssetDatabase.SaveAssets(); // persist changes in project assets (if it's an asset)
        EditorSceneManager.MarkSceneDirty(previewVolume.gameObject.scene);
    }

    // Core: apply preset values directly onto the provided VolumeProfile (blend 0..1)
    private void ApplyPresetToProfile(VolumeProfile profile, PostProcessPreset p, float blend)
    {
        if (profile == null || p == null) return;

        // Color Adjustments
        if (p.useColorAdjustments && profile.TryGet(out ColorAdjustments ca))
        {
            ca.colorFilter.value = Color.Lerp(ca.colorFilter.value, p.colorTint, blend);
            ca.saturation.value = Mathf.Lerp(ca.saturation.value, p.saturation, blend);
            EditorUtility.SetDirty(ca);
        }

        // Bloom
        if (p.useBloom && profile.TryGet(out Bloom bloom))
        {
            bloom.intensity.value = Mathf.Lerp(bloom.intensity.value, p.bloomIntensity, blend);
            bloom.threshold.value = Mathf.Lerp(bloom.threshold.value, p.bloomThreshold, blend);
            EditorUtility.SetDirty(bloom);
        }

        // Vignette
        if (p.useVignette && profile.TryGet(out Vignette vin))
        {
            vin.intensity.value = Mathf.Lerp(vin.intensity.value, p.vignetteIntensity, blend);
            EditorUtility.SetDirty(vin);
        }

        // Chromatic
        if (p.useChromatic && profile.TryGet(out ChromaticAberration chr))
        {
            chr.intensity.value = Mathf.Lerp(chr.intensity.value, p.chromaticIntensity, blend);
            EditorUtility.SetDirty(chr);
        }

        // Film grain
        if (p.useFilmGrain && profile.TryGet(out FilmGrain fg))
        {
            fg.intensity.value = Mathf.Lerp(fg.intensity.value, p.filmGrainIntensity, blend);
            EditorUtility.SetDirty(fg);
        }

        // Lens Distortion
        if (p.useLensDistortion && profile.TryGet(out LensDistortion ld))
        {
            ld.intensity.value = Mathf.Lerp(ld.intensity.value, p.lensDistortionIntensity, blend);
            EditorUtility.SetDirty(ld);
        }

        // Motion Blur (note: depending on URP version the MotionBlur component may differ)
        if (p.useMotionBlur && profile.TryGet(out MotionBlur mb))
        {
            // If property names differ in your URP version, adjust accordingly
            mb.intensity.value = Mathf.Lerp(mb.intensity.value, p.motionBlurIntensity, blend);
            EditorUtility.SetDirty(mb);
        }

        // Panini Projection
        if (p.usePanini && profile.TryGet(out PaniniProjection pp))
        {
            pp.distance.value = Mathf.Lerp(pp.distance.value, p.paniniDistance, blend);
            EditorUtility.SetDirty(pp);
        }

        // DOF
        if (p.useDOF && profile.TryGet(out DepthOfField dof))
        {
            dof.focusDistance.value = Mathf.Lerp(dof.focusDistance.value, p.dofFocusDistance, blend);
            EditorUtility.SetDirty(dof);
        }

        // White Balance
        if (p.useWhiteBalance && profile.TryGet(out WhiteBalance wb))
        {
            wb.temperature.value = Mathf.Lerp(wb.temperature.value, p.whiteBalanceTemperature, blend);
            wb.tint.value = Mathf.Lerp(wb.tint.value, p.whiteBalanceTint, blend);
            EditorUtility.SetDirty(wb);
        }

        // Lift/Gamma/Gain
        if (p.useLiftGammaGain && profile.TryGet(out LiftGammaGain lgg))
        {
            lgg.lift.value = Color.Lerp(lgg.lift.value, p.lift, blend);
            lgg.gamma.value = Color.Lerp(lgg.gamma.value, p.gamma, blend);
            lgg.gain.value = Color.Lerp(lgg.gain.value, p.gain, blend);
            EditorUtility.SetDirty(lgg);
        }

        // Shadows / Midtones / Highlights
        if (p.useSMH && profile.TryGet(out ShadowsMidtonesHighlights smh))
        {
            smh.shadows.value = Color.Lerp(smh.shadows.value, p.shadows, blend);
            smh.midtones.value = Color.Lerp(smh.midtones.value, p.midtones, blend);
            smh.highlights.value = Color.Lerp(smh.highlights.value, p.highlights, blend);
            EditorUtility.SetDirty(smh);
        }
    }

    private void OnDisable()
    {
        // Clean up any temporary profile we might have created while previewing
        if (previewVolume != null && tempProfile != null && originalProfile != null)
        {
            // revert so we don't leave scene in a changed state
            previewVolume.profile = originalProfile;
            DestroyImmediate(tempProfile, true);
        }
        originalProfile = null;
        tempProfile = null;
    }
}
