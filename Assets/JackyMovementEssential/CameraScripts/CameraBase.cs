using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class CameraBase : MonoBehaviour
{
    [SerializeField] private CameraMode cameraMode = CameraMode.BaseTest;
    [SerializeField] private MonoBehaviour[] reliedCameraComponents;
    [SerializeField] private bool deactivateSelfComponent = true;
    public CameraMode CameraMode => cameraMode;

    public Camera CachedCamera { get; private set; }

    protected virtual void Awake()
    {
        CachedCamera = GetComponent<Camera>();
        DeactivateCamera(); // Start with the camera disabled by default. It will be activated by the AllCameraManager when needed.

        AllCameraManager.Instance.RegisterCamera(this);
    }

    protected virtual void OnDestroy()
    {
        AllCameraManager.Instance.UnRegisterCamera(this);
    }

    public virtual void LateUpdate()
    {
        // BaseCamera doesn't do anything in LateUpdate, but derived classes can override this method to implement their own camera behavior.
    }

    public virtual void Update()
    {
        // BaseCamera doesn't do anything in Update, but derived classes can override this method to implement their own camera behavior.
    }

    public virtual void ActivateCamera()
    {
        CachedCamera.enabled = true;
        foreach (var component in reliedCameraComponents)
        {
            if (component is IReliedCameraComponent cameraComponent) { 
                component.enabled = true;
            }
            else{ 
                Debug.LogError($"CameraBase {name} has a component in reliedCameraComponents that does not implement IReliedCameraComponent, Please Check. Component: {component}", this);
            }
        }
        if (deactivateSelfComponent)
        {
            this.enabled = true;
        }
    }

    public virtual void DeactivateCamera()
    {
        CachedCamera.enabled = false;
        foreach (var component in reliedCameraComponents)
        {
            if (component is IReliedCameraComponent cameraCopmonent)
            {
                component.enabled = false;
            }
            else { 
                Debug.LogError($"CameraBase {name} has a component in reliedCameraComponents that does not implement IReliedCameraComponent, Please Check. Component: {component}", this);
            }
        }
        if (deactivateSelfComponent)
        {
            this.enabled = false;
        }
    }
}

public interface IReliedCameraComponent
{
    CameraBase ReliedCamera { get; }
}