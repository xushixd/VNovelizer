using System.Collections.Generic;
using System.Text;
using UnityEngine;
using LitJson;

/// <summary>
/// 剧本解析工具类：支持 CSV 与 JSON 两种剧本格式，根据内容自动判断。
/// </summary>
public static class ScriptParser
{
    public class ScriptData
    {
        public ChapterData Chapter;
        public List<StoryLine> Lines = new List<StoryLine>();
        public Dictionary<string, int> IDMap = new Dictionary<string, int>();
        public bool IsChapter { get { return Chapter != null; } }
    }

    /// <summary>
    /// JSON 剧本容器。新版是 Chapter → Segment → Content，没有 line。
    /// 旧版 CSV / {"lines":[StoryLine]} 仍可解析。
    /// </summary>
    [System.Serializable]
    public class JsonScriptContainer
    {
        public List<StoryLine> lines;
    }

    /// <summary>
    /// 解析剧本文件（根据内容自动判断 CSV / JSON）
    /// </summary>
    public static ScriptData Parse(string fileName)
    {
        // 从配置路径加载
        string configPath = VNProjectConfig.Instance.VNScriptResPath;
        string loadPath = configPath + "/" + fileName;
        Debug.Log($"[ScriptParser] 尝试加载剧本: {loadPath} (ConfigPath: {configPath}, FileName: {fileName})");

        TextAsset textAsset = Resources.Load<TextAsset>(loadPath);

        if (textAsset == null)
        {
            Debug.LogError($"[ScriptParser] 找不到剧本文件: {loadPath}");
            return null;
        }

        return ParseText(textAsset.text);
    }

    /// <summary>
    /// 解析剧本文本（根据内容自动判断 CSV / JSON）。供编辑器预览和导入校验复用。
    /// </summary>
    public static ScriptData ParseText(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        string trimmed = content.TrimStart();
        bool isJson = trimmed.StartsWith("{");
        return isJson ? ParseJson(content) : ParseCsv(content);
    }

    /// <summary>
    /// 解析 CSV 剧本
    /// </summary>
    private static ScriptData ParseCsv(string csvContent)
    {
        ScriptData data = new ScriptData();

        // 【修复】使用改进的行分割方法，正确处理引号内的换行符
        string[] lines = SplitCSVLines(csvContent);
        bool isFirstLine = true;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // 跳过标题行
            if (isFirstLine)
            {
                isFirstLine = false;
                continue;
            }

            string[] columns = SplitCSV(line);
            if (columns.Length >= 12) // 增加了 HeadProfile 列，现在需要 12 列
            {
                StoryLine storyLine = new StoryLine
                {
                    ID = columns[0].Trim(),
                    Speaker = columns[1].Trim(),
                    HeadProfile = columns[2].Trim(), // 新增：HeadProfile 列
                    CharLeft = columns[3].Trim(),
                    CharMid = columns[4].Trim(),
                    CharRight = columns[5].Trim(),
                    Text = columns[6].Trim(),
                    Background = columns[7].Trim(),
                    BGM = columns[8].Trim(),
                    Voice = columns[9].Trim(),
                    Command = columns[10].Trim(),
                    Note = columns[11].Trim()
                };

                data.Lines.Add(storyLine);
                // 记录ID索引
                if (!string.IsNullOrEmpty(storyLine.ID))
                {
                    data.IDMap[storyLine.ID] = data.Lines.Count - 1;
                }
            }
        }
        return data;
    }

    /// <summary>
    /// 解析 JSON 剧本
    /// </summary>
    private static ScriptData ParseJson(string jsonContent)
    {
        ScriptData data = new ScriptData();

        ChapterData chapter = TryParseChapter(jsonContent);
        if (chapter != null)
        {
            data.Chapter = chapter;
            return data;
        }

        List<StoryLine> lines = TryParseJsonLines(jsonContent);
        if (lines == null)
        {
            Debug.LogError("[ScriptParser] JSON 剧本无法识别。新版请使用 Chapter.segments.content，旧版使用 {\"lines\":[...]}");
            return null;
        }

        foreach (StoryLine line in lines)
        {
            if (line == null) continue;
            data.Lines.Add(line);
            if (!string.IsNullOrEmpty(line.ID))
                data.IDMap[line.ID] = data.Lines.Count - 1;
        }
        return data;
    }

    public static ChapterData TryParseChapter(string jsonText)
    {
        if (string.IsNullOrEmpty(jsonText)) return null;

        JsonData root;
        try { root = JsonMapper.ToObject(jsonText); }
        catch (System.Exception e)
        {
            Debug.LogError("[ScriptParser] JSON 解析失败: " + e.Message);
            return null;
        }

        if (root == null || !root.IsObject) return null;
        if (!root.ContainsKey("segments") || root["segments"] == null || !root["segments"].IsArray)
            return null;

        var chapter = new ChapterData
        {
            id = FirstLegacy(root, "id", "ID"),
            title = DialogueContent.ReadString(root, "title"),
            entrySegmentId = DialogueContent.ReadString(root, "entrySegmentId"),
            segments = new List<SegmentData>()
        };
        if (string.IsNullOrEmpty(chapter.id)) chapter.id = "001";

        JsonData segments = root["segments"];
        for (int i = 0; i < segments.Count; i++)
        {
            JsonData item = segments[i];
            if (item == null || !item.IsObject) continue;
            chapter.segments.Add(ReadSegment(item));
        }

        if (chapter.segments.Count == 0) return null;
        if (string.IsNullOrEmpty(chapter.entrySegmentId))
            chapter.entrySegmentId = chapter.segments[0].id;
        return chapter;
    }

    public static string SerializeChapter(ChapterData chapter)
    {
        JsonData root = new JsonData();
        root.SetJsonType(LitJson.JsonType.Object);
        if (chapter == null) chapter = new ChapterData();
        root["id"] = chapter.id ?? "";
        root["title"] = chapter.title ?? "";
        root["entrySegmentId"] = chapter.entrySegmentId ?? "";

        JsonData segmentArray = new JsonData();
        segmentArray.SetJsonType(LitJson.JsonType.Array);
        if (chapter.segments != null)
        {
            for (int i = 0; i < chapter.segments.Count; i++)
            {
                SegmentData segment = chapter.segments[i];
                if (segment == null) continue;
                segmentArray.Add(WriteSegment(segment));
            }
        }
        root["segments"] = segmentArray;

        var writer = new JsonWriter { PrettyPrint = true };
        JsonMapper.ToJson(root, writer);
        return writer.ToString();
    }

    /// <summary>
    /// 将 JSON 文本解析为剧本行列表；解析失败或不是剧本结构（无 lines 数组）时返回 null。
    /// 供运行时解析与编辑器优化工具复用。
    /// </summary>
    public static List<StoryLine> TryParseJsonLines(string jsonText)
    {
        if (string.IsNullOrEmpty(jsonText)) return null;

        JsonData root;
        try
        {
            root = JsonMapper.ToObject(jsonText);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ScriptParser] JSON 剧本解析失败: {e.Message}");
            return null;
        }

        if (root == null || !root.IsObject) return null;

        List<JsonData> items = CollectDialogueItems(root);
        if (items == null) return null;

        var lines = new List<StoryLine>();
        for (int i = 0; i < items.Count; i++)
        {
            JsonData item = items[i];
            if (item == null || !item.IsObject) continue;

            if (DialogueContent.LooksLikeDialogue(item))
            {
                DialogueContent dialogue = DialogueContent.FromJson(item);
                List<string> issues = dialogue.Validate();
                for (int n = 0; n < issues.Count; n++)
                    Debug.LogError("[ScriptParser] " + issues[n]);
                lines.Add(dialogue.ToStoryLine());
            }
            else
            {
                lines.Add(ReadLegacyStoryLine(item));
            }
        }

        return lines;
    }

    /// <summary>
    /// 将剧本行序列化为带缩进的 JSON。CompleteState 行写出新版 Dialogue 结构。
    /// </summary>
    public static string SerializeJsonLines(List<StoryLine> lines)
    {
        JsonData root = new JsonData();
        root.SetJsonType(LitJson.JsonType.Object);
        JsonData array = new JsonData();
        array.SetJsonType(LitJson.JsonType.Array);

        if (lines != null)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                StoryLine line = lines[i];
                if (line == null) continue;
                if (line.CompleteState)
                    array.Add(DialogueContent.FromStoryLine(line).ToJsonData());
                else
                    array.Add(WriteLegacyStoryLine(line));
            }
        }

        root["lines"] = array;
        var writer = new JsonWriter { PrettyPrint = true };
        JsonMapper.ToJson(root, writer);
        return writer.ToString();
    }

    private static SegmentData ReadSegment(JsonData item)
    {
        var segment = new SegmentData
        {
            id = FirstLegacy(item, "id", "ID"),
            title = DialogueContent.ReadString(item, "title"),
            nextSegmentId = DialogueContent.ReadString(item, "nextSegmentId"),
            content = new List<DialogueContent>()
        };

        if (item.ContainsKey("content") && item["content"] != null && item["content"].IsArray)
        {
            JsonData contents = item["content"];
            for (int i = 0; i < contents.Count; i++)
            {
                JsonData content = contents[i];
                if (content == null || !content.IsObject) continue;
                string type = DialogueContent.ReadString(content, "type");
                if (!string.IsNullOrEmpty(type) && !DialogueContent.IsDialogueType(type) && !DialogueContent.IsVideoType(type))
                {
                    Debug.LogWarning("[ScriptParser] 暂未播放的 Content 类型: " + type + " (" + FirstLegacy(content, "id", "ID") + ")");
                    continue;
                }
                DialogueContent storyContent = DialogueContent.FromJson(content);
                List<string> issues = storyContent.Validate();
                for (int n = 0; n < issues.Count; n++)
                    Debug.LogError("[ScriptParser] " + issues[n]);
                segment.content.Add(storyContent);
            }
        }

        return segment;
    }

    private static JsonData WriteSegment(SegmentData segment)
    {
        JsonData item = new JsonData();
        item.SetJsonType(LitJson.JsonType.Object);
        item["id"] = segment.id ?? "";
        item["title"] = segment.title ?? "";

        JsonData contentArray = new JsonData();
        contentArray.SetJsonType(LitJson.JsonType.Array);
        if (segment.content != null)
        {
            for (int i = 0; i < segment.content.Count; i++)
            {
                if (segment.content[i] != null)
                    contentArray.Add(segment.content[i].ToJsonData());
            }
        }
        item["content"] = contentArray;
        item["nextSegmentId"] = segment.nextSegmentId ?? "";
        return item;
    }

    private static List<JsonData> CollectDialogueItems(JsonData root)
    {
        if (root.ContainsKey("lines") && root["lines"] != null && root["lines"].IsArray)
            return ToList(root["lines"]);
        return null;
    }

    private static List<JsonData> ToList(JsonData array)
    {
        var items = new List<JsonData>();
        for (int i = 0; i < array.Count; i++)
            items.Add(array[i]);
        return items;
    }

    private static StoryLine ReadLegacyStoryLine(JsonData item)
    {
        return new StoryLine
        {
            ID = FirstLegacy(item, "ID", "id"),
            Speaker = DialogueContent.ReadString(item, "Speaker"),
            HeadProfile = DialogueContent.ReadString(item, "HeadProfile"),
            CharLeft = DialogueContent.ReadString(item, "CharLeft"),
            CharMid = DialogueContent.ReadString(item, "CharMid"),
            CharRight = DialogueContent.ReadString(item, "CharRight"),
            Text = FirstLegacy(item, "Text", "text"),
            Background = DialogueContent.ReadString(item, "Background"),
            BGM = DialogueContent.ReadString(item, "BGM"),
            Voice = DialogueContent.ReadString(item, "Voice"),
            Command = DialogueContent.ReadString(item, "Command"),
            Note = DialogueContent.ReadString(item, "Note"),
            CompleteState = false
        };
    }

    private static JsonData WriteLegacyStoryLine(StoryLine line)
    {
        JsonData item = new JsonData();
        item.SetJsonType(LitJson.JsonType.Object);
        item["ID"] = line.ID ?? "";
        item["Speaker"] = line.Speaker ?? "";
        item["HeadProfile"] = line.HeadProfile ?? "";
        item["CharLeft"] = line.CharLeft ?? "";
        item["CharMid"] = line.CharMid ?? "";
        item["CharRight"] = line.CharRight ?? "";
        item["Text"] = line.Text ?? "";
        item["Background"] = line.Background ?? "";
        item["BGM"] = line.BGM ?? "";
        item["Voice"] = line.Voice ?? "";
        item["Command"] = line.Command ?? "";
        item["Note"] = line.Note ?? "";
        return item;
    }

    private static string FirstLegacy(JsonData item, params string[] keys)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            string value = DialogueContent.ReadString(item, keys[i]);
            if (!string.IsNullOrEmpty(value)) return value;
        }
        return "";
    }

    /// <summary>
    /// 按 ID 从小到大排序剧本行（数值优先比较，非数值回退字符串比较）。
    /// </summary>
    public static void SortLinesById(List<StoryLine> lines)
    {
        lines.Sort(CompareLineById);
    }

    private static int CompareLineById(StoryLine a, StoryLine b)
    {
        string idA = a != null ? a.ID ?? "" : "";
        string idB = b != null ? b.ID ?? "" : "";

        bool numA = long.TryParse(idA, out long valA);
        bool numB = long.TryParse(idB, out long valB);

        if (numA && numB) return valA.CompareTo(valB);
        if (numA != numB) return numA ? -1 : 1; // 数字排在非数字前
        return string.CompareOrdinal(idA, idB);
    }

    /// <summary>
    /// 正确分割CSV行，处理引号内的换行符
    /// 只有在引号外遇到换行符时才分割行
    /// </summary>
    private static string[] SplitCSVLines(string csvContent)
    {
        List<string> lines = new List<string>();
        bool inQuotes = false;
        StringBuilder currentLine = new StringBuilder();

        for (int i = 0; i < csvContent.Length; i++)
        {
            char c = csvContent[i];
            char nextChar = (i + 1 < csvContent.Length) ? csvContent[i + 1] : '\0';

            if (c == '"')
            {
                // 处理转义的双引号（两个连续的双引号表示一个双引号字符）
                if (inQuotes && nextChar == '"')
                {
                    currentLine.Append('"');
                    i++; // 跳过下一个双引号
                }
                else
                {
                    inQuotes = !inQuotes;
                    currentLine.Append(c);
                }
            }
            else if ((c == '\n' || c == '\r') && !inQuotes)
            {
                // 只有在引号外遇到换行符时才分割行
                // 处理 \r\n 的情况（Windows换行符）
                if (c == '\r' && nextChar == '\n')
                {
                    i++; // 跳过 \n
                }

                // 如果当前行不为空，添加到列表
                if (currentLine.Length > 0)
                {
                    lines.Add(currentLine.ToString());
                    currentLine.Clear();
                }
            }
            else
            {
                // 引号内的换行符或其他字符，直接添加到当前行
                currentLine.Append(c);
            }
        }

        // 添加最后一行（如果有内容）
        if (currentLine.Length > 0)
        {
            lines.Add(currentLine.ToString());
        }

        return lines.ToArray();
    }

    /// <summary>
    /// 分割CSV行中的各个字段，处理引号内的逗号
    /// </summary>
    private static string[] SplitCSV(string line)
    {
        List<string> fields = new List<string>();
        bool inQuotes = false;
        StringBuilder currentField = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            char nextChar = (i + 1 < line.Length) ? line[i + 1] : '\0';

            if (c == '"')
            {
                // 处理转义的双引号（两个连续的双引号表示一个双引号字符）
                if (inQuotes && nextChar == '"')
                {
                    currentField.Append('"');
                    i++; // 跳过下一个双引号
                }
                else
                {
                    inQuotes = !inQuotes;
                    // 不添加引号本身到字段内容中（CSV标准）
                }
            }
            else if (c == ',' && !inQuotes)
            {
                // 只有在引号外遇到逗号时才分割字段
                fields.Add(currentField.ToString());
                currentField.Clear();
            }
            else
            {
                currentField.Append(c);
            }
        }

        // 添加最后一个字段
        fields.Add(currentField.ToString());
        return fields.ToArray();
    }
}
