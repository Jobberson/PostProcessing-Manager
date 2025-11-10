using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PostProcessManager : Singleton<PostProcessManager>
{
    [Header("Target Volume")]
    public[Tooltip("Assign your global Volume (or a local one you want to control)")] Volume targetVolume;

    // Cached references
    private ColorAdjustments colorAdjustments;
    private Bloom bloom;
    private Vignette vignette;
    private ChromaticAberration chromatic;
    private FilmGrain filmGrain;
    private DepthOfField depthOfField;
    private LensDistortion lensDistortion;
    private MotionBlur motionBlur;
    private PaniniProjection paniniProjection;
    private Tonemapping tonemapping;
    private WhiteBalance whiteBalance;
    private LiftGammaGain liftGammaGain;
    private ShadowsMidtonesHighlights shadowsMidtonesHighlights;
    private SplitToning splitToning;

    private Dictionary<string, Coroutine> activeCoroutines = new();

    protected override void Awake()
    {
        base.Awake();
        CacheComponents();
    }

    private void OnValidate()
    {
        CacheComponents();
    }

    #region Color Adjustments
    public void ApplyColorTint(Color targetTint, float targetSaturation = -100f, float blendAmount = 1f, float duration = 0f)
    {
        if (targetVolume == null || colorAdjustments == null) { CacheComponents(); if (colorAdjustments == null) return; }

        float clampedBlend = Mathf.Clamp01(blendAmount);
        float desiredSat = Mathf.Lerp(colorAdjustments.saturation.value, targetSaturation, clampedBlend);
        Color desiredCol = Color.Lerp(Color.white, targetTint, clampedBlend);

        string key = "ColorTint";
        StopActive(key);
        if (duration <= 0f)
        {
            colorAdjustments.saturation.value = desiredSat;
            colorAdjustments.colorFilter.value = desiredCol;
        }
        else
        {
            activeCoroutines[key] = StartCoroutine(BlendColorAdjustments(colorAdjustments.saturation.value, colorAdjustments.colorFilter.value, desiredSat, desiredCol, duration, key));
        }
    }

    private IEnumerator BlendColorAdjustments(float startSat, Color startCol, float endSat, Color endCol, float duration, string key)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / duration);
            colorAdjustments.saturation.value = Mathf.Lerp(startSat, endSat, u);
            colorAdjustments.colorFilter.value = Color.Lerp(startCol, endCol, u);
            yield return null;
        }
        colorAdjustments.saturation.value = endSat;
        colorAdjustments.colorFilter.value = endCol;
        activeCoroutines.Remove(key);
    }
    #endregion

    #region Bloom
    public void ApplyBloom(float targetIntensity, float targetThreshold, float blendAmount = 1f, float duration = 0f)
    {
        if (targetVolume == null || bloom == null) { CacheComponents(); if (bloom == null) return; }

        float finalIntensity = Mathf.Lerp(bloom.intensity.value, targetIntensity * Mathf.Clamp01(blendAmount), Mathf.Clamp01(blendAmount));
        float finalThreshold = Mathf.Lerp(bloom.threshold.value, targetThreshold, Mathf.Clamp01(blendAmount));

        string key = "Bloom";
        StopActive(key);
        if (duration <= 0f)
        {
            bloom.intensity.value = finalIntensity;
            bloom.threshold.value = finalThreshold;
        }
        else
        {
            activeCoroutines[key] = StartCoroutine(BlendBloom(bloom.intensity.value, bloom.threshold.value, finalIntensity, finalThreshold, duration, key));
        }
    }

    private IEnumerator BlendBloom(float startInt, float startThr, float endInt, float endThr, float duration, string key)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / duration);
            bloom.intensity.value = Mathf.Lerp(startInt, endInt, u);
            bloom.threshold.value = Mathf.Lerp(startThr, endThr, u);
            yield return null;
        }
        bloom.intensity.value = endInt;
        bloom.threshold.value = endThr;
        activeCoroutines.Remove(key);
    }
    #endregion

    #region Vignette
    public void ApplyVignette(float targetIntensity, float blendAmount = 1f, float duration = 0f)
    {
        if (targetVolume == null || vignette == null) { CacheComponents(); if (vignette == null) return; }

        float final = Mathf.Lerp(vignette.intensity.value, targetIntensity, Mathf.Clamp01(blendAmount));

        string key = "Vignette";
        StopActive(key);
        if (duration <= 0f)
        {
            vignette.intensity.value = final;
        }
        else
        {
            activeCoroutines[key] = StartCoroutine(BlendFloat(vignette.intensity.value, final, duration, (v) => vignette.intensity.value = v, key));
        }
    }
    #endregion

    #region Chromatic Aberration
    public void ApplyChromatic(float targetIntensity, float blendAmount = 1f, float duration = 0f)
    {
        if (targetVolume == null || chromatic == null) { CacheComponents(); if (chromatic == null) return; }

        float final = Mathf.Lerp(chromatic.intensity.value, targetIntensity, Mathf.Clamp01(blendAmount));

        string key = "Chromatic";
        StopActive(key);
        if (duration <= 0f)
        {
            chromatic.intensity.value = final;
        }
        else
        {
            activeCoroutines[key] = StartCoroutine(BlendFloat(chromatic.intensity.value, final, duration, (v) => chromatic.intensity.value = v, key));
        }
    }
    #endregion

    #region Film Grain
    public void ApplyFilmGrain(float targetIntensity, float blendAmount = 1f, float duration = 0f)
    {
        if (targetVolume == null || filmGrain == null) { CacheComponents(); if (filmGrain == null) return; }

        float final = Mathf.Lerp(filmGrain.intensity.value, targetIntensity, Mathf.Clamp01(blendAmount));

        string key = "FilmGrain";
        StopActive(key);
        if (duration <= 0f)
        {
            filmGrain.intensity.value = final;
        }
        else
        {
            activeCoroutines[key] = StartCoroutine(BlendFloat(filmGrain.intensity.value, final, duration, (v) => filmGrain.intensity.value = v, key));
        }
    }
    #endregion

    #region Lens Distortion
    public void ApplyLensDistortion(float targetIntensity, float blendAmount = 1f, float duration = 0f)
    {
        if (lensDistortion == null) { CacheComponents(); if (lensDistortion == null) return; }

        float final = Mathf.Lerp(lensDistortion.intensity.value, targetIntensity, Mathf.Clamp01(blendAmount));
        string key = "LensDistortion";
        StopActive(key);

        if (duration <= 0f)
        {
            lensDistortion.intensity.value = final;
        }
        else
        {
            activeCoroutines[key] = StartCoroutine(BlendFloat(lensDistortion.intensity.value, final, duration, v => lensDistortion.intensity.value = v, key));
        }
    }
    #endregion

    #region Motion Blur
    public void ApplyMotionBlur(float targetIntensity, float blendAmount = 1f, float duration = 0f)
    {
        if (motionBlur == null) { CacheComponents(); if (motionBlur == null) return; }

        float final = Mathf.Lerp(motionBlur.intensity.value, targetIntensity, Mathf.Clamp01(blendAmount));
        string key = "MotionBlur";
        StopActive(key);

        if (duration <= 0f)
        {
            motionBlur.intensity.value = final;
        }
        else
        {
            activeCoroutines[key] = StartCoroutine(BlendFloat(motionBlur.intensity.value, final, duration, v => motionBlur.intensity.value = v, key));
        }
    }
    #endregion

    #region Panini Projection
    public void ApplyPaniniProjection(float targetDistance, float blendAmount = 1f, float duration = 0f)
    {
        if (paniniProjection == null) { CacheComponents(); if (paniniProjection == null) return; }

        float final = Mathf.Lerp(paniniProjection.distance.value, targetDistance, Mathf.Clamp01(blendAmount));
        string key = "PaniniProjection";
        StopActive(key);

        if (duration <= 0f)
        {
            paniniProjection.distance.value = final;
        }
        else
        {
            activeCoroutines[key] = StartCoroutine(BlendFloat(paniniProjection.distance.value, final, duration, v => paniniProjection.distance.value = v, key));
        }
    }
    #endregion

    #region Depth of Field
    public void ApplyDOFFocus(float targetFocusDistance, float blendAmount = 1f, float duration = 0f)
    {
        if (targetVolume == null || depthOfField == null) { CacheComponents(); if (depthOfField == null) return; }

        float final = Mathf.Lerp(depthOfField.focusDistance.value, targetFocusDistance, Mathf.Clamp01(blendAmount));

        string key = "DOF";
        StopActive(key);
        if (duration <= 0f)
        {
            depthOfField.focusDistance.value = final;
        }
        else
        {
            activeCoroutines[key] = StartCoroutine(BlendFloat(depthOfField.focusDistance.value, final, duration, (v) => depthOfField.focusDistance.value = v, key));
        }
    }
    #endregion

    #region White Balance
    public void ApplyWhiteBalance(float temperature, float tint, float blendAmount = 1f, float duration = 0f)
    {
        if (whiteBalance == null) { CacheComponents(); if (whiteBalance == null) return; }

        float finalTemp = Mathf.Lerp(whiteBalance.temperature.value, temperature, Mathf.Clamp01(blendAmount));
        float finalTint = Mathf.Lerp(whiteBalance.tint.value, tint, Mathf.Clamp01(blendAmount));
        string key = "WhiteBalance";
        StopActive(key);

        if (duration <= 0f)
        {
            whiteBalance.temperature.value = finalTemp;
            whiteBalance.tint.value = finalTint;
        }
        else
        {
            activeCoroutines[key] = StartCoroutine(BlendWhiteBalance(whiteBalance.temperature.value, whiteBalance.tint.value, finalTemp, finalTint, duration, key));
        }
    }
    
    private IEnumerator BlendWhiteBalance(float startTemp, float startTint, float endTemp, float endTint, float duration, string key)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / duration);
            whiteBalance.temperature.value = Mathf.Lerp(startTemp, endTemp, u);
            whiteBalance.tint.value = Mathf.Lerp(startTint, endTint, u);
            yield return null;
        }
        whiteBalance.temperature.value = endTemp;
        whiteBalance.tint.value = endTint;
        activeCoroutines.Remove(key);
    }
    #endregion

    #region Lift Gamma Gain
    public void ApplyLiftGammaGain(Color lift, Color gamma, Color gain, float blendAmount = 1f, float duration = 0f)
    {
        if (liftGammaGain == null) { CacheComponents(); if (liftGammaGain == null) return; }

        Color finalLift = Color.Lerp(liftGammaGain.lift.value, lift, Mathf.Clamp01(blendAmount));
        Color finalGamma = Color.Lerp(liftGammaGain.gamma.value, gamma, Mathf.Clamp01(blendAmount));
        Color finalGain = Color.Lerp(liftGammaGain.gain.value, gain, Mathf.Clamp01(blendAmount));
        string key = "LiftGammaGain";
        StopActive(key);

        if (duration <= 0f)
        {
            liftGammaGain.lift.value = finalLift;
            liftGammaGain.gamma.value = finalGamma;
            liftGammaGain.gain.value = finalGain;
        }
        else
        {
            activeCoroutines[key] = StartCoroutine(BlendLiftGammaGain(liftGammaGain.lift.value, liftGammaGain.gamma.value, liftGammaGain.gain.value, finalLift, finalGamma, finalGain, duration, key));
        }
    }

    private IEnumerator BlendLiftGammaGain(Color startLift, Color startGamma, Color startGain, Color endLift, Color endGamma, Color endGain, float duration, string key)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / duration);
            liftGammaGain.lift.value = Color.Lerp(startLift, endLift, u);
            liftGammaGain.gamma.value = Color.Lerp(startGamma, endGamma, u);
            liftGammaGain.gain.value = Color.Lerp(startGain, endGain, u);
            yield return null;
        }
        liftGammaGain.lift.value = endLift;
        liftGammaGain.gamma.value = endGamma;
        liftGammaGain.gain.value = endGain;
        activeCoroutines.Remove(key);
    }
    #endregion

    #region Shadows, Midtones and Highlights
    public void ApplySMH(Color shadows, Color midtones, Color highlights, float blendAmount = 1f, float duration = 0f)
    {
        if (shadowsMidtonesHighlights == null) { CacheComponents(); if (shadowsMidtonesHighlights == null) return; }

        Color finalShadows = Color.Lerp(shadowsMidtonesHighlights.shadows.value, shadows, Mathf.Clamp01(blendAmount));
        Color finalMidtones = Color.Lerp(shadowsMidtonesHighlights.midtones.value, midtones, Mathf.Clamp01(blendAmount));
        Color finalHighlights = Color.Lerp(shadowsMidtonesHighlights.highlights.value, highlights, Mathf.Clamp01(blendAmount));
        string key = "SMH";
        StopActive(key);

        if (duration <= 0f)
        {
            shadowsMidtonesHighlights.shadows.value = finalShadows;
            shadowsMidtonesHighlights.midtones.value = finalMidtones;
            shadowsMidtonesHighlights.highlights.value = finalHighlights;
        }
        else
        {
            activeCoroutines[key] = StartCoroutine(BlendSMH(shadowsMidtonesHighlights.shadows.value, shadowsMidtonesHighlights.midtones.value, shadowsMidtonesHighlights.highlights.value, finalShadows, finalMidtones, finalHighlights, duration, key));
        }
    }

    private IEnumerator BlendSMH(Color startShadows, Color startMidtones, Color startHighlights, Color endShadows, Color endMidtones, Color endHighlights, float duration, string key)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / duration);
            shadowsMidtonesHighlights.shadows.value = Color.Lerp(startShadows, endShadows, u);
            shadowsMidtonesHighlights.midtones.value = Color.Lerp(startMidtones, endMidtones, u);
            shadowsMidtonesHighlights.highlights.value = Color.Lerp(startHighlights, endHighlights, u);
            yield return null;
        }
        shadowsMidtonesHighlights.shadows.value = endShadows;
        shadowsMidtonesHighlights.midtones.value = endMidtones;
        shadowsMidtonesHighlights.highlights.value = endHighlights;
        activeCoroutines.Remove(key);
    }
    #endregion

    #region Utilities
    private void CacheComponents()
    {
        if (targetVolume == null || targetVolume.profile == null) return;

        targetVolume.profile.TryGet(out colorAdjustments);
        targetVolume.profile.TryGet(out bloom);
        targetVolume.profile.TryGet(out vignette);
        targetVolume.profile.TryGet(out chromatic);
        targetVolume.profile.TryGet(out filmGrain);
        targetVolume.profile.TryGet(out depthOfField);
        targetVolume.profile.TryGet(out lensDistortion);
        targetVolume.profile.TryGet(out motionBlur);
        targetVolume.profile.TryGet(out paniniProjection);
        targetVolume.profile.TryGet(out tonemapping);
        targetVolume.profile.TryGet(out whiteBalance);
        targetVolume.profile.TryGet(out liftGammaGain);
        targetVolume.profile.TryGet(out shadowsMidtonesHighlights);
        targetVolume.profile.TryGet(out splitToning);
    }

    public void Modify<T>(Action<T> modifier) where T : VolumeComponent
    {
        if (targetVolume?.profile == null) return;

        if (targetVolume.profile.TryGet<T>(out var comp) && comp != null)
        {
            modifier.Invoke(comp);
        }
        else
        {
            Debug.LogWarning($"PostProcessManager: VolumeComponent of type {typeof(T)} not found.");
        }
    }

    private IEnumerator BlendFloat(float start, float end, float duration, Action<float> setter, string key)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / duration);
            setter(Mathf.Lerp(start, end, u));
            yield return null;
        }
        setter(end);
        activeCoroutines.Remove(key);
    }

    private void StopActive(string key)
    {
        if (activeCoroutines.TryGetValue(key, out var c))
        {
            if (c != null) StopCoroutine(c);
            activeCoroutines.Remove(key);
        }
    }

    public void RefreshProfile(Volume newVolume)
    {
        targetVolume = newVolume;
        CacheComponents();
    }
    #endregion
}
