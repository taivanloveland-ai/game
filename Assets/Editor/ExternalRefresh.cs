using System;
using System.IO;
using UnityEditor;

// Keeps finished work from interrupting the game while Taivan is playing it, and loads it as soon as he stops.
//
// While the game is playing, the editor does not pick up any changed files at all, so new code never swaps in
// mid-game (that is what was making the game crash or stop). Once he stops playing, everything that changed is
// picked up, and pressing Play again plays the new version.
//
// Writing a new line of text to Temp/RefreshRequest.txt asks for changed files to be picked up without anyone having
// to click on the Unity window. A request that arrives during play waits until play stops. Temp/RefreshStatus.txt
// says what happened, so the change can be confirmed as loaded.
[InitializeOnLoad]
static class ExternalRefresh
{
    const string RequestPath = "Temp/RefreshRequest.txt";
    const string StatusPath = "Temp/RefreshStatus.txt";
    const string HandledKey = "ExternalRefresh.Handled";
    const double SecondsBetweenChecks = 1.0;
    // Unity's own setting for what to do when scripts change while playing: 1 means wait until play stops.
    const string ScriptChangesWhilePlaying = "ScriptCompilationDuringPlay";
    const int RecompileAfterFinishedPlaying = 1;

    static double nextCheck, resumeAfter;
    static bool refreshedForResume;
    // Whether the editor is being held back from picking up changed files. Kept in the session, since the editor
    // reloads its code on entering and leaving play, which would otherwise forget it and leave the hold on for good.
    const string HoldingKey = "ExternalRefresh.Holding";
    static bool holding
    {
        get => SessionState.GetBool(HoldingKey, false);
        set => SessionState.SetBool(HoldingKey, value);
    }

    static ExternalRefresh()
    {
        // This runs every time the editor finishes loading freshly compiled code.
        WriteStatus("loaded");
        EditorPrefs.SetInt(ScriptChangesWhilePlaying, RecompileAfterFinishedPlaying);
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        if (EditorApplication.isPlaying) Hold();
        // Back in the editor with the hold still on (play stopped while the code was reloading): let go of it now.
        else if (holding && !EditorApplication.isPlayingOrWillChangePlaymode) Release();
        EditorApplication.update += Poll;
    }

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode) Hold();
        // Gives the newest changes a moment to start being taken in before play may start again.
        if (change == PlayModeStateChange.EnteredEditMode) resumeAfter = EditorApplication.timeSinceStartup + 2.0;
        else if (change == PlayModeStateChange.EnteredEditMode) Release();
    }

    // Stops the editor picking up changed files until play stops.
    static void Hold()
    {
        if (holding) return;
        holding = true;
        AssetDatabase.DisallowAutoRefresh();
        WriteStatus("playing");
    }

    // Lets the editor pick up changed files again, and picks up anything that changed during play.
    static void Release()
    {
        if (!holding) return;
        holding = false;
        AssetDatabase.AllowAutoRefresh();
        AssetDatabase.Refresh();
        WriteStatus(EditorApplication.isCompiling ? "compiling" : "refreshed");
    }

    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + SecondsBetweenChecks;
        // Update Content on the pause screen stopped the game to take in the newest changes. Once they are in, play
        // starts again (and the game goes straight back into the saved game).
        if (UnityEngine.PlayerPrefs.HasKey(PlanetOne.ResumeAfterUpdateKey) && !EditorApplication.isPlayingOrWillChangePlaymode
            && !EditorApplication.isCompiling && !EditorApplication.isUpdating && EditorApplication.timeSinceStartup >= resumeAfter)
        {
            // First make sure every change has been taken in (if any code changed, Unity reloads it, and this carries on
            // afterwards); then play.
            if (!refreshedForResume)
            {
                refreshedForResume = true;
                AssetDatabase.AllowAutoRefresh();
                holding = false;
                AssetDatabase.Refresh();
                return;
            }
            refreshedForResume = false;
            WriteStatus("starting again after Update Content");
            EditorApplication.EnterPlaymode();
            return;
        }
        if (!File.Exists(RequestPath)) return;

        string request = File.ReadAllText(RequestPath).Trim();
        if (request.Length == 0 || request == SessionState.GetString(HandledKey, "")) return;

        // Never interrupt the game: a request that comes in while it is playing waits until play stops.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            WriteStatus("waiting for play to stop");
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
