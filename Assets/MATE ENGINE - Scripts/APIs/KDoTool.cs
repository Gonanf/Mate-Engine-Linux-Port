using System;
using Debug = UnityEngine.Debug;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tmds.DBus;
using UnityEngine;

[DBusInterface("org.kde.KWin")]
interface IKWin : IDBusObject
{
    Task<string> supportInformationAsync();
}

[DBusInterface("org.kde.kwin.Scripting")]
interface IScripting : IDBusObject
{
    Task<int> loadScriptAsync(string path, string name);
    Task unloadScriptAsync(string name);
}

[DBusInterface("org.kde.kwin.Script")]
interface IScriptInstance : IDBusObject
{
    Task runAsync();
}

public class KDoTool : MonoBehaviour
{
    private Connection _connection;
    private ConnectionInfo _connectionInfo;

    private async void Start()
    {
        await RunExample();
    }

    async Task RunExample()
    {
        _connection = new Connection(Address.Session);
        _connectionInfo = await _connection.ConnectAsync();

        var geo = await GetWindowGeometry();
        if (geo != null)
        {
            Debug.Log($"Window: {geo.Width}x{geo.Height} at {geo.X},{geo.Y}");
        }
    }
    
    public static Task WaitForExitAsync(Process process, CancellationToken cancellationToken = default)
    { 
        if (process.HasExited) return Task.CompletedTask;

        var tcs = new TaskCompletionSource<object>();
        process.EnableRaisingEvents = true;

        process.Exited += (sender, args) => tcs.TrySetResult(null);
        cancellationToken.Register(() => tcs.TrySetCanceled());

        return tcs.Task;
    }

    public async Task<WindowGeometry> GetWindowGeometry()
    {
        string scriptName = "getgeo_" + Guid.NewGuid().ToString("N");
        string jsScript = $@"
            var w = workspace.activeClient;
            if (!w) {{
                print('{scriptName}:ERROR:noactive');
            }} else {{
                print('{scriptName}:GEO:' + w.geometry.x + ',' + w.geometry.y + ',' + w.geometry.width + ',' + w.geometry.height);
            }}";

        DateTime startTime = DateTime.Now;
        await ExecuteKWinScript(scriptName, jsScript);

        string service = await GetKWinServiceName();
        string since = startTime.AddSeconds(-5).ToString("yyyy-MM-dd HH:mm:ss");

        using (var process = new Process())
        {
            process.StartInfo.FileName = "journalctl";
            process.StartInfo.Arguments = $"--user -u {service} --since \"{since}\"";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            WaitForExitAsync(process);

            var lines = output.Split('\n');
            var relevant = lines.Where(l => l.Contains($"{scriptName}:")).ToList();
            if (relevant.Count == 0) 
            {
                Debug.LogError("No output found in journal");
                return null;
            }

            foreach (var line in relevant)
            {
                var logContent = line.Split(new[] { scriptName + ":" }, StringSplitOptions.None)[1];
                if (logContent.StartsWith("ERROR:"))
                {
                    Debug.LogError(logContent);
                    return null;
                }
                else if (logContent.StartsWith("GEO:"))
                {
                    var parts = logContent.Substring(4).Split(',');
                    return new WindowGeometry
                    {
                        X = int.Parse(parts[0]),
                        Y = int.Parse(parts[1]),
                        Width = int.Parse(parts[2]),
                        Height = int.Parse(parts[3])
                    };
                }
            }
        }
        return null;
    }

    private async Task<string> GetKWinServiceName()
    {
        var kwinProxy = _connection.CreateProxy<IKWin>("org.kde.KWin", "/KWin");
        string info = await kwinProxy.supportInformationAsync();
        if (info.Contains("Operation Mode: X11 only"))
            return "kwin_x11";
        return "kwin_wayland";
    }

    private async Task ExecuteKWinScript(string name, string code)
    {
        string tempFile = Path.Combine(Application.temporaryCachePath, name + ".js");
        await File.WriteAllTextAsync(tempFile, code);

        var scripting = _connection.CreateProxy<IScripting>("org.kde.KWin", "/Scripting");
        int scriptId = await scripting.loadScriptAsync(tempFile, name);
        if (scriptId < 0)
        {
            Debug.LogError("Failed to load script");
            return;
        }

        var instance = _connection.CreateProxy<IScriptInstance>("org.kde.KWin", $"/{scriptId}");
        await instance.runAsync();

        await Task.Delay(500);  // Increased for reliability

        await scripting.unloadScriptAsync(name);

        if (File.Exists(tempFile)) File.Delete(tempFile);
    }

    public class WindowGeometry
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}