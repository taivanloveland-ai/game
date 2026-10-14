using System.Collections.Generic;
using UnityEngine;

// Draws all the writing on screen, and makes sure it can be read: every label and every button's words are made
// smaller, if need be, until they fit inside the box they are drawn in. Nothing is ever cut off by the edge of its
// box, whatever font the game uses and whatever size the window is.
public static class GameText
{
    // Writing is never made smaller than this.
    const int SmallestSize = 7;
    // The size that was found to fit, for writing already measured: by its style, its words, its box and the size
    // it started at.
    static readonly Dictionary<(GUIStyle, string, int, int, int), int> fitted = new Dictionary<(GUIStyle, string, int, int, int), int>();
    static readonly GUIContent measured = new GUIContent();

    // Every piece of writing that had to be made smaller to fit, and by how much: for checking the game's screens.
    public static readonly Dictionary<string, string> MadeSmaller = new Dictionary<string, string>();
    // Every piece of writing drawn, with the size it was drawn at.
    public static readonly Dictionary<string, int> Drawn = new Dictionary<string, int>();

    public static void Label(Rect box, string words, GUIStyle style)
    {
        int usual = style.fontSize;
        style.fontSize = SizeThatFits(box, words, style);
        GUI.Label(box, words, style);
        style.fontSize = usual;
    }

    public static bool Button(Rect box, string words, GUIStyle style)
    {
        int usual = style.fontSize;
        style.fontSize = SizeThatFits(box, words, style);
        bool pressed = GUI.Button(box, words, style);
        style.fontSize = usual;
        return pressed;
    }

    // True if the words fit in the box at the style's present size. Writing that runs on to more lines (a style
    // with word wrap) fits if all its lines fit the height and its longest word fits the width.
    static bool Fits(Rect box, string words, GUIStyle style)
    {
        if (!style.wordWrap)
        {
            Vector2 needs = style.CalcSize(measured);
            return needs.x <= box.width + 0.5f && needs.y <= box.height + 0.5f;
        }
        if (style.CalcHeight(measured, box.width) > box.height + 0.5f) return false;
        style.wordWrap = false;
        bool wordsFit = true;
        foreach (string word in words.Split(' ', '\n'))
        {
            measured.text = word;
            if (style.CalcSize(measured).x > box.width + 0.5f) wordsFit = false;
        }
        measured.text = words;
        style.wordWrap = true;
        return wordsFit;
    }

    // The same, in the usual style of a label or a button.
    public static void Label(Rect box, string words)
    {
        Label(box, words, GUI.skin.label);
    }

    public static bool Button(Rect box, string words)
    {
        return Button(box, words, GUI.skin.button);
    }

    // The biggest size, no bigger than the style's own, at which the words fit in the box.
    public static int SizeThatFits(Rect box, string words, GUIStyle style)
    {
        int usual = style.fontSize > 0 ? style.fontSize : 13;
        if (string.IsNullOrEmpty(words)) return usual;
        var key = (style, words, Mathf.RoundToInt(box.width), Mathf.RoundToInt(box.height), usual);
        if (fitted.TryGetValue(key, out int size)) return size;

        measured.text = words;
        for (size = usual; size > SmallestSize; size--)
        {
            style.fontSize = size;
            if (Fits(box, words, style)) break;
        }
        style.fontSize = usual;

        if (fitted.Count > 4000) fitted.Clear();
        fitted[key] = size;
        Drawn[words] = size;
        if (size < usual) MadeSmaller[words] = usual + " -> " + size + " in a box " + key.Item3 + " x " + key.Item4;
        return size;
    }
}
