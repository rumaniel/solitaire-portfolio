#if DEBUG
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>FPS counter overlay — active only when DEBUG is defined.</summary>
public class DebugFpsOverlay : MonoBehaviour
{
    private const float UpdateInterval = 0.5f;
    private const float Smoothing = 0.1f;

    private static DebugFpsOverlay instance;

    private TMP_Text fpsText;
    private float smoothDeltaTime;
    private float timer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (instance != null) return;

        var go = new GameObject("DebugOverlay");
        instance = go.AddComponent<DebugFpsOverlay>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        BuildUI();
    }

    private void Update()
    {
        smoothDeltaTime += (Time.unscaledDeltaTime - smoothDeltaTime) * Smoothing;
        timer += Time.unscaledDeltaTime;

        if (timer < UpdateInterval) return;

        timer = 0f;
        int fps = Mathf.RoundToInt(1f / smoothDeltaTime);
        fpsText.SetText("{0} FPS", fps);
        fpsText.color = fps >= 30 ? Color.green : Color.red;
    }

    private void BuildUI()
    {
        var canvasGo = new GameObject("DebugCanvas");
        canvasGo.transform.SetParent(transform);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        canvasGo.AddComponent<GraphicRaycaster>();

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);

        var textGo = new GameObject("FpsText");
        textGo.transform.SetParent(canvasGo.transform, false);

        fpsText = textGo.AddComponent<TextMeshProUGUI>();
        fpsText.fontSize = 28;
        fpsText.color = Color.green;
        fpsText.alignment = TextAlignmentOptions.TopLeft;
        fpsText.raycastTarget = false;

        var rect = fpsText.rectTransform;
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(20, -20);
        rect.sizeDelta = new Vector2(250, 50);
    }
}
#endif
