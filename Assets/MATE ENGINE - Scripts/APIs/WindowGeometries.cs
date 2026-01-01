using System;
using System.Threading.Tasks;
using X11;
using UnityEngine;
using UnityEngine.EventSystems;

public class WindowGeometries : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public static WindowGeometries Instance;
    
    private string sessionType;
    private string currentDesktopEnv;
    
    private Vector2 initialMousePos;
    private Vector2 initialWindowPos;
    private Vector2 lastPos;
    
    [HideInInspector]
    public bool isDragging;
    
    private void OnEnable()
    {
        Instance = this;
    }
    
    private void Start()
    {
        sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        currentDesktopEnv = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");
        print($"Current session is {currentDesktopEnv} ({sessionType}).");
    }

    private void Update()
    {
        if (!isDragging) return;
        var currentMousePos = GetMousePosition();
        var delta = currentMousePos - initialMousePos;
        var newPos = initialWindowPos + delta;
        if (newPos == lastPos) return;
        SetWindowPosition(newPos);
        lastPos = newPos;
    }
    
    public async void OnPointerDown(PointerEventData eventData)
    {
        try
        {
            initialMousePos = GetMousePosition();
            initialWindowPos = await GetWindowPosition();
            isDragging = true;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
    }

    public Vector2 GetMousePosition()
    {
        switch (sessionType)
        {
            case "x11":
                return X11Manager.Instance.GetMousePosition();
            case "wayland" when currentDesktopEnv == "Hyprland":
                return WaylandUtility.GetMousePositionHyprland();
            case "wayland":
                // Use fallback (XWayland will handle this)
                return X11Manager.Instance.GetMousePosition();
            default:
                Debug.LogError("Unsupported session type.");
                return Vector2.zero;
        }
    }

    public async Task<Vector2> GetWindowPosition()
    {
        switch (sessionType)
        {
            case "x11":
                return X11Manager.Instance.GetWindowPosition();
            case "wayland" when currentDesktopEnv == "KDE":
                return await WaylandUtility.GetWindowPositionKWin();
            case "wayland":
                // Use fallback (XWayland will handle this)
                return X11Manager.Instance.GetWindowPosition();
            default:
                Debug.LogError("Unsupported session type.");
                return Vector2.zero;
        }
    }

    public void SetWindowPosition(Vector2 pos)
    {
        switch (sessionType)
        {
            case "x11":
                X11Manager.Instance.SetWindowPosition(pos);
                break;
            case "wayland" when currentDesktopEnv == "Hyprland":
                WaylandUtility.SetWindowPositionHyprland(pos);
                break;
            case "wayland":
                // Use fallback (XWayland will handle this)
                X11Manager.Instance.SetWindowPosition(pos);
                break;
            default:
                Debug.LogError("Unsupported session type.");
                break;
        }
    }
}