using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BaseVisualController : MonoBehaviour
{
    public enum MaterialApplyMode
    {
        InstanceMaterials,  // ʹ�� Renderer.materials���Ƽ�������Ⱦ��������
        SharedMaterials     // ʹ�� Renderer.sharedMaterials����Ӱ�칲��ò��ʵĶ���
    }

    [Header("Targets")]
    [Tooltip("Ϊ�����Զ�ץȡ����������������� Renderer��")]
    [SerializeField] private Renderer[] targetRenderers;

    [Header("Behavior")]
    [SerializeField] private MaterialApplyMode applyMode = MaterialApplyMode.InstanceMaterials;

    [Tooltip("Awake ʱ�����ʼ���ʣ����� Flash/Reset��")]
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
        // ���༭�����������øɾ������ⶪ�� renderer ����Ϊ�У�
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
    /// �滻���� Renderer ������ material slot Ϊͬһ�����ʣ������ڡ������ɫ/�ж�/����״̬������
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
	/// Temporarily replaces all material slots on all Renderers without updating the cache.
	/// Call <see cref="ResetMaterials"/> to restore the original materials.
	/// Use this for transient visual states such as Highlight or Focus.
	/// </summary>
	public void SetMaterialAllTemp(Material material)
	{
		if (material == null) return;

		StopFlash();

		EnsureResolved();

		// Ensure cache is valid before we overwrite renderers
		if (cachedMaterialsPerRenderer == null || cachedMaterialsPerRenderer.Length == 0)
			CacheCurrentMaterials();

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
		// Intentionally does NOT call CacheCurrentMaterials()
	}


    /// <summary>
    /// �滻ָ�� slot������ĳЩģ�͵� 0 ���� body���� 1 ���� weapon����
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
    /// ��˸����ʱ�滻���� duration ���ָ����ָ��� Cache �ĳ�ʼ���ʣ���
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
