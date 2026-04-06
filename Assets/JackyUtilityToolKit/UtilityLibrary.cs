using UnityEngine;

public static class UtilityLibrary
{
    /// <summary>
    /// 让目标朝向摄像机（一次性对齐）。常用于你自己在 Update/LateUpdate 中每帧调用。
    /// </summary>
    /// <param name="target">需要面向摄像机的物体（UI/Canvas/世界空间物体均可）</param>
    /// <param name="camera">指定摄像机；传 null 时使用 Camera.main</param>
    /// <param name="keepUp">
    /// true: 使用摄像机的 up 作为朝上方向（更“正”）。
    /// false: 世界 up（Vector3.up），更稳定但与摄像机倾斜时可能不完全一致。
    /// </param>
    /// <param name="invertForward">
    /// 有些 UI/平面法线方向相反会“背对”摄像机，设为 true 会反过来。
    /// </param>
    public static void FaceCameraOnce(GameObject target, Camera camera = null, bool keepUp = true, bool invertForward = false)
    {
        if (target == null)
            return;

        var cam = camera != null ? camera : Camera.main;
        if (cam == null)
            return;

        var t = target.transform;

        // 让物体的 forward 指向摄像机（或反向）
        Vector3 dir = cam.transform.position - t.position;
        if (invertForward)
            dir = -dir;

        if (dir.sqrMagnitude < 0.000001f)
            return;

        Vector3 up = keepUp ? cam.transform.up : Vector3.up;
        t.rotation = Quaternion.LookRotation(dir, up);
    }

    /// <summary>
    /// 让目标持续面向摄像机：自动添加/获取一个跟随组件（调用一次即可持续生效）
    /// </summary>
    public static FaceCameraBillboard EnsureFaceCamera(GameObject target, Camera camera = null, bool keepUp = true, bool invertForward = false)
    {
        if (target == null)
            return null;

        var comp = target.GetComponent<FaceCameraBillboard>();
        if (comp == null)
            comp = target.AddComponent<FaceCameraBillboard>();

        comp.TargetCamera = camera;
        comp.KeepUp = keepUp;
        comp.InvertForward = invertForward;

        return comp;
    }

    /// <summary>
    /// 持续将物体朝向摄像机的组件（用于“调用一次后自动每帧对齐”）
    /// </summary>
    public sealed class FaceCameraBillboard : MonoBehaviour
    {
        public Camera TargetCamera;
        public bool KeepUp = true;
        public bool InvertForward = false;

        // UI/跟随通常放到 LateUpdate，避免和其他位移/动画抖动
        private void LateUpdate()
        {
            UtilityLibrary.FaceCameraOnce(gameObject, TargetCamera, KeepUp, InvertForward);
        }
    }
}
