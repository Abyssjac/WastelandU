// using UnityEngine;

// public static class UtilityLibrary
// {
//     /// <summary>
//     /// ��Ŀ�곯���������һ���Զ��룩�����������Լ��� Update/LateUpdate ��ÿ֡���á�
//     /// </summary>
//     /// <param name="target">��Ҫ��������������壨UI/Canvas/����ռ�������ɣ�</param>
//     /// <param name="camera">ָ����������� null ʱʹ�� Camera.main</param>
//     /// <param name="keepUp">
//     /// true: ʹ��������� up ��Ϊ���Ϸ��򣨸�����������
//     /// false: ���� up��Vector3.up�������ȶ������������бʱ���ܲ���ȫһ�¡�
//     /// </param>
//     /// <param name="invertForward">
//     /// ��Щ UI/ƽ�淨�߷����෴�ᡰ���ԡ����������Ϊ true �ᷴ������
//     /// </param>
//     public static void FaceCameraOnce(GameObject target, Camera camera = null, bool keepUp = true, bool invertForward = false)
//     {
//         if (target == null)
//             return;

//         var cam = camera != null ? camera : Camera.main;
//         if (cam == null)
//             return;

//         var t = target.transform;

//         // ������� forward ָ�������������
//         Vector3 dir = cam.transform.position - t.position;
//         if (invertForward)
//             dir = -dir;

//         if (dir.sqrMagnitude < 0.000001f)
//             return;

//         Vector3 up = keepUp ? cam.transform.up : Vector3.up;
//         t.rotation = Quaternion.LookRotation(dir, up);
//     }

//     /// <summary>
//     /// ��Ŀ�����������������Զ�����/��ȡһ���������������һ�μ��ɳ�����Ч��
//     /// </summary>
//     public static FaceCameraBillboard EnsureFaceCamera(GameObject target, Camera camera = null, bool keepUp = true, bool invertForward = false)
//     {
//         if (target == null)
//             return null;

//         var comp = target.GetComponent<FaceCameraBillboard>();
//         if (comp == null)
//             comp = target.AddComponent<FaceCameraBillboard>();

//         comp.TargetCamera = camera;
//         comp.KeepUp = keepUp;
//         comp.InvertForward = invertForward;

//         return comp;
//     }

//     /// <summary>
//     /// ���������峯�����������������ڡ�����һ�κ��Զ�ÿ֡���롱��
//     /// </summary>
//     public sealed class FaceCameraBillboard : MonoBehaviour
//     {
//         public Camera TargetCamera;
//         public bool KeepUp = true;
//         public bool InvertForward = false;

//         // UI/����ͨ���ŵ� LateUpdate�����������λ��/��������
//         private void LateUpdate()
//         {
//             UtilityLibrary.FaceCameraOnce(gameObject, TargetCamera, KeepUp, InvertForward);
//         }
//     }
// }
