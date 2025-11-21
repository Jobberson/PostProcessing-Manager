#if UNITY_EDITOR
using System;

namespace Snog.PostProcessingManager.Editor
{
    /// <summary>
    /// Centralized configuration for URP Volume override types.
    /// </summary>
    public static class PostProcessOverrideConfig
    {
        /// <summary>
        /// Full type names of URP Volume overrides to ensure/add.
        /// </summary>
        public static readonly string[] RequiredTypeNames =
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
    }
}
#endif