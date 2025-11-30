using System;
using UnityEngine;
using System.Diagnostics;

public class WaylandUtility
{
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

    string RunCommand(string command)
    {
        ProcessStartInfo psi = new ProcessStartInfo()
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"{command}\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (Process p = Process.Start(psi))
        {
            p.WaitForExit();
            return p.StandardOutput.ReadToEnd();
        }
    }
}