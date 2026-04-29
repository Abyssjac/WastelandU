using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace JackyUtility
{
    /// <summary>
    /// Live debug window for <see cref="GridRoomManager"/>.
    /// Open via  Wasteland Debug ? Grid Room Manager  in the Unity menu bar.
    /// </summary>
    public class GridRoomManagerDebugWindow : DebugEditorWindow<GridRoomManager>
    {
        [MenuItem("Wasteland Debug/Grid Room Manager")]
        public static void ShowWindow() =>
            GetWindow<GridRoomManagerDebugWindow>("Grid Room Manager").Show();

        // Per-room foldout state keyed by RoomId
        private readonly Dictionary<int, bool> _roomFoldouts = new Dictionary<int, bool>();

        protected override void DrawContent()
        {
            var rm = Target;

            // ©¤©¤ Overview ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
            Header("Overview");
            Row("Active Rooms", rm.ActiveRooms.Count.ToString());
            Row("Policy",       rm.Editor_PolicyType.ToString());

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Force Recalculate", GUILayout.Height(22)))
                rm.Editor_ForceRecalculate();

            // ©¤©¤ Room list ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
            if (rm.ActiveRooms.Count == 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox("No rooms detected.", MessageType.None);
                return;
            }

            Header("Rooms");

            for (int i = 0; i < rm.ActiveRooms.Count; i++)
            {
                var room = rm.ActiveRooms[i];

                if (!_roomFoldouts.ContainsKey(room.RoomId))
                    _roomFoldouts[room.RoomId] = false;

                _roomFoldouts[room.RoomId] = EditorGUILayout.Foldout(
                    _roomFoldouts[room.RoomId],
                    $"Room {room.RoomId}  ¡ª  {room.CellCount} cells",
                    true);

                if (_roomFoldouts[room.RoomId])
                {
                    EditorGUI.indentLevel++;

                    int shown = 0;
                    foreach (var cell in room.Cells)
                    {
                        EditorGUILayout.LabelField(cell.ToString());
                        if (++shown >= 20) break;
                    }

                    if (room.CellCount > 20)
                        EditorGUILayout.LabelField(
                            $"... and {room.CellCount - 20} more cells",
                            EditorStyles.miniLabel);

                    EditorGUI.indentLevel--;
                }
            }
        }
    }
}
