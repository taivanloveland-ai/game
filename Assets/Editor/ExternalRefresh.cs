using System;
using System.IO;
using UnityEditor;

// Lets finished work be loaded into the open editor from outside, without anyone having to click on the Unity window.
// Writing a new line of text to Temp/RefreshRequest.txt makes the editor stop the game if it is playing and pick up
// every changed file. Temp/RefreshStatus.txt then says what happened, so the change can be confirmed as loaded.
[InitializeOnLoad]
static class ExternalRefresh
{
    const string RequestPath = "Temp/RefreshRequest.txt";
    const string StatusPath = "Temp/RefreshStatus.txt";
    const string HandledKey = "ExternalRefresh.Handled";
    const double SecondsBetweenChecks = 1.0;

    static double nextCheck;

    static ExternalRefresh()
    {
        // This runs every time the editor finishes loading freshly compiled code.
        WriteStatus("loaded");
        EditorApplication.update += Poll;
    }

    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + SecondsBetweenChecks;
        if (!File.Exists(RequestPath)) return;

        string request = File.ReadAllText(RequestPath).Trim();
        if (request.Length == 0 || request == SessionState.GetString(HandledKey, "")) return;

        // New code cannot be swapped in while the game is running, so stop it first and come back on the next check.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            return;
        }

        SessionState.SetString(HandledKey, request);
        AssetDatabase.Refresh();
        // If scripts changed, the editor now recompiles and the status becomes "loaded" when that is done.
        WriteStatus(EditorApplication.isCompiling ? "compiling" : "refreshed");
    }

    static void WriteStatus(string state)
    {
        try
        {
            string handled = SessionState.GetString(HandledKey, "");
            File.WriteAllText(StatusPath, state + "|" + handled + "|" + DateTime.UtcNow.ToString("o"));
        }
        catch (IOException)
        {
            // The status file is only a courtesy; never let it break the editor.
        }
    }
}
