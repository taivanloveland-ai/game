using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Makes the game fill the screen when it is played. In the editor, pressing Play makes the Game view take up the
// whole Unity window ("Play Maximized"), so maximise the Unity window too for the whole screen. A built copy of the
// game opens full screen.
[InitializeOnLoad]
static class PlayMaximized
{
    static PlayMaximized()
    {
        // Done once the editor has finished starting up, when the Game view exists.
        EditorApplication.delayCall += Apply;
        EditorApplication.playModeStateChanged += change =>
        {
            if (change == PlayModeStateChange.ExitingEditMode) Apply();
        };
    }

    static void Apply()
    {
        // The Game view's "Play Maximized" setting is not part of Unity's public scripting interface, so it is set by
        // name. Newer editors call it enterPlayModeBehavior; older ones, maximizeOnPlay.
        Type gameView = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        if (gameView == null) return;
        foreach (UnityEngine.Object view in Resources.FindObjectsOfTypeAll(gameView))
        {
            PropertyInfo behaviour = gameView.GetProperty("enterPlayModeBehavior", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (behaviour != null && behaviour.CanWrite)
            {
                // The setting's choices, by name: play focused, play maximized, or play unfocused.
                object maximized = Enum.Parse(behaviour.PropertyType, "PlayMaximized");
                if (!behaviour.GetValue(view).Equals(maximized)) behaviour.SetValue(view, maximized);
                continue;
            }
            PropertyInfo maximizeOnPlay = gameView.GetProperty("maximizeOnPlay", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (maximizeOnPlay != null && maximizeOnPlay.CanWrite && !(bool)maximizeOnPlay.GetValue(view)) maximizeOnPlay.SetValue(view, true);
        }
    }
}
