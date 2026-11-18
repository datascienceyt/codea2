using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Compila el APK de Quest en Build/app.apk con las escenas y el keystore de los ajustes del
/// proyecto. Informe en Logs/Codea_Compilacion.txt.
///
/// Sin abrir Unity:
/// Unity.exe -batchmode -quit -projectPath . -buildTarget Android -executeMethod CodeaBuild.BuildBatch
/// </summary>
public static class CodeaBuild
{
    private const string Output = "Build/app.apk";
    private const string Report = "Logs/Codea_Compilacion.txt";

    [MenuItem("Tools/Codea/Compilar el APK", false, 90)]
    private static void BuildMenu() => Build();

    public static void BuildBatch()
    {
        bool ok = Build();
        EditorApplication.Exit(ok ? 0 : 1);
    }

    private static bool Build()
    {
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Output,
            target = BuildTarget.Android,
            options = BuildOptions.None
        });

        BuildSummary summary = report.summary;
        string text = $"[Codea] Compilación: {summary.result} · {PlayerSettings.bundleVersion} " +
                      $"(código {PlayerSettings.Android.bundleVersionCode}) · {string.Join(", ", scenes)}\n" +
                      $"  {Output} · {summary.totalSize / (1024f * 1024f):0.0} MB · {summary.totalTime.TotalMinutes:0.0} min · " +
                      $"{summary.totalErrors} error(es), {summary.totalWarnings} aviso(s)\n";

        foreach (BuildStep step in report.steps)
            foreach (BuildStepMessage message in step.messages)
                if (message.type == LogType.Error || message.type == LogType.Exception)
                    text += $"  ✗ {message.content}\n";

        Directory.CreateDirectory(Path.GetDirectoryName(Report));
        File.WriteAllText(Report, text);
        Debug.Log(text);

        return summary.result == BuildResult.Succeeded;
    }
}
