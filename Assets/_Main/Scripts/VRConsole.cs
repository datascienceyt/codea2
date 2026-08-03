using UnityEngine;
using UnityEngine.UI;

public class VRConsole : MonoBehaviour
{
    public Text logText;
    public int maxLines = 10;

    void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    void HandleLog(string message, string stackTrace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception) return;

        logText.text += $"\n[{type}] {message}";

        var lines = logText.text.Split('\n');
        if (lines.Length > maxLines)
            logText.text = string.Join("\n", lines[^maxLines..]);
    }
}