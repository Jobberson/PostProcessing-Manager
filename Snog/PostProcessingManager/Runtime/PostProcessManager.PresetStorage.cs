using System.Collections.Generic;
using UnityEngine;

public partial class PostProcessManager
{
    [Header("Presets (assign PostProcessPreset assets here)")]
    [SerializeField] private List<PostProcessPreset> presets = new();

    /// <summary>
    /// Read-only view for runtime callers / other scripts.
    /// </summary>
    public IReadOnlyList<PostProcessPreset> Presets => presets;

    /// <summary>
    /// Adds a preset to the internal list (editor friendly).
    /// </summary>
    public void AddPresetToList(PostProcessPreset preset)
    {
        if (preset == null) return;
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "Add PostProcess Preset");
#endif
        presets.Add(preset);
    }

    /// <summary>
    /// Removes a preset at index (editor friendly).
    /// </summary>
    public void RemovePresetFromList(int index)
    {
        if (index < 0 || index >= presets.Count) return;
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "Remove PostProcess Preset");
#endif
        presets.RemoveAt(index);
    }

    /// <summary>
    /// Apply preset by index (safe).
    /// </summary>
    public void ApplyPresetAtIndex(int index, float blendAmount = 1f, float duration = 0f)
    {
        if (index < 0 || index >= presets.Count) return;
        ApplyPreset(presets[index], blendAmount, duration);
    }

    /// <summary>
    /// Apply preset instantly by index.
    /// </summary>
    public void ApplyPresetInstantAtIndex(int index, float blendAmount = 1f)
    {
        if (index < 0 || index >= presets.Count) return;
        ApplyPresetInstant(presets[index], blendAmount);
    }
}
