// Minimal Unity IMGUI surface used for headless type-checking. See README.md.
#pragma warning disable CA1050, IDE0060

namespace UnityEngine
{
    public struct Rect
    {
        public Rect(float x, float y, float width, float height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }

        public float x { get; set; }

        public float y { get; set; }

        public float width { get; set; }

        public float height { get; set; }
    }

    public class GUIStyle
    {
        public GUIStyle() { }

        public GUIStyle(GUIStyle other) { }

        public int fontSize { get; set; }

        public bool richText { get; set; }

        public bool wordWrap { get; set; }
    }

    public class GUISkin : ScriptableObject
    {
        public GUIStyle label { get; set; }

        public GUIStyle box { get; set; }

        public GUIStyle button { get; set; }
    }

    public static class GUI
    {
        public static GUISkin skin { get; set; }

        public static Color color { get; set; }

        public static void Label(Rect position, string text) { }

        public static void Label(Rect position, string text, GUIStyle style) { }

        public static bool Button(Rect position, string text) => false;

        public static void Box(Rect position, string text) { }
    }
}
