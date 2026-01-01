using System;
using UnityEngine;
using System.Diagnostics;

public static class WaylandUtility
{
    public static Vector2 GetMousePositionHyprland()
    {
        string output = RunCommand("/usr/bin/hyprctl cursorpos");
        string[] cursor = output.Trim().Split(',');
        return new Vector2(float.Parse(cursor[0]),float.Parse(cursor[1]));
    }

    public static Vector2 SetWindowPositionHyprland(Vector2 position){
        RunCommand($"/usr/bin/hyprctl dispatch moveactive exact {(position.x - (Screen.width/2))} {(position.y - (Screen.height/2))}");
        return position;
    }

    public Vector2 GetWindowPositionKWin()
    {
        string output = RunCommand(Application.streamingAssetsPath + "/kdotool search --name 'MateEngineX' getwindowgeometry");
        string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length >= 2 && 
            int.TryParse(lines[0].Trim(), out int x) && 
            int.TryParse(lines[1].Trim(), out int y))
        {
            return new Vector2(x, y);
        }
        return Vector2.zero;
    }

    static string RunCommand(string command)
    {
        ProcessStartInfo psi = new ProcessStartInfo()
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"{command}\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process p = Process.Start(psi);
        p?.WaitForExit();
        return p?.StandardOutput.ReadToEnd();
    }
}
