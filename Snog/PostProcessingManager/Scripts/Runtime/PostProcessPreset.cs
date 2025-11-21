using UnityEngine;

namespace Snog.PostProcessingManager.Runtime
{
    [CreateAssetMenu(menuName = "PostProcessing/PostProcessPreset", fileName = "PostProcessPreset")]
    public class PostProcessPreset : ScriptableObject
    {
        [Header("Color Adjustments")]
        public bool useColorAdjustments = false;
        public Color colorTint = Color.white;
        [Tooltip("Saturation value (e.g. -100 .. 100)")]
        public float saturation = 0f;

        [Header("Bloom")]
        public bool useBloom = false;
        public float bloomIntensity = 1f;
        public float bloomThreshold = 1f;

        [Header("Vignette")]
        public bool useVignette = false;
        [Range(0f, 1f)]
        public float vignetteIntensity = 0.3f;

        [Header("Chromatic Aberration")]
        public bool useChromatic = false;
        [Range(0f, 1f)]
        public float chromaticIntensity = 0.1f;

        [Header("Film Grain")]
        public bool useFilmGrain = false;
        [Range(0f, 1f)]
        public float filmGrainIntensity = 0.1f;

        [Header("Lens Distortion")]
        public bool useLensDistortion = false;
        public float lensDistortionIntensity = 0f;

        [Header("Motion Blur")]
        public bool useMotionBlur = false;
        public float motionBlurIntensity = 0.5f;

        [Header("Panini Projection")]
        public bool usePanini = false;
        public float paniniDistance = 1f;

        [Header("Depth of Field")]
        public bool useDOF = false;
        public float dofFocusDistance = 10f;

        [Header("White Balance")]
        public bool useWhiteBalance = false;
        public float whiteBalanceTemperature = 0f;
        public float whiteBalanceTint = 0f;

        [Header("Lift / Gamma / Gain")]
        public bool useLiftGammaGain = false;
        public Color lift = Color.black;
        public Color gamma = Color.gray;
        public Color gain = Color.white;

        [Header("Shadows/Midtones/Highlights")]
        public bool useSMH = false;
        public Color shadows = Color.black;
        public Color midtones = Color.gray;
        public Color highlights = Color.white;
    }
}