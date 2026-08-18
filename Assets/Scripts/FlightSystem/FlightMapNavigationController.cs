using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Provides middle-mouse panning and scroll-wheel zooming for a map content RectTransform.
/// The map content must be a direct child of the viewport and use centred anchors/pivot.
/// This component intentionally never resets its position or zoom, so closing and reopening
/// the map preserves the player's current view.
/// </summary>
[DisallowMultipleComponent]
public class FlightMapNavigationController : MonoBehaviour,
    IInitializePotentialDragHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IScrollHandler
{
    [Header("References")]
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform content;

    [Header("Zoom")]
    [SerializeField, Min(0.01f)] private float minZoom = 0.75f;
    [SerializeField, Min(0.01f)] private float maxZoom = 2.5f;
    [SerializeField, Range(0.01f, 1f)] private float zoomStep = 0.15f;

    [Header("Pan")]
    [SerializeField] private PointerEventData.InputButton dragButton = PointerEventData.InputButton.Middle;
    [SerializeField] private bool clampToViewport = true;

    private bool isDragging;
    private Canvas rootCanvas;

    private void Awake()
    {
        ResolveReferences();
        ClampContentPosition();
    }

    private void OnValidate()
    {
        minZoom = Mathf.Max(0.01f, minZoom);
        maxZoom = Mathf.Max(minZoom, maxZoom);
        zoomStep = Mathf.Clamp(zoomStep, 0.01f, 1f);
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        if (eventData != null && eventData.button == dragButton)
            eventData.useDragThreshold = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = eventData != null && eventData.button == dragButton;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || eventData == null || content == null)
            return;

        float canvasScale = GetCanvasScaleFactor();
        content.anchoredPosition += eventData.delta / canvasScale;
        ClampContentPosition();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData != null && eventData.button == dragButton)
            isDragging = false;
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (eventData == null || Mathf.Approximately(eventData.scrollDelta.y, 0f))
            return;

        ResolveReferences();
        if (viewport == null || content == null)
            return;

        float currentZoom = content.localScale.x;
        float direction = Mathf.Sign(eventData.scrollDelta.y);
        float targetZoom = Mathf.Clamp(currentZoom * (1f + direction * zoomStep), minZoom, maxZoom);
        if (Mathf.Approximately(currentZoom, targetZoom))
            return;

        Camera eventCamera = eventData.pressEventCamera;
        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(
                viewport, eventData.position, eventCamera, out Vector3 pointerWorldPosition))
        {
            return;
        }

        Vector3 pointerInContent = content.InverseTransformPoint(pointerWorldPosition);
        content.localScale = new Vector3(targetZoom, targetZoom, 1f);

        Vector3 pointerAfterZoom = content.TransformPoint(pointerInContent);
        if (content.parent is RectTransform parent)
        {
            Vector3 localCompensation = parent.InverseTransformVector(pointerWorldPosition - pointerAfterZoom);
            content.anchoredPosition += new Vector2(localCompensation.x, localCompensation.y);
        }

        ClampContentPosition();
    }

    private void ResolveReferences()
    {
        if (viewport == null)
            viewport = transform as RectTransform;

        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();
    }

    private void ClampContentPosition()
    {
        if (!clampToViewport || viewport == null || content == null)
            return;

        Vector2 contentSize = Vector2.Scale(content.rect.size, new Vector2(
            Mathf.Abs(content.localScale.x),
            Mathf.Abs(content.localScale.y)));
        Vector2 viewportSize = viewport.rect.size;
        Vector2 maxOffset = Vector2.Max(Vector2.zero, (contentSize - viewportSize) * 0.5f);

        Vector2 position = content.anchoredPosition;
        position.x = Mathf.Clamp(position.x, -maxOffset.x, maxOffset.x);
        position.y = Mathf.Clamp(position.y, -maxOffset.y, maxOffset.y);
        content.anchoredPosition = position;
    }

    private float GetCanvasScaleFactor()
    {
        ResolveReferences();
        return rootCanvas != null ? Mathf.Max(0.0001f, rootCanvas.scaleFactor) : 1f;
    }
}
