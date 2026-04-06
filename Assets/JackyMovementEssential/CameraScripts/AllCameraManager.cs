using System;
using System.Collections.Generic;
using UnityEngine;
using JackyUtility;
public class AllCameraManager : MonoBehaviour
{
    public static AllCameraManager Instance { get; private set; }

    [SerializeField] private CameraMode defaultCameraMode = CameraMode.Empty;
    private List<CameraBase> currentCameras = new List<CameraBase>();
    private CameraMode currentCameraMode;

    private List<CameraBase> allRegisteredCameras = new List<CameraBase>();

    public Action<CameraMode> OnCameraModeSwitched;

    private void RegisterDebugCommands()
    {
        if (DebugConsoleManager.Instance == null) return;

        // Usage: cam <mode>   e.g. "cam FollowTarget"
        DebugConsoleManager.Instance.RegisterCommand(new DebugCommand(
            "cam",
            "Switch camera mode. Usage: cam <mode>  (BaseTest / FollowTarget / FreePerspective)",
            args =>
            {
                if (args.Length == 0)
                {
                    Debug.LogWarning("cam command requires a mode argument. Current mode: " + currentCameraMode);
                    return;
                }

                if (Enum.TryParse(args[0], true, out CameraMode mode))
                {
                    SwitchToCameraMode(mode);
                    Debug.Log($"Camera switched to {mode}");
                }
                else
                {
                    Debug.LogWarning($"Unknown CameraMode: {args[0]}. Available: {string.Join(", ", Enum.GetNames(typeof(CameraMode)))}");
                }
            }
        ));
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (defaultCameraMode == CameraMode.Empty) { Debug.LogError("Remember To Set the DefaultCameraMode, Current is Empty"); }

        RegisterDebugCommands();
    }

    public void RegisterCamera(CameraBase tarCameraBase)
    {
        if (tarCameraBase == null) { Debug.LogError($"[{nameof(AllCameraManager)}] Trying to register a NULL camera.", this); return; }
        if (allRegisteredCameras.Contains(tarCameraBase)) { Debug.LogError("Trying to Register a CameraBase that has already been registered, Please Check"); return; }
        allRegisteredCameras.Add(tarCameraBase);

        if (currentCameras.Count == 0 && tarCameraBase.CameraMode == defaultCameraMode) { 
            SwitchToCameraMode(defaultCameraMode);
        }

        Debug.Log($"Camera '{tarCameraBase.name}' registered to AllCameraManager. Total registered cameras: {allRegisteredCameras.Count}");
    }

    public void UnRegisterCamera(CameraBase tarCameraBase)
    {
        if (tarCameraBase == null) { Debug.LogError("Trying to UnRegister a Null CameraBase, Please Check"); return; }
        if (!allRegisteredCameras.Contains(tarCameraBase)) { Debug.LogError("Trying to UnRegister a CameraBase that has not been registered, Please Check"); return; }
        allRegisteredCameras.Remove(tarCameraBase);

        if (currentCameras.Contains(tarCameraBase))
        {
            tarCameraBase.DeactivateCamera();
            currentCameras.Remove(tarCameraBase);
            if (currentCameras.Count == 0)
            {
                SwitchToCameraMode(defaultCameraMode);
            }
        }

        Debug.Log($"Camera '{tarCameraBase.name}' unregistered from AllCameraManager. Total registered cameras: {allRegisteredCameras.Count}");
    }

    public void SwitchToCameraMode(CameraMode tarMode)
    {
        CleanupNullCameras();

        if (tarMode == CameraMode.Empty)
        {
            Debug.LogError($"[{nameof(AllCameraManager)}] Cannot switch to CameraMode.Empty.", this);
            return;
        }
        List<CameraBase> targetCameras = FindCamerasByMode(tarMode);
        if (targetCameras == null)
        {
            Debug.LogError($"[{nameof(AllCameraManager)}] No registered camera found for mode '{tarMode}'.", this);
            return;
        }
        for (int i = 0; i < allRegisteredCameras.Count; i++)
        {
            CameraBase curCam = allRegisteredCameras[i];

            if (targetCameras.Contains(curCam))
            {
                curCam.ActivateCamera();
            }
            else { 
                curCam.DeactivateCamera();  
            }
        }
        currentCameras = targetCameras;
        currentCameraMode = tarMode;

        OnCameraModeSwitched?.Invoke(currentCameraMode);
    }

    public List<CameraBase> FindCamerasActivated()
    {
        return new List<CameraBase>(currentCameras);
    }

    public List<CameraBase> FindCamerasByMode(CameraMode mode)
    {
        List<CameraBase> camerasWithMode = new List<CameraBase>();
        for (int i = 0; i < allRegisteredCameras.Count; i++)
        {
            CameraBase cam = allRegisteredCameras[i];
            if (cam == null)
                continue;
            if (cam.CameraMode == mode)
                camerasWithMode.Add(cam);
        }

        return camerasWithMode;
    }

    private void DeactivateAllRegisteredCameras()
    {
        for (int i = 0; i < allRegisteredCameras.Count; i++)
        {
            CameraBase cam = allRegisteredCameras[i];
            if (cam == null)
                continue;

            cam.DeactivateCamera();
        }
    }

    private void CleanupNullCameras()
    {
        for (int i = allRegisteredCameras.Count - 1; i >= 0; i--)
        {
            if (allRegisteredCameras[i] == null)
                allRegisteredCameras.RemoveAt(i);
        }
    }
}

public enum CameraMode
{ 
    Empty = 0,
    BaseTest = 1,
    FollowTarget = 2,
    FreePerspective = 3,
}