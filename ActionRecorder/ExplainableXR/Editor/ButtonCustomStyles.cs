using UnityEngine;

public static class ButtonCustomStyles
{
    public static GUIStyle FlatButton
    {
        get
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.normal.background = MakeTex(2, 2, new Color(0.78f, 0.78f, 0.78f));
            style.active.background = MakeTex(2, 2, new Color(0.7f, 0.7f, 0.7f));
            style.focused.background = MakeTex(2, 2, new Color(0.78f, 0.78f, 0.78f));
            style.alignment = TextAnchor.MiddleLeft;
            style.normal.textColor = GUI.skin.label.normal.textColor;
            style.hover.textColor = GUI.skin.label.normal.textColor;
            style.active.textColor = GUI.skin.label.normal.textColor;
            style.focused.textColor = GUI.skin.label.normal.textColor;
            return style;
        }
    }

    public static GUIStyle SelectedButton
    {
        get
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.normal.background = MakeTex(2, 2, new Color(0.68f, 0.68f, 0.68f));
            // style.hover.background = MakeTex(2, 2, new Color(0.68f, 0.68f, 0.68f));
            style.active.background = MakeTex(2, 2, new Color(0.68f, 0.68f, 0.68f));
            style.focused.background = MakeTex(2, 2, new Color(0.68f, 0.68f, 0.68f));
            style.alignment = TextAnchor.MiddleLeft;
            style.normal.textColor = GUI.skin.label.normal.textColor;
            return style;
        }
    }

    public static GUIStyle IconButton
    {
        get
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.normal.background = null;
            style.hover.background = null;
            style.active.background = null;
            style.focused.background = null;
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = GUI.skin.button.normal.textColor;
            style.hover.textColor = GUI.skin.button.hover.textColor;
            style.active.textColor = GUI.skin.button.active.textColor;
            style.focused.textColor = GUI.skin.button.focused.textColor;
            style.fontSize = 15;
            return style;
        }
    }

    public static GUIStyle IconButton2
    {
        get
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.normal.background = MakeTex(2, 2, new Color(0.89f, 0.89f, 0.89f));
            style.hover.background = MakeTex(2, 2, new Color(0.89f, 0.89f, 0.89f));
            style.active.background = MakeTex(2, 2, new Color(0.89f, 0.89f, 0.89f));
            style.focused.background = MakeTex(2, 2, new Color(0.89f, 0.89f, 0.89f));
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = GUI.skin.button.normal.textColor;
            style.hover.textColor = GUI.skin.button.hover.textColor;
            style.active.textColor = GUI.skin.button.active.textColor;
            style.focused.textColor = GUI.skin.button.focused.textColor;
            style.fontSize = 18;
            style.fixedWidth = 18;
            style.fixedHeight = 18;
            return style;
        }
    }

    private static Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;

        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();

        return result;
    }
}
