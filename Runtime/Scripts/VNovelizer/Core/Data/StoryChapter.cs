using System;
using System.Collections.Generic;
using LitJson;

/// <summary>
/// 新版故事数据：Chapter → Segment → Content。没有 line。
/// </summary>
[Serializable]
public class ChapterData
{
    public string id = "001";
    public string title = "第一章";
    public string entrySegmentId = "";
    public List<SegmentData> segments = new List<SegmentData>();

    public SegmentData FindSegment(string segmentId)
    {
        if (segments == null || string.IsNullOrEmpty(segmentId)) return null;
        for (int i = 0; i < segments.Count; i++)
        {
            if (segments[i] != null && segments[i].id == segmentId)
                return segments[i];
        }
        return null;
    }

    public bool TryFindContent(string contentId, out SegmentData segment, out int contentIndex)
    {
        segment = null;
        contentIndex = -1;
        if (segments == null || string.IsNullOrEmpty(contentId)) return false;

        for (int i = 0; i < segments.Count; i++)
        {
            SegmentData item = segments[i];
            if (item == null || item.content == null) continue;
            for (int n = 0; n < item.content.Count; n++)
            {
                if (item.content[n] != null && item.content[n].id == contentId)
                {
                    segment = item;
                    contentIndex = n;
                    return true;
                }
            }
        }
        return false;
    }

    public static ChapterData CreateSample()
    {
        var chapter = new ChapterData
        {
            id = "001",
            title = "第一章",
            entrySegmentId = "001-0001"
        };
        var segment = new SegmentData
        {
            id = "001-0001",
            title = "开场",
            nextSegmentId = ""
        };
        segment.content.Add(new DialogueContent
        {
            id = "001-0001-00001",
            type = DialogueContent.TypeName,
            speaker = new DialogueSpeakerData(),
            text = "",
            backgroundAssetId = "Shrine",
            voiceAssetId = "",
            bgmAssetId = "",
            stageCharacters = new List<StageCharacterData>()
        });
        chapter.segments.Add(segment);
        return chapter;
    }
}

[Serializable]
public class SegmentData
{
    public string id = "";
    public string title = "";
    public List<DialogueContent> content = new List<DialogueContent>();
    public string nextSegmentId = "";
}
