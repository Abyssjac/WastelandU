using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders a crosshair at the center of the screen using a UI Canvas + Image.
/// Implements <see cref="IReliedCameraComponent"/> so it can be added to a
/// <see cref="CameraBase.reliedCameraComponents"/> array and automatically
/// enable/disable with the owning camera.
/// </summary>
public class CrosshairOverlay : MonoBehaviour, IReliedCameraComponent
{
    [Header("Camera")]
    [SerializeField] private CameraBase reliedCamera;

    [Header("Crosshair")]
    [Tooltip("Sprite to display as the crosshair. If null, a default dot will be created.")]
    [SerializeField] private Sprite crosshairSprite;

    [Tooltip("Size of the crosshair in pixels.")]
    [SerializeField] private Vector2 crosshairSize = new Vector2(16f, 16f);

    [Tooltip("Color of the crosshair.")]
    [SerializeField] private Color crosshairColor = Color.white;

    // ©¤©¤ IReliedCameraComponent ©¤©¤
    public CameraBase ReliedCamera => reliedCamera;

    // Runtime
    private Canvas canvas;
    private Image crosshairImage;

    private void Awake()
    {
        if (reliedCamera == null)
            reliedCamera = GetComponentInParent<CameraBase>();

        BuildUI();
    }

    private void OnEnable()
    {
        if (canvas != null) canvas.enabled = true;
    }

    private void OnDisable()
    {
        if (canvas != null) canvas.enabled = false;
    }

    private void BuildUI()
    {
        // Create Canvas (Screen Space Overlay ¡ª always on top, no camera reference needed)
        GameObject canvasObj = new GameObject("[CrosshairCanvas]");
        canvasObj.transform.SetParent(transform, false);

        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Create crosshair Image centered on screen
        GameObject imageObj = new GameObject("[Crosshair]");
        imageObj.transform.SetParent(canvasObj.transform, false);

        crosshairImage = imageObj.AddComponent<Image>();
        crosshairImage.sprite = crosshairSprite;
        crosshairImage.color = crosshairColor;
        crosshairImage.raycastTarget = false;

        // If no sprite assigned, use a simple white square (acts as a dot)
        if (crosshairSprite == null)
            crosshairImage.sprite = null; // Unity draws a white rectangle

        RectTransform rt = crosshairImage.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = crosshairSize;

        // Match initial enabled state
        canvas.enabled = enabled;
    }

    /// <summary>
    /// Update crosshair appearance at runtime.
    /// </summary>
    public void SetCrosshairColor(Color color)
    {
        crosshairColor = color;
        if (crosshairImage != null)
            crosshairImage.color = color;
    }

    public void SetCrosshairSize(Vector2 size)
    {
        crosshairSize = size;
        if (crosshairImage != null)
            crosshairImage.rectTransform.sizeDelta = size;
    }

    public void SetCrosshairSprite(Sprite sprite)
    {
        crosshairSprite = sprite;
        if (crosshairImage != null)
            crosshairImage.sprite = sprite;
    }
}
