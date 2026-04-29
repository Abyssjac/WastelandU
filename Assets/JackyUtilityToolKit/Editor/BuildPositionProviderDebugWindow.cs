using UnityEditor;

namespace JackyUtility
{
    /// <summary>
    /// Live debug window for <see cref="BuildPositionProvider"/>.
    /// Open via  Wasteland Debug ? Build Position Provider  in the Unity menu bar.
    /// </summary>
    public class BuildPositionProviderDebugWindow : DebugEditorWindow<BuildPositionProvider>
    {
        [MenuItem("Wasteland Debug/Build Position Provider")]
        public static void ShowWindow() =>
            GetWindow<BuildPositionProviderDebugWindow>("Build Position Provider").Show();

        protected override void DrawContent()
        {
            var pp = Target;

            Header("Raycast");
            Row("Has Valid Hit", pp.HasValidHit.ToString());

            if (!pp.HasValidHit)
            {
                EditorGUILayout.HelpBox("No valid raycast hit this frame.", MessageType.None);
                return;
            }

            Row("Cell",           pp.CurrentCell.ToString());
            Row("Hit World",      pp.CurrentHitWorldPosition.ToString("F3"));
            Row("Snapped",        pp.CurrentSnappedWorldPosition.ToString("F2"));
            Row("Snapped Center", pp.CurrentSnappedWorldPositionCenter.ToString("F2"));

            Header("Hit Buildable");
            var hitB = pp.CurrentHitBuildable;
            if (hitB?.Data != null)
            {
                var d = hitB.Data;
                Row("Instance ID",  d.InstanceId);
                Row("Enum Key",     d.Property.EnumKey.ToString());
                Row("Anchor Cell",  d.AnchorCell.ToString());
                Row("Rotation",     $"{d.RotationStep}  ({d.RotationStep * 90}бу)");
            }
            else
            {
                EditorGUILayout.LabelField("None");
            }
        }
    }
}
