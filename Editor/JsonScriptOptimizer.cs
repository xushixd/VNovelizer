using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// JSON 剧本优化工具：将剧本按行 ID 从小到大排序并重写文件。
/// </summary>
public static class JsonScriptOptimizer
{
    [MenuItem("VNovelizer/JSON 剧本优化（按 ID 排序）")]
    public static void OptimizeAll()
    {
        string folder = GetScriptFolder();
        if (string.IsNullOrEmpty(folder)) return;

        string[] files = Directory.GetFiles(folder, "*.json", SearchOption.AllDirectories);
        int sortedCount = 0, unchangedCount = 0, failedCount = 0;
        foreach (string file in files)
        {
            if (OptimizeFile(file, out bool changed, out _))
            {
                if (changed) sortedCount++;
                else unchangedCount++;
            }
            else
            {
                failedCount++;
            }
        }

        AssetDatabase.Refresh();

        string summary = $"优化完成：排序 {sortedCount} 个，无需调整 {unchangedCount} 个，失败 {failedCount} 个。";
        Debug.Log($"[JsonScriptOptimizer] {summary}");
        EditorUtility.DisplayDialog("JSON 优化", summary, "确定");
    }

    public static bool OptimizeFile(string file, out bool changed, out string error)
    {
        changed = false;
        error = null;

        try
        {
            string text = File.ReadAllText(file);
            ChapterData chapter = ScriptParser.TryParseChapter(text);
            if (chapter != null)
            {
                string pretty = ScriptParser.SerializeChapter(chapter);
                if (pretty == text) return true;
                File.WriteAllText(file, pretty, new UTF8Encoding(false));
                changed = true;
                return true;
            }

            List<StoryLine> lines = ScriptParser.TryParseJsonLines(text);
            if (lines == null)
            {
                error = "JSON 不是 Chapter 或旧版 lines 剧本。";
                return false;
            }

            List<StoryLine> sorted = new List<StoryLine>(lines);
            ScriptParser.SortLinesById(sorted);
            if (SameOrder(lines, sorted)) return true;

            File.WriteAllText(file, ScriptParser.SerializeJsonLines(sorted), new UTF8Encoding(false));
            changed = true;
            return true;
        }
        catch (System.Exception e)
        {
            error = e.Message;
            Debug.LogError($"[JsonScriptOptimizer] 处理失败 {file}: {e.Message}");
            return false;
        }
    }

    private static string GetScriptFolder()
    {
        var config = VNProjectConfig.Instance;
        if (config == null || string.IsNullOrEmpty(config.VNScriptResPath))
        {
            EditorUtility.DisplayDialog("JSON 优化", "未找到 VNProjectConfig 或 VNScriptResPath 未配置。", "确定");
            return null;
        }

        string folder = Path.Combine(Application.dataPath, "Resources", config.VNScriptResPath);
        if (!Directory.Exists(folder))
        {
            EditorUtility.DisplayDialog("JSON 优化", $"剧本目录不存在：\n{folder}", "确定");
            return null;
        }

        return folder;
    }

    private static bool SameOrder(List<StoryLine> a, List<StoryLine> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (!ReferenceEquals(a[i], b[i])) return false;
        }
        return true;
    }
}
