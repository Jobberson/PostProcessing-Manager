using UnityEngine;

public partial class PostProcessManager
{
    /// <summary>
    /// Apply all settings from a preset. blendAmount is the per-effect blend factor (0..1).
    /// If duration > 0, the changes will be blended over time using the manager's blend coroutines.
    /// </summary>
    public void ApplyPreset(PostProcessPreset preset, float blendAmount = 1f, float duration = 0f)
    {
        if (preset == null)
        {
            Debug.LogWarning("PostProcessManager: preset is null.");
            return;
        }

        // Color adjustments
        if (preset.useColorAdjustments)
        {
            // tint + saturation (note: your ApplyColorTint expects targetSaturation default -100)
            ApplyColorTint(preset.colorTint, preset.saturation, blendAmount, duration);
        }

        // Bloom
        if (preset.useBloom)
        {
            ApplyBloom(preset.bloomIntensity, preset.bloomThreshold, blendAmount, duration);
        }

        // Vignette
        if (preset.useVignette)
        {
            ApplyVignette(preset.vignetteIntensity, blendAmount, duration);
        }

        // Chromatic
        if (preset.useChromatic)
        {
            ApplyChromatic(preset.chromaticIntensity, blendAmount, duration);
        }

        // Film Grain
        if (preset.useFilmGrain)
        {
            ApplyFilmGrain(preset.filmGrainIntensity, blendAmount, duration);
        }

        // Lens Distortion
        if (preset.useLensDistortion)
        {
            ApplyLensDistortion(preset.lensDistortionIntensity, blendAmount, duration);
        }

        // Motion Blur
        if (preset.useMotionBlur)
        {
            ApplyMotionBlur(preset.motionBlurIntensity, blendAmount, duration);
        }

        // Panini
        if (preset.usePanini)
        {
            ApplyPaniniProjection(preset.paniniDistance, blendAmount, duration);
        }

        // DOF
        if (preset.useDOF)
        {
            ApplyDOFFocus(preset.dofFocusDistance, blendAmount, duration);
        }

        // White Balance
        if (preset.useWhiteBalance)
        {
            ApplyWhiteBalance(preset.whiteBalanceTemperature, preset.whiteBalanceTint, blendAmount, duration);
        }

        // Lift/Gamma/Gain
        if (preset.useLiftGammaGain)
        {
            ApplyLiftGammaGain(preset.lift, preset.gamma, preset.gain, blendAmount, duration);
        }

        // SMH
        if (preset.useSMH)
        {
            ApplySMH(preset.shadows, preset.midtones, preset.highlights, blendAmount, duration);
        }
    }

    /// <summary>
    /// Instantly apply preset values (duration = 0).
    /// </summary>
    public void ApplyPresetInstant(PostProcessPreset preset, float blendAmount = 1f)
    {
        ApplyPreset(preset, blendAmount, 0f);
    }
}
