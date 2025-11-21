# Snog's PostProcessing Manager

**Snog's PostProcessing Manager** is a lightweight, designer-friendly utility for controlling URP `Volume` profiles at runtime and in the editor. It provides a clean API to apply and blend common post-processing effects (bloom, vignette, color adjustments, DOF, film grain, etc.), plus an inspector with preset management and quick tuning controls.

This tool is meant to be easy to drop into a project and iterate with — assign a `Volume`, create `PostProcessPreset` assets, preview them on a `Volume`, and apply them instantly or blended over time.

---

## Features

* **ScriptableObject Presets**
  Create reusable `PostProcessPreset` assets (Create → PostProcessing → PostProcessPreset) to store effect values.

* **Manager-first UX**
  Presets are edited and applied from the `PostProcessManager` inspector for fast iteration (preview, revert, apply, or permanently write to a profile).

* **Per-effect API**
  Fine-grained methods such as `ApplyBloom`, `ApplyVignette`, `ApplyDOFFocus`, `ApplyWhiteBalance`, `ApplyLiftGammaGain`, etc. — all support blend amount and blend duration.

* **Smooth blending**
  Built-in coroutines blend values over time with automatic cancellation and safety when switching profiles.

* **Quick Tuning Controls**
  Inspector quick controls let you tweak single values in-play and apply them to the target `Volume`.

* **URP-focused, lightweight**
  No heavy dependencies. Designed around URP `Volume` & `VolumeProfile` components.

---

## Table of Contents

1. [Core Concepts](#core-concepts)
2. [Supported Effects / Components](#supported-effects--components)
3. [Quick Setup](#quick-setup)
4. [Using Presets (Editor)](#using-presets-editor)
5. [API Usage (Runtime)](#api-usage-runtime)
6. [Tips & Best Practices](#tips--best-practices)
7. [FAQ](#faq)
8. [License](#license)

---

## Core Concepts

* **PostProcessManager** — Singleton MonoBehaviour that controls a target `Volume` (assigned in the inspector). This is the main entry point for applying presets and individual effects.

* **PostProcessPreset** — `ScriptableObject` that stores which effects to modify and their parameter values. Presets can be created, edited, and applied from the manager inspector.

* **Target Volume** — a Unity `Volume` component (global or local) whose `VolumeProfile` will be modified by the manager.

---

## Supported Effects / Components

The manager caches and manipulates common URP `VolumeComponent`s (availability depends on your URP version):

* `ColorAdjustments` (saturation, color filter)
* `Bloom` (intensity, threshold)
* `Vignette` (intensity)
* `ChromaticAberration`
* `FilmGrain`
* `LensDistortion`
* `MotionBlur` *(URP dependent)*
* `PaniniProjection`
* `DepthOfField` (focus distance)
* `WhiteBalance` (temperature, tint)
* `LiftGammaGain`
* `ShadowsMidtonesHighlights`
* `SplitToning` *(if present)*

> **Important:** URP versions differ. Verify the components exist in your Unity/URP version (this project was developed with URP in mind — test in Unity 6 URP).

---

## Quick Setup

1. **Import**
   Copy the `PostProcessingManager` folder into your `Assets/` (or import as a package).

2. **Add Manager**
   Create an empty GameObject in your scene and add `PostProcessManager` to it. Assign the `Target Volume` field to the `Volume` you want to control (global or local).

3. **Create Presets**
   Create presets via `Assets → Create → PostProcessing → PostProcessPreset`. Edit their values in the inspector.

4. **Add Presets to Manager**
   In the `PostProcessManager` inspector, add your preset assets to the Presets list (use the ReorderableList).

5. **Preview & Apply**
   Select a preset in the manager's list, click **Preview** to see non-destructive preview (duplicate profile assigned to the Volume), **Revert** to undo preview, or **Apply Permanently** (with Undo support) to write into the profile asset.

---

## Using Presets (Editor)

* **Preview**: Duplicates the profile and assigns it to the target Volume so you can preview without modifying the asset. Use **Revert Preview** to restore the original profile.
* **Apply (blend)**: Applies preset values with a blend amount and duration (smooth transition).
* **Apply Instant**: Immediately writes the preset values into the active profile instance (no blend).
* **Apply Permanently (with Undo)**: Writes into the profile asset and records an Undo step (useful when you want to save changes permanently).

---

## API Usage (Runtime)

All public methods live on `PostProcessManager.Instance` (singleton). Examples:

### Apply a full preset

```csharp
// blendAmount (0..1), duration in seconds
PostProcessManager.Instance.ApplyPreset(myPreset, 1f, 0.5f);
```

### Apply a preset instantly

```csharp
PostProcessManager.Instance.ApplyPresetInstant(myPreset, 1f);
```

### Apply a single effect (example: bloom)

```csharp
// targetIntensity, targetThreshold, blendAmount, duration
PostProcessManager.Instance.ApplyBloom(2.5f, 1.0f, 1f, 0.75f);
```

### Stop all blends (useful before switching profiles)

```csharp
PostProcessManager.Instance.StopAllActive();
```

### Read/modify a raw VolumeComponent

```csharp
PostProcessManager.Instance.Modify<ColorAdjustments>(ca =>
{
    ca.saturation.value = -20f;
    ca.colorFilter.value = Color.Lerp(ca.colorFilter.value, Color.red, 0.5f);
});
```

---

## Tips & Best Practices

* **Keep a demo scene**: include a small scene with a sample `Volume` and example presets so artists and designers can preview quickly.
* **Version-check URP**: verify which post-processing components your target URP exposes. Remove or guard any components that don't exist in older/newer URP versions.
* **Use Undo for asset edits**: prefer **Apply Permanently (with Undo)** when you want to change profile assets, so designers can revert.
* **Stop blends before swapping profiles**: calling `RefreshProfile(newVolume)` automatically stops active blends. It’s a good idea to call that when your target volume changes at runtime.

---

## FAQ

**Q: Which Unity versions are supported?**
This is targeted for URP in Unity 6+. Because Unity/URP post-processing APIs change across versions, test the package in your target Unity/URP version.

**Q: Are presets editable in the manager?**
Yes — the manager shows and applies `PostProcessPreset` assets. (You chose to keep preset editing in assets rather than embedded inline presets.)

**Q: Will this break if a component is missing from the profile?**
No. The manager uses `TryGet` and safe guards. If a component is not present, that effect call is skipped and a warning is logged when using `Modify<T>`.

**Q: Can I create presets at runtime?**
You can create preset instances in code and call `ApplyPreset` at runtime. Saving ScriptableObject assets requires editor APIs and should be done in editor scripts.

---

## License

This project is licensed under the **MIT License** — see `LICENSE` for details.
