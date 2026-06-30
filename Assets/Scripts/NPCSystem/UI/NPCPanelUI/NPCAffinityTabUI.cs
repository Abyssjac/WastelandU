using System;
using JackyUtility;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public struct NPCAffinityTabData
{
    public float EnvironmentValue;
    public float EnvironmentMax;
    public float DailyInteractionValue;
    public float DailyInteractionMax;
    public float FamiliarityValue;
    public float FamiliarityMax;
    public bool DailyInteractable;
}

/// <summary>
/// Draws the NPC affinity tab. Data is prepared by NPCPanelUI or a legacy owner.
/// </summary>
public class NPCAffinityTabUI : MonoBehaviour, INPCPanelTab
{
    [Header("Root")]
    [SerializeField] private GameObject tabRoot;

    [Header("Affinity Sliders")]
    [SerializeField] private Slider environmentAffinitySlider;
    [SerializeField] private Slider dailyInteractionSlider;
    [SerializeField] private Slider familiaritySlider;

    [Header("Affinity Labels")]
    [SerializeField] private TextMeshProUGUI environmentAffinityLabel;
    [SerializeField] private TextMeshProUGUI dailyInteractionLabel;
    [SerializeField] private TextMeshProUGUI familiarityLabel;

    [Header("Buttons")]
    [SerializeField] private Button dailyInteractButton;

    public event Action OnDailyInteractRequested;

    private NPCAffinityTabData _data;

    private void Awake()
    {
        if (tabRoot == null)
            tabRoot = gameObject;

        if (dailyInteractButton != null)
            dailyInteractButton.onClick.AddListener(HandleDailyInteractClicked);
    }

    private void OnDestroy()
    {
        if (dailyInteractButton != null)
            dailyInteractButton.onClick.RemoveListener(HandleDailyInteractClicked);
    }

    public void SetData(NPCAffinityTabData data)
    {
        _data = data;
    }

    public void Open()
    {
        if (tabRoot != null)
            tabRoot.SetActive(true);

        Refresh();
    }

    public void Close()
    {
        if (tabRoot != null)
            tabRoot.SetActive(false);
    }

    public void Refresh()
    {
        SetSlider(
            environmentAffinitySlider,
            environmentAffinityLabel,
            0f,
            _data.EnvironmentMax,
            _data.EnvironmentValue);

        SetSlider(
            dailyInteractionSlider,
            dailyInteractionLabel,
            0f,
            _data.DailyInteractionMax,
            _data.DailyInteractionValue);

        SetSlider(
            familiaritySlider,
            familiarityLabel,
            0f,
            _data.FamiliarityMax,
            _data.FamiliarityValue);

        if (dailyInteractButton != null)
            dailyInteractButton.interactable = _data.DailyInteractable;
    }

    private void HandleDailyInteractClicked()
    {
        OnDailyInteractRequested?.Invoke();
    }

    private static void SetSlider(Slider slider, TextMeshProUGUI label, float min, float max, float value)
    {
        max = Mathf.Max(min, max);
        value = Mathf.Clamp(value, min, max);

        if (slider != null)
        {
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
        }

        if (label != null)
            label.text = $"{value:0.#}/{max:0.#}";
    }
}