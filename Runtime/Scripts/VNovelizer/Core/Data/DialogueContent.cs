using System;
using System.Collections.Generic;
using LitJson;
using UnityEngine;

[Serializable]
public class DialogueSpeakerData
{
    public string characterId = "";
    public string emotionId = "";

    public bool IsEmpty
    {
        get { return string.IsNullOrEmpty(characterId) && string.IsNullOrEmpty(emotionId); }
    }
}

[Serializable]
public class StageCharacterData
{
    public string slot = "middle";
    public string characterId = "";
    public string emotionId = "";
}

[Serializable]
public class DialogueOptionData
{
    public string id = "";
    public string text = "";
    public string result = "";
}

/// <summary>
/// Chapter → Segment 里的一条 Content。目前支持 Dialogue 和 Video。
/// </summary>
[Serializable]
public class DialogueContent
{
    public const string TypeName = "Dialogue";
    public const string VideoTypeName = "Video";
    public const string PlaybackOnce = "once";
    public const string PlaybackLoop = "loop";

    public static readonly string[] ValidSlots = { "left", "middle", "right" };

    public string id = "";
    public string type = TypeName;
    public DialogueSpeakerData speaker = new DialogueSpeakerData();
    public string text = "";
    public List<StageCharacterData> stageCharacters = new List<StageCharacterData>();
    public string backgroundAssetId = "";
    public string voiceAssetId = "";
    public string bgmAssetId = "";
    public List<DialogueOptionData> options = new List<DialogueOptionData>();

    public string videoAssetId = "";
    public string playback = PlaybackOnce;
    public bool skippable = true;

    public static bool IsDialogueType(string type)
    {
        return string.Equals(type, TypeName, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsVideoType(string type)
    {
        return string.Equals(type, VideoTypeName, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsVideo()
    {
        return IsVideoType(type);
    }

    public bool IsLoop()
    {
        return NormalizePlayback(playback) == PlaybackLoop;
    }

    public static bool LooksLikeDialogue(JsonData item)
    {
        if (item == null || !item.IsObject) return false;
        string type = ReadString(item, "type");
        if (IsVideoType(type)) return false;
        if (item.ContainsKey("type") && IsDialogueType(type))
            return true;
        if (item.ContainsKey("speaker") && item["speaker"] != null && item["speaker"].IsObject)
            return true;
        return item.ContainsKey("backgroundAssetId") || item.ContainsKey("stageCharacters");
    }

    public static DialogueContent FromJson(JsonData item)
    {
        var content = new DialogueContent();
        if (item == null || !item.IsObject) return content;

        content.id = FirstString(item, "id", "ID");
        content.type = ReadString(item, "type");
        if (string.IsNullOrEmpty(content.type))
            content.type = TypeName;

        content.speaker = new DialogueSpeakerData();
        if (item.ContainsKey("speaker") && item["speaker"] != null && item["speaker"].IsObject)
        {
            content.speaker.characterId = ReadString(item["speaker"], "characterId");
            content.speaker.emotionId = ReadString(item["speaker"], "emotionId");
        }

        content.text = ReadString(item, "text");
        content.backgroundAssetId = FirstString(item, "backgroundAssetId", "Background");
        content.voiceAssetId = FirstString(item, "voiceAssetId", "Voice");
        content.bgmAssetId = FirstString(item, "bgmAssetId", "BGM");
        content.videoAssetId = FirstString(item, "videoAssetId", "assetId");
        content.playback = NormalizePlayback(FirstString(item, "playback", "mode"));
        content.skippable = item.ContainsKey("skippable") ? ReadBool(item, "skippable") : true;

        content.stageCharacters = new List<StageCharacterData>();
        if (item.ContainsKey("stageCharacters") && item["stageCharacters"] != null && item["stageCharacters"].IsArray)
        {
            for (int i = 0; i < item["stageCharacters"].Count; i++)
            {
                JsonData stage = item["stageCharacters"][i];
                if (stage == null || !stage.IsObject) continue;
                content.stageCharacters.Add(new StageCharacterData
                {
                    slot = NormalizeSlot(ReadString(stage, "slot")),
                    characterId = ReadString(stage, "characterId"),
                    emotionId = ReadString(stage, "emotionId")
                });
            }
        }

        content.options = ReadOptions(item);

        return content;
    }

    public static DialogueContent FromStoryLine(StoryLine line)
    {
        var content = new DialogueContent
        {
            id = line != null ? line.ID ?? "" : "",
            type = TypeName,
            text = line != null ? line.Text ?? "" : "",
            backgroundAssetId = line != null ? line.Background ?? "" : "",
            voiceAssetId = line != null ? line.Voice ?? "" : "",
            bgmAssetId = line != null ? line.BGM ?? "" : "",
            speaker = new DialogueSpeakerData(),
            stageCharacters = new List<StageCharacterData>(),
            options = new List<DialogueOptionData>()
        };

        if (line == null) return content;

        content.speaker.characterId = line.Speaker ?? "";
        content.speaker.emotionId = EmotionFromProfile(line.HeadProfile, content.speaker.characterId);

        AddStage(content, "left", line.CharLeft);
        AddStage(content, "middle", line.CharMid);
        AddStage(content, "right", line.CharRight);
        return content;
    }

    public StoryLine ToStoryLine()
    {
        var line = new StoryLine
        {
            ID = id ?? "",
            Speaker = speaker != null ? speaker.characterId ?? "" : "",
            HeadProfile = BuildHeadProfile(),
            CharLeft = BuildChar("left"),
            CharMid = BuildChar("middle"),
            CharRight = BuildChar("right"),
            Text = text ?? "",
            Background = backgroundAssetId ?? "",
            BGM = bgmAssetId ?? "",
            Voice = voiceAssetId ?? "",
            Command = "",
            Note = "",
            CompleteState = true
        };
        return line;
    }

    public List<string> Validate()
    {
        var issues = new List<string>();
        string label = string.IsNullOrEmpty(id) ? "(无ID)" : id;

        if (IsVideo())
        {
            if (string.IsNullOrWhiteSpace(videoAssetId))
                issues.Add($"Video {label}: videoAssetId 不能为空");
            return issues;
        }

        if (string.IsNullOrWhiteSpace(backgroundAssetId))
            issues.Add($"Dialogue {label}: 背景 backgroundAssetId 不能为空");

        if (speaker != null && !string.IsNullOrEmpty(speaker.characterId) && string.IsNullOrEmpty(speaker.emotionId))
            issues.Add($"Dialogue {label}: 已填写 speaker.characterId，但 emotionId 为空");

        var used = new HashSet<string>();
        if (stageCharacters == null) return issues;

        for (int i = 0; i < stageCharacters.Count; i++)
        {
            StageCharacterData stage = stageCharacters[i];
            if (stage == null) continue;

            string slot = NormalizeSlot(stage.slot);
            if (string.IsNullOrEmpty(slot) || Array.IndexOf(ValidSlots, slot) < 0)
            {
                issues.Add($"Dialogue {label}: 非法舞台位置 '{stage.slot}'，只允许 left/middle/right");
                continue;
            }

            if (!used.Add(slot))
                issues.Add($"Dialogue {label}: 舞台位置 {slot} 重复");

            if (string.IsNullOrEmpty(stage.characterId) || string.IsNullOrEmpty(stage.emotionId))
                issues.Add($"Dialogue {label}: 位置 {slot} 缺少 characterId 或 emotionId");
        }

        return issues;
    }

    public JsonData ToJsonData()
    {
        JsonData item = NewObject();
        item["id"] = id ?? "";
        item["type"] = string.IsNullOrEmpty(type) ? TypeName : type;

        if (IsVideo())
        {
            item["videoAssetId"] = videoAssetId ?? "";
            item["playback"] = NormalizePlayback(playback);
            item["skippable"] = skippable ? "true" : "false";
            JsonData videoOptions = NewArray();
            if (options != null)
            {
                for (int i = 0; i < options.Count; i++)
                {
                    DialogueOptionData option = options[i];
                    if (option == null) continue;
                    JsonData optionData = NewObject();
                    optionData["id"] = option.id ?? "";
                    optionData["text"] = option.text ?? "";
                    optionData["result"] = option.result ?? "";
                    videoOptions.Add(optionData);
                }
            }
            item["options"] = videoOptions;
            return item;
        }

        JsonData speakerData = NewObject();
        speakerData["characterId"] = speaker != null ? speaker.characterId ?? "" : "";
        speakerData["emotionId"] = speaker != null ? speaker.emotionId ?? "" : "";
        item["speaker"] = speakerData;
        item["text"] = text ?? "";

        JsonData stages = NewArray();
        if (stageCharacters != null)
        {
            for (int i = 0; i < stageCharacters.Count; i++)
            {
                StageCharacterData stage = stageCharacters[i];
                if (stage == null) continue;
                JsonData stageData = NewObject();
                stageData["slot"] = NormalizeSlot(stage.slot);
                stageData["characterId"] = stage.characterId ?? "";
                stageData["emotionId"] = stage.emotionId ?? "";
                stages.Add(stageData);
            }
        }
        item["stageCharacters"] = stages;
        item["backgroundAssetId"] = backgroundAssetId ?? "";
        item["voiceAssetId"] = voiceAssetId ?? "";
        item["bgmAssetId"] = bgmAssetId ?? "";

        JsonData optionArray = NewArray();
        if (options != null)
        {
            for (int i = 0; i < options.Count; i++)
            {
                DialogueOptionData option = options[i];
                if (option == null) continue;
                JsonData optionData = NewObject();
                optionData["id"] = option.id ?? "";
                optionData["text"] = option.text ?? "";
                optionData["result"] = option.result ?? "";
                optionArray.Add(optionData);
            }
        }
        item["options"] = optionArray;
        return item;
    }

    public bool HasOptions()
    {
        if (options == null) return false;
        for (int i = 0; i < options.Count; i++)
        {
            if (options[i] != null && !string.IsNullOrEmpty(options[i].text))
                return true;
        }
        return false;
    }

    private static List<DialogueOptionData> ReadOptions(JsonData item)
    {
        var list = new List<DialogueOptionData>();
        if (item == null || !item.IsObject || !item.ContainsKey("options") || item["options"] == null || !item["options"].IsArray)
            return list;

        JsonData array = item["options"];
        for (int i = 0; i < array.Count; i++)
        {
            JsonData option = array[i];
            if (option == null || !option.IsObject) continue;
            list.Add(new DialogueOptionData
            {
                id = FirstString(option, "id", "ID"),
                text = FirstString(option, "text", "Text"),
                result = FirstString(option, "result", "Result")
            });
        }
        return list;
    }

    public static string NormalizeSlot(string slot)
    {
        if (string.IsNullOrWhiteSpace(slot)) return "";
        switch (slot.Trim().ToLowerInvariant())
        {
            case "left":
            case "l":
                return "left";
            case "middle":
            case "mid":
            case "m":
            case "center":
                return "middle";
            case "right":
            case "r":
                return "right";
            default:
                return slot.Trim().ToLowerInvariant();
        }
    }

    private string BuildHeadProfile()
    {
        if (speaker == null || speaker.IsEmpty) return "";
        if (string.IsNullOrEmpty(speaker.characterId) || string.IsNullOrEmpty(speaker.emotionId))
            return "";
        return speaker.characterId + "_" + speaker.emotionId;
    }

    private string BuildChar(string slot)
    {
        if (stageCharacters != null)
        {
            for (int i = 0; i < stageCharacters.Count; i++)
            {
                StageCharacterData stage = stageCharacters[i];
                if (stage == null) continue;
                if (NormalizeSlot(stage.slot) != slot) continue;
                if (string.IsNullOrEmpty(stage.characterId) || string.IsNullOrEmpty(stage.emotionId))
                    return "";
                return stage.characterId + "_" + stage.emotionId;
            }
        }

        if (slot == "middle" && speaker != null && !string.IsNullOrEmpty(speaker.characterId) && !string.IsNullOrEmpty(speaker.emotionId))
            return speaker.characterId + "_" + speaker.emotionId;
        return "";
    }

    private static void AddStage(DialogueContent content, string slot, string packed)
    {
        if (string.IsNullOrEmpty(packed) || packed == "hide") return;
        int split = packed.IndexOf('_');
        if (split <= 0 || split >= packed.Length - 1) return;
        content.stageCharacters.Add(new StageCharacterData
        {
            slot = slot,
            characterId = packed.Substring(0, split),
            emotionId = packed.Substring(split + 1)
        });
    }

    private static string EmotionFromProfile(string headProfile, string characterId)
    {
        if (string.IsNullOrEmpty(headProfile) || headProfile == "hide") return "";
        if (!string.IsNullOrEmpty(characterId) && headProfile.StartsWith(characterId + "_", StringComparison.Ordinal))
            return headProfile.Substring(characterId.Length + 1);
        int split = headProfile.IndexOf('_');
        return split > 0 ? headProfile.Substring(split + 1) : "";
    }

    private static JsonData NewObject()
    {
        JsonData data = new JsonData();
        data.SetJsonType(LitJson.JsonType.Object);
        return data;
    }

    private static JsonData NewArray()
    {
        JsonData data = new JsonData();
        data.SetJsonType(LitJson.JsonType.Array);
        return data;
    }

    private static string FirstString(JsonData data, params string[] keys)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            string value = ReadString(data, keys[i]);
            if (!string.IsNullOrEmpty(value)) return value;
        }
        return "";
    }

    public static string ReadString(JsonData data, string key)
    {
        if (data == null || !data.IsObject || !data.ContainsKey(key) || data[key] == null)
            return "";
        JsonData value = data[key];
        if (value.IsString) return (string)value;
        if (value.IsInt) return ((int)value).ToString();
        if (value.IsLong) return ((long)value).ToString();
        if (value.IsBoolean) return ((bool)value) ? "true" : "false";
        return value.ToString();
    }

    public static string NormalizePlayback(string playback)
    {
        if (string.IsNullOrWhiteSpace(playback)) return PlaybackOnce;
        string value = playback.Trim();
        if (value.Equals(PlaybackLoop, StringComparison.OrdinalIgnoreCase) ||
            value.Equals("loopAfterEnd", StringComparison.OrdinalIgnoreCase))
            return PlaybackLoop;
        return PlaybackOnce;
    }

    private static bool ReadBool(JsonData data, string key)
    {
        if (data == null || !data.IsObject || !data.ContainsKey(key) || data[key] == null)
            return false;
        JsonData value = data[key];
        if (value.IsBoolean) return (bool)value;
        string text = ReadString(data, key);
        return text.Equals("true", StringComparison.OrdinalIgnoreCase) || text == "1";
    }
}
