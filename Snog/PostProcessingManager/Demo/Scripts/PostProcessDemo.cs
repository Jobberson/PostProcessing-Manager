using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PostProcessDemo : MonoBehaviour
{
    [Header("References")]
    public PostProcessManager manager;             // auto-assigned if left empty
    public List<PostProcessPreset> presets;        // drag your SOs here

    [Header("Blend Settings")]
    public float blendAmount = 1f;
    public float transitionDuration = 0.8f;

    [Header("Auto Cycle (optional)")]
    public bool autoCycle = false;
    public float cycleInterval = 5f;

    [Header("UI (optional)")]
    public Text label;
    public Button nextButton, prevButton, randomButton, autoButton;

    private int current = -1;
    private Coroutine cycleRoutine;

    void Awake()
    {
        if (manager == null)
            manager = FindObjectOfType<PostProcessManager>();

        HookUI();
    }

    void Start()
    {
        if (presets.Count > 0)
            ApplyIndex(0, instant: true);

        UpdateAutoCycle();
    }

    void HookUI()
    {
        if (nextButton) nextButton.onClick.AddListener(Next);
        if (prevButton) prevButton.onClick.AddListener(Previous);
        if (randomButton) randomButton.onClick.AddListener(RandomPreset);
        if (autoButton) autoButton.onClick.AddListener(() =>
        {
            autoCycle = !autoCycle;
            UpdateAutoCycle();
        });
    }

    // -----------------------------------------------------
    // Core
    // -----------------------------------------------------

    public void ApplyIndex(int index, bool instant = false)
    {
        if (presets.Count == 0) return;
        if (index < 0 || index >= presets.Count) return;

        current = index;

        var preset = presets[current];
        if (preset == null) return;

        if (label != null)
            label.text = preset.name;

        // The magic: use YOUR PRESET SO directly.
        if (instant)
            manager.ApplyPreset(preset, 1f, 0f);
        else
            manager.ApplyPreset(preset, blendAmount, transitionDuration);
    }

    public void Next()
    {
        if (presets.Count == 0) return;
        int next = (current + 1) % presets.Count;
        ApplyIndex(next);
    }

    public void Previous()
    {
        if (presets.Count == 0) return;
        int prev = (current - 1 + presets.Count) % presets.Count;
        ApplyIndex(prev);
    }

    public void RandomPreset()
    {
        if (presets.Count == 0) return;
        int r = Random.Range(0, presets.Count);
        ApplyIndex(r);
    }

    // -----------------------------------------------------
    // Auto Cycle
    // -----------------------------------------------------

    void UpdateAutoCycle()
    {
        if (cycleRoutine != null)
            StopCoroutine(cycleRoutine);

        if (autoCycle && presets.Count > 1)
            cycleRoutine = StartCoroutine(AutoCycle());
    }

    IEnumerator AutoCycle()
    {
        while (autoCycle)
        {
            yield return new WaitForSeconds(cycleInterval);
            Next();
        }
    }
}
