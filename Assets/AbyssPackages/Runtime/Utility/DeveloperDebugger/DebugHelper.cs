using UnityEngine;

namespace JackyUtility
{
    /// <summary>
    /// Lightweight immediate-mode debug panel drawn with OnGUI.
    /// Usage:
    ///   void OnGUI()
    ///   {
    ///       var panel = DebugGUIPanel.Begin(new Vector2(10, 10), 420f, 14);
    ///       panel.DrawLine("<b>¨T¨T¨T My Debug ¨T¨T¨T</b>");
    ///       panel.DrawLine($"Health: {hp}");
    ///       panel.End();
    ///   }
    /// </summary>
    public class DebugGUIPanel
    {
        // ©¤©¤ Layout state ©¤©¤
        private float x;
        private float y;
        private float startY;
        private float panelWidth;
        private float lineHeight;
        private int maxLines;
        private GUIStyle style;

        // reuse a single instance to avoid GC each frame
        private static readonly DebugGUIPanel shared = new DebugGUIPanel();

        private DebugGUIPanel() { }

        /// <summary>
        /// Start drawing a debug panel. Call DrawLine() afterwards, then End().
        /// </summary>
        /// <param name="position">Top-left corner in screen space.</param>
        /// <param name="width">Panel width in pixels.</param>
        /// <param name="lineCount">How many lines the background should reserve.</param>
        /// <param name="lineHeight">Height per line in pixels.</param>
        /// <param name="fontSize">Font size.</param>
        /// <param name="bgColor">Background tint color.</param>
        public static DebugGUIPanel Begin(
            Vector2 position,
            float width,
            int lineCount,
            float lineHeight = 18f,
            int fontSize = 13,
            Color? bgColor = null)
        {
            var p = shared;
            p.x = position.x;
            p.y = position.y;
            p.startY = position.y;
            p.panelWidth = width;
            p.lineHeight = lineHeight;
            p.maxLines = lineCount;

            // draw background
            Color bg = bgColor ?? new Color(0f, 0f, 0f, 0.75f);
            Color prevColor = GUI.color;
            GUI.color = bg;
            GUI.DrawTexture(new Rect(p.x, p.y, width, lineHeight * lineCount), Texture2D.whiteTexture);
            GUI.color = prevColor;

            // prepare style
            if (p.style == null)
            {
                p.style = new GUIStyle(GUI.skin.label)
                {
                    richText = true,
                };
            }
            p.style.fontSize = fontSize;

            return p;
        }

        /// <summary>Draw a single line of rich text and advance the cursor.</summary>
        public void DrawLine(string text)
        {
            GUI.Label(new Rect(x + 6f, y, panelWidth, lineHeight), text, style);
            y += lineHeight;
        }

        /// <summary>Insert a blank line (spacer).</summary>
        public void Space(float pixels = -1f)
        {
            y += pixels < 0f ? lineHeight : pixels;
        }

        /// <summary>
        /// Finish drawing. Currently a no-op but keeps the API symmetrical
        /// and gives a future hook for borders / separators.
        /// </summary>
        public void End() { }
    }
}