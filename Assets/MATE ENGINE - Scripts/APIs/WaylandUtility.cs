using UnityEngine;
using System.Diagnostics;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Object = UnityEngine.Object;

public class WaylandUtility
{
    private static WaylandUtility Instance = new();
    
    public static Vector2 GetMousePositionHyprland() 
    {
        if (!(Time.time >= Instance.nextPollTime)) return Instance.lastMousePos;
        string output = RunCommand("/usr/bin/hyprctl cursorpos");
        string[] cursor = output.Trim().Split(',');
        Instance.lastMousePos = new Vector2(int.Parse(cursor[0]), int.Parse(cursor[1]));
        return Instance.lastMousePos;
    }
    
    private float pollInterval = 0.1f; // 10 times per second max
    private float nextPollTime = 0f;

    private Vector2 lastWinPos;
    private Vector2 lastMousePos;

    public static void SetWindowPositionHyprland(Vector2 position)
    {
        if (!(Time.time >= Instance.nextPollTime)) return;
        RunCommand($"/usr/bin/hyprctl dispatch moveactive exact {(position.x - (Screen.width / 2))} {(position.y - (Screen.height / 2))}");
        Instance.nextPollTime = Time.time + Instance.pollInterval;
    }

    public static async Task<Vector2> GetWindowPositionKWin() 
    {
        var windowGeometry = await Object.FindFirstObjectByType<KWinManager>().GetWindowGeometry();
        return new Vector2(windowGeometry.X, windowGeometry.Y);
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
