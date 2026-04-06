using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BaseVisualController : MonoBehaviour
{
    public enum MaterialApplyMode
    {
        InstanceMaterials,  // 使用 Renderer.materials（推荐：不污染其他对象）
        SharedMaterials     // 使用 Renderer.sharedMaterials（会影响共享该材质的对象）
    }

    [Header("Targets")]
    [Tooltip("为空则自动抓取自身及子物体下所有 Renderer。")]
    [SerializeField] private Renderer[] targetRenderers;

    [Header("Behavior")]
    [SerializeField] private MaterialApplyMode applyMode = MaterialApplyMode.InstanceMaterials;

    [Tooltip("Awake 时缓存初始材质，用于 Flash/Reset。")]
    [SerializeField] private bool cacheOnAwake = true;

    private Renderer[] renderers;
    private Material[][] cachedMaterialsPerRenderer;

    private Coroutine flashRoutine;

    private void Awake()
    {
        ResolveRenderers();

        if (cacheOnAwake)
            CacheCurrentMaterials();
    }

    private void OnValidate()
    {
        // 仅编辑器：保持引用干净（避免丢了 renderer 还以为有）
        if (targetRenderers != null && targetRenderers.Length == 0)
            targetRenderers = null;
    }

    [ContextMenu("Visual/Resolve Renderers")]
    public void ResolveRenderers()
    {
        renderers = (targetRenderers != null && targetRenderers.Length > 0)
            ? targetRenderers
            : GetComponentsInChildren<Renderer>(includeInactive: true);
    }

    [ContextMenu("Visual/Cache Current Materials")]
    public void CacheCurrentMaterials()
    {
        if (renderers == null || renderers.Length == 0)
            ResolveRenderers();

        cachedMaterialsPerRenderer = new Material[renderers.Length][];

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null)
            {
                cachedMaterialsPerRenderer[i] = null;
                continue;
            }

            var mats = GetMaterials(r);
            cachedMaterialsPerRenderer[i] = mats != null ? (Material[])mats.Clone() : null;
        }
    }

    [ContextMenu("Visual/Reset Materials")]
    public void ResetMaterials()
    {
        if (cachedMaterialsPerRenderer == null || cachedMaterialsPerRenderer.Length == 0)
        {
            CacheCurrentMaterials();
            return;
        }

        StopFlash();

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;

            var cached = cachedMaterialsPerRenderer[i];
            if (cached == null) continue;

            SetMaterials(r, (Material[])cached.Clone());
        }
    }

    /// <summary>
    /// 替换所有 Renderer 的所有 material slot 为同一个材质（常用于“整体变色/中毒/冰冻状态”）。
    /// </summary>
    public void SetMaterialAll(Material material)
    {
        if (material == null) return;

        StopFlash();

        EnsureResolved();
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;

            var mats = GetMaterials(r);
            if (mats == null || mats.Length == 0) continue;

            for (int m = 0; m < mats.Length; m++)
                mats[m] = material;

            SetMaterials(r, mats);
        }

        CacheCurrentMaterials();
    }

    /// <summary>
    /// 替换指定 slot（例如某些模型第 0 个是 body，第 1 个是 weapon）。
    /// </summary>
    public void SetMaterialSlot(int slotIndex, Material material)
    {
        if (material == null) return;
        if (slotIndex < 0) return;

        StopFlash();

        EnsureResolved();
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;

            var mats = GetMaterials(r);
            if (mats == null) continue;
            if (slotIndex >= mats.Length) continue;

            mats[slotIndex] = material;
            SetMaterials(r, mats);
        }

        CacheCurrentMaterials();
    }

    /// <summary>
    /// 闪烁：临时替换材质 duration 秒后恢复（恢复到 Cache 的初始材质）。
    /// </summary>
    public void FlashMaterial(Material flashMaterial, float duration)
    {
        if (flashMaterial == null) return;
        if (duration <= 0f) return;

        EnsureResolved();

        if (cachedMaterialsPerRenderer == null || cachedMaterialsPerRenderer.Length == 0)
            CacheCurrentMaterials();

        StopFlash();
        flashRoutine = StartCoroutine(FlashRoutine(flashMaterial, duration));
    }

    public void StopFlash()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }
    }

    private IEnumerator FlashRoutine(Material flashMaterial, float duration)
    {
        // Apply flash
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;

            var mats = GetMaterials(r);
            if (mats == null || mats.Length == 0) continue;

            for (int m = 0; m < mats.Length; m++)
                mats[m] = flashMaterial;

            SetMaterials(r, mats);
        }

        yield return new WaitForSeconds(duration);

        // Restore
        ResetMaterials();

        flashRoutine = null;
    }

    private void EnsureResolved()
    {
        if (renderers == null || renderers.Length == 0)
            ResolveRenderers();
    }

    private Material[] GetMaterials(Renderer r)
    {
        return applyMode == MaterialApplyMode.SharedMaterials
            ? r.sharedMaterials
            : r.materials;
    }

    private void SetMaterials(Renderer r, Material[] mats)
    {
        if (applyMode == MaterialApplyMode.SharedMaterials)
            r.sharedMaterials = mats;
        else
            r.materials = mats;
    }
}