using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;
using System.IO;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using Gtk;
using UnityEngine.Localization;
using X11;
using Application = UnityEngine.Application;
using Button = UnityEngine.UI.Button;

public class UploadButtonHoldHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [HideInInspector] public AvatarLibraryMenu.AvatarEntry entry;

    [Header("UI References")]
    public Slider progressSlider;
    public TMP_Text labelText;
    public TMP_Text errorText;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip tickSound;
    public AudioClip completeSound;

    [SerializeField] private string fallbackUpload = "Upload";
    [SerializeField] private string fallbackUpdate = "Update";
    [SerializeField] private string fallbackPngMissing = "PNG Missing";

    private Coroutine holdRoutine;
    private bool isHolding;

    private LocalizedString currentLocString;

    private void OnEnable()
    {
        CancelHoldIfRunning();
        SetInteractable(true);
        UpdateButtonLabel();
    }

    private void OnDisable()
    {
        CancelHoldIfRunning();
        SetInteractable(true);
        UpdateButtonLabel();
        if (currentLocString != null)
        {
            currentLocString.StringChanged -= OnLocalizedChanged;
            currentLocString = null;
        }
    }

    private void Start()
    {
        if (entry != null && !entry.isOwner)
        {
            gameObject.SetActive(false);
            return;
        }
        UpdateButtonLabel();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (IsThumbnailMissing() || IsThumbnailTooBig())
        {
            X11Manager.Instance.SetTopmost(false);
            Gdk.Window gdkWindow = GdkX11Helper.ForeignNewForDisplay(X11Manager.Instance.UnityWindow);
            var dummyParent = new Window("");
            dummyParent.Realize();
            dummyParent.SkipTaskbarHint = true;
            dummyParent.SkipPagerHint = true;
            dummyParent.Decorated = false;
            dummyParent.Window.Reparent(gdkWindow, 0, 0);
            var dialog = new FileChooserDialog("Select PNG Thumbnail (Max 700KB)", dummyParent, FileChooserAction.Open, "Cancel", ResponseType.Cancel, "Open", ResponseType.Accept);
            var filter = new FileFilter();
            filter.Name = "Image";
            filter.AddPattern("*.png");
            dialog.AddFilter(filter);
            dialog.ShowAll();
            dialog.Response += (_, response) =>
            {
                dialog.Hide();
                if (response.ResponseId != ResponseType.Accept)
                {
                    return;
                }
                string path = dialog.Filename;  // Selected file path
                if (path.Length == 0 || !File.Exists(path))
                {
                    return;
                }
                FileInfo fi = new FileInfo(path);
                if (fi.Length > 700 * 1024)
                {
                    if (errorText != null)
                    {
                        SetErrorByKey("PNG_TOO_BIG", "PNG too big");
                    }
                    return;
                }

                string thumbnailsFolder = Path.Combine(Application.persistentDataPath, "Thumbnails");
                if (!Directory.Exists(thumbnailsFolder))
                    Directory.CreateDirectory(thumbnailsFolder);

                string safeName = Path.GetFileNameWithoutExtension(entry.filePath) + "_thumb.png";
                string destinationPath = Path.Combine(thumbnailsFolder, safeName);
                File.Copy(path, destinationPath, true);
                entry.thumbnailPath = destinationPath;

                string avatarsJsonPath = Path.Combine(Application.persistentDataPath, "avatars.json");
                if (File.Exists(avatarsJsonPath))
                {
                    var json = File.ReadAllText(avatarsJsonPath);
                    var list = JsonConvert.DeserializeObject<List<AvatarLibraryMenu.AvatarEntry>>(json);
                    var match = list.FirstOrDefault(e => e.filePath == entry.filePath);
                    if (match != null)
                    {
                        match.thumbnailPath = destinationPath;
                        File.WriteAllText(avatarsJsonPath, JsonConvert.SerializeObject(list, Formatting.Indented));
                    }
                }

                var menu = FindFirstObjectByType<AvatarLibraryMenu>();
                if (menu != null) menu.ReloadAvatars();

                if (errorText != null) errorText.text = "";
                UpdateButtonLabel();
            };
            
            return;
        }

        if (holdRoutine == null)
        {
            isHolding = true;
            holdRoutine = StartCoroutine(HoldToUpload());
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isHolding = false;
    }

    private bool IsThumbnailMissing()
    {
        return string.IsNullOrEmpty(entry.thumbnailPath) || !File.Exists(entry.thumbnailPath);
    }

    private bool IsThumbnailTooBig()
    {
        if (string.IsNullOrEmpty(entry.thumbnailPath) || !File.Exists(entry.thumbnailPath))
            return false;
        FileInfo fi = new FileInfo(entry.thumbnailPath);
        return fi.Length > 700 * 1024;
    }

    private void SetErrorByKey(string key, string fallback)
    {
        if (errorText == null) return;
        errorText.text = fallback;
        var ls = new LocalizedString("Languages (UI)", key);
        ls.StringChanged += (val) => { if (errorText != null) errorText.text = val; };
    }

    private void SetLabelImmediate(string text)
    {
        if (labelText != null) labelText.text = text;
    }

    private void OnLocalizedChanged(string val)
    {
        if (labelText != null) labelText.text = val;
    }

    private void SetLabelByKey(string key, string fallback)
    {
        if (labelText == null) return;
        if (currentLocString != null) currentLocString.StringChanged -= OnLocalizedChanged;

        labelText.text = fallback;
        currentLocString = new LocalizedString("Languages (UI)", key);
        currentLocString.StringChanged += OnLocalizedChanged;
    }

    private void UpdateButtonLabel()
    {
        if (labelText == null) return;
        if (IsThumbnailMissing() || IsThumbnailTooBig())
        {
            SetLabelByKey("PNG_MISSING", fallbackPngMissing);
            return;
        }

        if (entry != null && entry.steamFileId > 0)
            SetLabelByKey("UPDATE", fallbackUpdate);
        else
            SetLabelByKey("UPLOAD", fallbackUpload);
    }

    private IEnumerator HoldToUpload()
    {
        float duration = 5f;
        float timeHeld = 0f;
        int lastSecond = -1;
        float pitch = 1f;
        bool completed = false;
        SetLabelImmediate(Mathf.CeilToInt(duration).ToString());
        SetInteractable(false);

        while (isHolding && timeHeld < duration)
        {
            timeHeld += Time.deltaTime;
            int currentSecond = Mathf.CeilToInt(duration - timeHeld);

            if (currentSecond != lastSecond)
            {
                lastSecond = currentSecond;
                SetLabelImmediate(currentSecond.ToString());

                if (audioSource != null && tickSound != null)
                {
                    audioSource.pitch = pitch;
                    audioSource.PlayOneShot(tickSound);
                    pitch += 0.1f;
                }
            }
            yield return null;
        }

        if (timeHeld >= duration && isHolding)
        {
            completed = true;
            SetLabelImmediate("0");

            if (audioSource != null && completeSound != null)
            {
                audioSource.pitch = 1f;
                audioSource.PlayOneShot(completeSound);
            }

            yield return new WaitForSeconds(0.5f);
            
            SetLabelImmediate("Uploaded");
            yield return StartCoroutine(WaitForSteamIdAndRelabel(entry.filePath, 20f));
        }

        if (!completed)
            UpdateButtonLabel(); 
        SetInteractable(true);
        holdRoutine = null;
    }

    private IEnumerator WaitForSteamIdAndRelabel(string filePath, float timeoutSeconds)
    {
        float t = 0f;
        string avatarsJsonPath = Path.Combine(Application.persistentDataPath, "avatars.json");

        while (t < timeoutSeconds)
        {
            try
            {
                if (File.Exists(avatarsJsonPath))
                {
                    var json = File.ReadAllText(avatarsJsonPath);
                    var list = JsonConvert.DeserializeObject<List<AvatarLibraryMenu.AvatarEntry>>(json);
                    var match = list?.FirstOrDefault(e => e.filePath == filePath);

                    if (match != null && match.steamFileId != 0)
                    {
                        entry.steamFileId = match.steamFileId;
                        entry.isSteamWorkshop = match.isSteamWorkshop;
                        SetLabelByKey("UPDATE", fallbackUpdate);
                        yield break;
                    }
                }
            }
            catch { /* retry */ }

            yield return new WaitForSeconds(0.5f);
            t += 0.5f;
        }
        UpdateButtonLabel();
    }

    private void CancelHoldIfRunning()
    {
        isHolding = false;
        if (holdRoutine != null)
        {
            StopCoroutine(holdRoutine);
            holdRoutine = null;
        }
    }

    private void SetInteractable(bool value)
    {
        var btn = GetComponent<Button>();
        if (btn != null) btn.interactable = value;
    }
}