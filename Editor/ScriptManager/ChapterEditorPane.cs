using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// JSON Chapter 的树状编辑：左树右检视，同级拖拽排序后重写 ID。
/// </summary>
public class ChapterEditorPane : VisualElement
{
    private const string NoneLabel = "(无)";
    private const string EmptySpeaker = "(旁白)";

    private readonly VisualElement treeHost;
    private readonly ScrollView treeScroll;
    private readonly ScrollView inspector;
    private readonly Label status;

    private string filePath;
    private ChapterData chapter;
    private readonly List<TreeRow> rows = new List<TreeRow>();
    private readonly HashSet<string> collapsed = new HashSet<string>();
    private TreeRow selected;
    private TreeRow dragging;
    private int dropIndex = -1;
    private bool saving;
    private string newContentType = DialogueContent.TypeName;

    public ChapterEditorPane()
    {
        style.flexGrow = 1;
        style.flexDirection = FlexDirection.Column;

        status = new Label { style = { height = 20, color = Color.green, marginBottom = 4 } };
        Add(status);

        var split = new TwoPaneSplitView(0, 320, TwoPaneSplitViewOrientation.Horizontal);
        split.style.flexGrow = 1;
        Add(split);

        treeScroll = new ScrollView { style = { flexGrow = 1, backgroundColor = new Color(0.16f, 0.16f, 0.16f) } };
        treeHost = treeScroll.contentContainer;
        treeScroll.RegisterCallback<MouseMoveEvent>(evt =>
        {
            if (dragging == null) return;
            UpdateDropTarget(evt.mousePosition);
        });
        treeScroll.RegisterCallback<MouseUpEvent>(evt =>
        {
            if (dragging == null) return;
            FinishDrag();
            evt.StopPropagation();
        });
        inspector = new ScrollView { style = { flexGrow = 1, paddingLeft = 10, paddingRight = 8, paddingTop = 6 } };
        split.Add(treeScroll);
        split.Add(inspector);
    }

    public void ClearEditor()
    {
        filePath = null;
        chapter = null;
        selected = null;
        dragging = null;
        dropIndex = -1;
        rows.Clear();
        treeHost.Clear();
        inspector.Clear();
        status.text = "";
    }

    public void Load(string path, ChapterData data)
    {
        filePath = path;
        chapter = data;
        selected = null;
        collapsed.Clear();
        RebuildTree();
        ShowChapterInspector();
        status.text = "已加载：" + Path.GetFileName(path);
    }

    public void RefreshAssetLists()
    {
        if (chapter == null) return;
        BindInspector();
    }

    private void RebuildTree()
    {
        rows.Clear();
        treeHost.Clear();
        if (chapter == null) return;

        var chapterRow = new TreeRow { Kind = RowKind.Chapter, Chapter = chapter, Label = chapter.id + "  " + chapter.title };
        rows.Add(chapterRow);

        if (chapter.segments != null)
        {
            for (int i = 0; i < chapter.segments.Count; i++)
            {
                SegmentData segment = chapter.segments[i];
                if (segment == null) continue;
                var segmentRow = new TreeRow
                {
                    Kind = RowKind.Segment,
                    Segment = segment,
                    Label = segment.id + "  " + (string.IsNullOrEmpty(segment.title) ? "(未命名)" : segment.title)
                };
                rows.Add(segmentRow);
                if (collapsed.Contains(segment.id) || segment.content == null) continue;
                for (int n = 0; n < segment.content.Count; n++)
                {
                    DialogueContent content = segment.content[n];
                    if (content == null) continue;
                    rows.Add(new TreeRow
                    {
                        Kind = RowKind.Content,
                        Segment = segment,
                        Content = content,
                        Label = ContentLabel(content)
                    });
                }
            }
        }

        var toolbar = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, paddingLeft = 6, paddingTop = 4, paddingBottom = 4 } };
        toolbar.Add(SmallButton("+ Segment", AddSegment));
        treeHost.Add(toolbar);

        for (int i = 0; i < rows.Count; i++)
            treeHost.Add(MakeRowView(rows[i], i));
    }

    private VisualElement MakeRowView(TreeRow row, int index)
    {
        var view = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                alignItems = Align.Center,
                height = 26,
                paddingLeft = row.Kind == RowKind.Chapter ? 6 : row.Kind == RowKind.Segment ? 18 : 36,
                backgroundColor = row == selected ? new Color(0.23f, 0.33f, 0.47f) : Color.clear
            }
        };
        if (index == dropIndex)
            view.style.borderTopWidth = 2;
        view.style.borderTopColor = new Color(0.89f, 0.76f, 0.48f);

        if (row.Kind == RowKind.Segment)
        {
            bool closed = collapsed.Contains(row.Segment.id);
            var fold = new Button(() => ToggleSegment(row.Segment.id)) { text = closed ? "▸" : "▾", style = { width = 18, height = 18, marginRight = 2 } };
            view.Add(fold);
        }

        var label = new Label(row.Label) { style = { flexGrow = 1, unityTextAlign = TextAnchor.MiddleLeft } };
        if (row.Kind == RowKind.Content && row.Content != null && row.Content.IsVideo())
            label.style.color = new Color(0.75f, 0.85f, 1f);
        view.Add(label);
        if (row.Kind == RowKind.Segment)
            view.Add(SmallButton("+ Content", () => AddContent(row.Segment, newContentType)));
        if (row.Kind == RowKind.Content)
            view.Add(SmallButton("复制", () => DuplicateContent(row.Segment, row.Content)));
        view.userData = row;

        view.RegisterCallback<MouseDownEvent>(evt =>
        {
            if (evt.button != 0) return;
            if (HitsButton(evt.target as VisualElement)) return;
            Select(row);
            if (row.Kind == RowKind.Chapter) return;
            dragging = row;
            dropIndex = -1;
            evt.StopPropagation();
        });
        return view;
    }

    private void UpdateDropTarget(Vector2 mouse)
    {
        int next = -1;
        for (int i = 0; i < treeHost.childCount; i++)
        {
            var child = treeHost[i];
            var row = child.userData as TreeRow;
            if (row == null) continue;
            if (child.worldBound.Contains(mouse))
            {
                next = rows.IndexOf(row);
                break;
            }
        }
        if (next == dropIndex) return;
        dropIndex = next;
        PaintDropLine();
    }

    private void PaintDropLine()
    {
        for (int i = 0; i < treeHost.childCount; i++)
        {
            var child = treeHost[i];
            var row = child.userData as TreeRow;
            child.style.borderTopWidth = row != null && rows.IndexOf(row) == dropIndex ? 2 : 0;
        }
    }

    private void FinishDrag()
    {
        TreeRow source = dragging;
        int target = dropIndex;
        dragging = null;
        dropIndex = -1;
        if (source == null || target < 0 || target >= rows.Count) { RebuildTreeKeepSelection(); return; }

        TreeRow dest = rows[target];
        if (source == dest) { RebuildTreeKeepSelection(); return; }

        if (source.Kind == RowKind.Segment && dest.Kind == RowKind.Segment)
            MoveSegment(source.Segment, dest.Segment);
        else if (source.Kind == RowKind.Content && dest.Kind == RowKind.Content)
            MoveContent(source.Segment, source.Content, dest.Segment, dest.Content, false);
        else if (source.Kind == RowKind.Content && dest.Kind == RowKind.Segment)
            MoveContent(source.Segment, source.Content, dest.Segment, null, true);
        else
        {
            RebuildTreeKeepSelection();
            return;
        }

        selected = FindRow(source);
        Persist(true);
        RebuildTreeKeepSelection();
        BindInspector();
    }

    private void MoveSegment(SegmentData source, SegmentData dest)
    {
        int from = chapter.segments.IndexOf(source);
        int to = chapter.segments.IndexOf(dest);
        if (from < 0 || to < 0 || from == to) return;
        chapter.segments.RemoveAt(from);
        if (from < to) to--;
        chapter.segments.Insert(to, source);
    }

    private void MoveContent(SegmentData fromSeg, DialogueContent content, SegmentData toSeg, DialogueContent dest, bool append)
    {
        if (fromSeg == null || toSeg == null || content == null) return;
        fromSeg.content.Remove(content);
        if (append || dest == null)
        {
            toSeg.content.Add(content);
            return;
        }
        int to = toSeg.content.IndexOf(dest);
        if (to < 0) toSeg.content.Add(content);
        else toSeg.content.Insert(to, content);
    }

    private void ToggleSegment(string id)
    {
        if (!collapsed.Add(id)) collapsed.Remove(id);
        RebuildTreeKeepSelection();
    }

    private void Select(TreeRow row)
    {
        selected = row;
        RebuildTreeKeepSelection();
        BindInspector();
    }

    private void RebuildTreeKeepSelection()
    {
        TreeRow keep = selected;
        RebuildTree();
        selected = FindRow(keep);
        if (selected != null)
        {
            foreach (var child in treeHost.Children())
            {
                if (child.userData == selected)
                    child.style.backgroundColor = new Color(0.23f, 0.33f, 0.47f);
            }
        }
    }

    private TreeRow FindRow(TreeRow hint)
    {
        if (hint == null) return null;
        for (int i = 0; i < rows.Count; i++)
        {
            TreeRow row = rows[i];
            if (hint.Kind == RowKind.Chapter && row.Kind == RowKind.Chapter) return row;
            if (hint.Kind == RowKind.Segment && row.Segment == hint.Segment) return row;
            if (hint.Kind == RowKind.Content && row.Content == hint.Content) return row;
        }
        return rows.Count > 0 ? rows[0] : null;
    }

    private void BindInspector()
    {
        if (selected == null || selected.Kind == RowKind.Chapter) ShowChapterInspector();
        else if (selected.Kind == RowKind.Segment) ShowSegmentInspector(selected.Segment);
        else ShowContentInspector(selected.Segment, selected.Content);
    }

    private void ShowChapterInspector()
    {
        inspector.Clear();
        inspector.Add(Header("Chapter"));
        inspector.Add(TextRow("ID", chapter.id, value =>
        {
            chapter.id = value.Trim();
            Persist(true);
        }));
        inspector.Add(TextRow("名称", chapter.title, value =>
        {
            chapter.title = value;
            Persist(false);
            RefreshTreeLabels();
        }));
        inspector.Add(DropdownRow("入口 Segment", SegmentChoices(), MatchChoice(SegmentChoices(), chapter.entrySegmentId), value =>
        {
            chapter.entrySegmentId = ParseId(value);
            Persist(false);
        }));
    }

    private void ShowSegmentInspector(SegmentData segment)
    {
        inspector.Clear();
        inspector.Add(Header("Segment  " + segment.id));
        inspector.Add(TextRow("名称", segment.title, value =>
        {
            segment.title = value;
            Persist(false);
            RefreshTreeLabels();
        }));
        inspector.Add(DropdownRow("下一 Segment", SegmentChoices(segment.id), MatchChoice(SegmentChoices(segment.id), segment.nextSegmentId), value =>
        {
            segment.nextSegmentId = ParseId(value);
            Persist(false);
        }));

        var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 8 } };
        row.Add(new Button(() => AddContent(segment, newContentType)) { text = "+ Content" });
        row.Add(new Button(() => DeleteSegment(segment)) { text = "删除", style = { backgroundColor = new Color(0.6f, 0.2f, 0.2f) } });
        inspector.Add(row);
    }

    private void ShowContentInspector(SegmentData segment, DialogueContent content)
    {
        inspector.Clear();
        inspector.Add(Header((content.IsVideo() ? "Video  " : "Dialogue  ") + content.id));
        inspector.Add(DropdownRow("类型", new List<string> { DialogueContent.TypeName, DialogueContent.VideoTypeName }, content.IsVideo() ? DialogueContent.VideoTypeName : DialogueContent.TypeName, value =>
        {
            content.type = value;
            Persist(false);
            ShowContentInspector(segment, content);
            RefreshTreeLabels();
        }));

        if (content.IsVideo())
        {
            inspector.Add(DropdownRow("视频", ResourceChoices(VideoFolder(), "t:VideoClip"), DisplayAsset(content.videoAssetId), value =>
            {
                content.videoAssetId = ParseId(value);
                Persist(false);
            }));
            inspector.Add(DropdownRow("播放", new List<string> { DialogueContent.PlaybackOnce, DialogueContent.PlaybackLoop }, content.IsLoop() ? DialogueContent.PlaybackLoop : DialogueContent.PlaybackOnce, value =>
            {
                content.playback = value;
                Persist(false);
            }));
            inspector.Add(ToggleRow("可跳过", content.skippable, value =>
            {
                content.skippable = value;
                Persist(false);
            }));
        }
        else
        {
            inspector.Add(DropdownRow("背景", ResourceChoices(BackgroundFolder(), "t:Sprite"), DisplayAsset(content.backgroundAssetId), value =>
            {
                content.backgroundAssetId = ParseId(value);
                Persist(false);
            }));
            inspector.Add(DropdownRow("说话人", SpeakerChoices(), DisplaySpeaker(content.speaker), value =>
            {
                if (content.speaker == null) content.speaker = new DialogueSpeakerData();
                content.speaker.characterId = ParseId(value);
                if (string.IsNullOrEmpty(content.speaker.characterId)) content.speaker.emotionId = "";
                Persist(false);
                ShowContentInspector(segment, content);
            }));
            inspector.Add(DropdownRow("表情", EmotionChoices(content.speaker != null ? content.speaker.characterId : ""), DisplayAsset(content.speaker != null ? content.speaker.emotionId : ""), value =>
            {
                if (content.speaker == null) content.speaker = new DialogueSpeakerData();
                content.speaker.emotionId = ParseId(value);
                Persist(false);
            }));
            inspector.Add(StageRow(content, "left", "左立绘"));
            inspector.Add(StageRow(content, "middle", "中立绘"));
            inspector.Add(StageRow(content, "right", "右立绘"));
            inspector.Add(AreaRow("对白", content.text, value =>
            {
                content.text = value;
                Persist(false);
                RefreshTreeLabels();
            }));
            inspector.Add(DropdownRow("BGM", ResourceChoices(BgmFolder(), "t:AudioClip"), DisplayAsset(content.bgmAssetId), value =>
            {
                content.bgmAssetId = ParseId(value);
                Persist(false);
            }));
            inspector.Add(DropdownRow("语音", ResourceChoices(VoiceFolder(), "t:AudioClip"), DisplayAsset(content.voiceAssetId), value =>
            {
                content.voiceAssetId = ParseId(value);
                Persist(false);
            }));
        }

        inspector.Add(OptionsBlock(content));
        inspector.Add(new Button(() => DeleteContent(segment, content))
        {
            text = "删除这条 Content",
            style = { marginTop = 10, backgroundColor = new Color(0.6f, 0.2f, 0.2f) }
        });
    }

    private VisualElement OptionsBlock(DialogueContent content)
    {
        if (content.options == null) content.options = new List<DialogueOptionData>();
        bool enabled = HasUsableOption(content);

        var box = new VisualElement { style = { marginTop = 12 } };
        var toggle = new Toggle("添加选项") { value = enabled };
        toggle.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue)
            {
                if (!HasUsableOption(content))
                    content.options.Add(new DialogueOptionData { text = "", result = "" });
            }
            else
            {
                content.options.Clear();
            }
            Persist(true);
            BindInspector();
        });
        box.Add(toggle);
        if (!enabled) return box;

        List<string> targets = AllSegmentChoices();
        for (int i = 0; i < content.options.Count; i++)
        {
            DialogueOptionData option = content.options[i];
            if (option == null) continue;
            int captured = i;
            var row = new VisualElement { style = { marginTop = 8, marginLeft = 8 } };
            var text = new TextField("选项文本") { value = option.text ?? "", multiline = true };
            text.style.minHeight = 48;
            text.RegisterCallback<FocusOutEvent>(_ =>
            {
                option.text = text.value;
                Persist(false);
            });
            row.Add(text);
            row.Add(DropdownRow("跳转 Segment", targets, MatchChoice(targets, option.result), value =>
            {
                option.result = ParseId(value);
                Persist(false);
            }));
            row.Add(new Button(() =>
            {
                content.options.RemoveAt(captured);
                if (!HasUsableOption(content)) content.options.Clear();
                Persist(true);
                BindInspector();
            })
            { text = "删除这个选项", style = { alignSelf = Align.FlexStart, backgroundColor = new Color(0.6f, 0.2f, 0.2f) } });
            box.Add(row);
        }

        box.Add(new Button(() =>
        {
            content.options.Add(new DialogueOptionData { text = "", result = "" });
            Persist(true);
            BindInspector();
        })
        { text = "+ 选项", style = { marginTop = 6, alignSelf = Align.FlexStart } });
        return box;
    }

    private static Button SmallButton(string text, Action click)
    {
        var button = new Button(click)
        {
            text = text,
            style = { height = 20, marginLeft = 2, marginRight = 2, fontSize = 11 }
        };
        button.RegisterCallback<MouseDownEvent>(evt => evt.StopPropagation());
        return button;
    }

    private static bool HitsButton(VisualElement target)
    {
        while (target != null)
        {
            if (target is Button) return true;
            target = target.parent;
        }
        return false;
    }

    private void AddSegment()
    {
        if (chapter.segments == null) chapter.segments = new List<SegmentData>();
        var segment = new SegmentData { title = "新段落", content = new List<DialogueContent>() };
        chapter.segments.Add(segment);
        selected = new TreeRow { Kind = RowKind.Segment, Segment = segment };
        Persist(true);
        RebuildTreeKeepSelection();
        BindInspector();
    }

    private void AddContent(SegmentData segment, string type)
    {
        if (segment.content == null) segment.content = new List<DialogueContent>();
        var content = new DialogueContent
        {
            type = DialogueContent.IsVideoType(type) ? DialogueContent.VideoTypeName : DialogueContent.TypeName,
            speaker = new DialogueSpeakerData(),
            text = "",
            skippable = true,
            playback = DialogueContent.PlaybackOnce
        };
        segment.content.Add(content);
        collapsed.Remove(segment.id);
        selected = new TreeRow { Kind = RowKind.Content, Segment = segment, Content = content };
        Persist(true);
        RebuildTreeKeepSelection();
        BindInspector();
    }

    private void DuplicateContent(SegmentData segment, DialogueContent source)
    {
        if (segment == null || source == null) return;
        if (segment.content == null) segment.content = new List<DialogueContent>();
        DialogueContent copy = CloneContent(source);
        int index = segment.content.IndexOf(source);
        if (index < 0) segment.content.Add(copy);
        else segment.content.Insert(index + 1, copy);
        collapsed.Remove(segment.id);
        selected = new TreeRow { Kind = RowKind.Content, Segment = segment, Content = copy };
        Persist(true);
        RebuildTreeKeepSelection();
        BindInspector();
    }

    private static DialogueContent CloneContent(DialogueContent source)
    {
        var copy = new DialogueContent
        {
            type = source.type,
            text = source.text,
            backgroundAssetId = source.backgroundAssetId,
            voiceAssetId = source.voiceAssetId,
            bgmAssetId = source.bgmAssetId,
            videoAssetId = source.videoAssetId,
            playback = source.playback,
            skippable = source.skippable,
            speaker = new DialogueSpeakerData
            {
                characterId = source.speaker != null ? source.speaker.characterId : "",
                emotionId = source.speaker != null ? source.speaker.emotionId : ""
            },
            stageCharacters = new List<StageCharacterData>(),
            options = new List<DialogueOptionData>()
        };

        if (source.stageCharacters != null)
        {
            for (int i = 0; i < source.stageCharacters.Count; i++)
            {
                StageCharacterData stage = source.stageCharacters[i];
                if (stage == null) continue;
                copy.stageCharacters.Add(new StageCharacterData
                {
                    slot = stage.slot,
                    characterId = stage.characterId,
                    emotionId = stage.emotionId
                });
            }
        }

        if (source.options != null)
        {
            for (int i = 0; i < source.options.Count; i++)
            {
                DialogueOptionData option = source.options[i];
                if (option == null) continue;
                copy.options.Add(new DialogueOptionData
                {
                    text = option.text,
                    result = option.result
                });
            }
        }

        return copy;
    }

    private void DeleteSegment(SegmentData segment)
    {
        if (!EditorUtility.DisplayDialog("删除 Segment", "确定删除 " + segment.id + " 及其全部 Content 吗？", "删除", "取消"))
            return;
        chapter.segments.Remove(segment);
        selected = new TreeRow { Kind = RowKind.Chapter, Chapter = chapter };
        Persist(true);
        RebuildTreeKeepSelection();
        BindInspector();
    }

    private void DeleteContent(SegmentData segment, DialogueContent content)
    {
        segment.content.Remove(content);
        selected = new TreeRow { Kind = RowKind.Segment, Segment = segment };
        Persist(true);
        RebuildTreeKeepSelection();
        BindInspector();
    }

    private void Persist(bool remapIds)
    {
        if (chapter == null || string.IsNullOrEmpty(filePath) || saving) return;
        saving = true;
        try
        {
            string oldChapterId = chapter.id;
            Dictionary<string, string> map = remapIds ? ChapterIdUtility.Remap(chapter) : new Dictionary<string, string>();
            File.WriteAllText(filePath, ScriptParser.SerializeChapter(chapter) + "\n", new UTF8Encoding(false));
            if (remapIds && map.Count > 0)
                ChapterIdUtility.RemapRouteMap(oldChapterId, chapter, map);
            if (remapIds)
                AssetDatabase.Refresh();
            status.text = "已保存 " + Path.GetFileName(filePath);
        }
        finally
        {
            saving = false;
        }
    }

    private void RefreshTreeLabels()
    {
        RebuildTreeKeepSelection();
    }

    private static string ContentLabel(DialogueContent content)
    {
        string type = content.IsVideo() ? "Video" : "Dialogue";
        string name = content.IsVideo()
            ? (string.IsNullOrEmpty(content.videoAssetId) ? "(未选视频)" : content.videoAssetId)
            : FirstLine(content.text);
        return content.id + "  [" + type + "]  " + name;
    }

    private static string FirstLine(string text)
    {
        if (string.IsNullOrEmpty(text)) return "(空对白)";
        text = text.Replace("\n", " ");
        return text.Length > 18 ? text.Substring(0, 18) + "…" : text;
    }

    private List<string> SegmentChoices(string exclude = null)
    {
        var list = new List<string> { NoneLabel };
        AppendSegmentChoices(list, chapter, "", exclude);
        return list;
    }

    private List<string> AllSegmentChoices()
    {
        var list = new List<string> { NoneLabel };
        AppendSegmentChoices(list, chapter, "", null);
        string folder = ScriptFolder();
        if (!Directory.Exists(folder)) return list;

        string[] files = Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly);
        for (int i = 0; i < files.Length; i++)
        {
            if (files[i].EndsWith(".graph.json")) continue;
            ChapterData other = ScriptParser.TryParseChapter(File.ReadAllText(files[i]));
            if (other == null) continue;
            if (chapter != null && other.id == chapter.id) continue;
            AppendSegmentChoices(list, other, Path.GetFileNameWithoutExtension(files[i]), null);
        }
        return list;
    }

    private static void AppendSegmentChoices(List<string> list, ChapterData data, string prefix, string exclude)
    {
        if (data == null || data.segments == null) return;
        for (int i = 0; i < data.segments.Count; i++)
        {
            SegmentData segment = data.segments[i];
            if (segment == null || string.IsNullOrEmpty(segment.id) || segment.id == exclude) continue;
            string title = string.IsNullOrEmpty(segment.title) ? segment.id : segment.title;
            string label = string.IsNullOrEmpty(prefix)
                ? segment.id + "  " + title
                : segment.id + "  " + prefix + " / " + title;
            if (!list.Contains(label)) list.Add(label);
        }
    }

    private static string ScriptFolder()
    {
        if (VNProjectConfig.Instance != null && !string.IsNullOrEmpty(VNProjectConfig.Instance.VNScriptResPath))
            return Path.Combine(Application.dataPath, "Resources", VNProjectConfig.Instance.VNScriptResPath);
        return Path.Combine(Application.dataPath, "Resources/VNovelizerRes/VNScripts");
    }

    private VisualElement StageRow(DialogueContent content, string slot, string title)
    {
        StageCharacterData stage = FindStage(content, slot);
        string characterId = stage != null ? stage.characterId : "";
        string emotionId = stage != null ? stage.emotionId : "";
        var box = new VisualElement { style = { marginBottom = 4 } };
        box.Add(DropdownRow(title, SpeakerChoices(), DisplaySpeaker(new DialogueSpeakerData { characterId = characterId }), value =>
        {
            SetStage(content, slot, ParseId(value), "");
            Persist(false);
            BindInspector();
        }));
        box.Add(DropdownRow(title + "表情", EmotionChoices(characterId), DisplayAsset(emotionId), value =>
        {
            SetStage(content, slot, characterId, ParseId(value));
            Persist(false);
        }));
        return box;
    }

    private static StageCharacterData FindStage(DialogueContent content, string slot)
    {
        if (content == null || content.stageCharacters == null) return null;
        for (int i = 0; i < content.stageCharacters.Count; i++)
        {
            StageCharacterData stage = content.stageCharacters[i];
            if (stage != null && DialogueContent.NormalizeSlot(stage.slot) == slot)
                return stage;
        }
        return null;
    }

    private static void SetStage(DialogueContent content, string slot, string characterId, string emotionId)
    {
        if (content.stageCharacters == null) content.stageCharacters = new List<StageCharacterData>();
        StageCharacterData stage = FindStage(content, slot);
        if (string.IsNullOrEmpty(characterId))
        {
            if (stage != null) content.stageCharacters.Remove(stage);
            return;
        }
        if (stage == null)
        {
            stage = new StageCharacterData { slot = slot };
            content.stageCharacters.Add(stage);
        }
        stage.characterId = characterId;
        stage.emotionId = emotionId;
        if (string.IsNullOrEmpty(stage.emotionId))
        {
            List<string> emotions = new List<string>();
            string folder = CharacterFolder();
            if (AssetDatabase.IsValidFolder(folder))
            {
                string[] guids = AssetDatabase.FindAssets("t:CharacterProfile", new[] { folder });
                for (int i = 0; i < (guids != null ? guids.Length : 0); i++)
                {
                    var profile = AssetDatabase.LoadAssetAtPath<CharacterProfile>(AssetDatabase.GUIDToAssetPath(guids[i]));
                    if (profile == null || profile.CharacterID != characterId || profile.ElementSprites == null) continue;
                    for (int n = 0; n < profile.ElementSprites.Count; n++)
                    {
                        if (profile.ElementSprites[n] != null && !string.IsNullOrEmpty(profile.ElementSprites[n].Element))
                            emotions.Add(profile.ElementSprites[n].Element);
                    }
                }
            }
            if (emotions.Count > 0) stage.emotionId = emotions[0];
        }
    }

    private static bool HasUsableOption(DialogueContent content)
    {
        if (content == null || content.options == null) return false;
        for (int i = 0; i < content.options.Count; i++)
        {
            if (content.options[i] != null) return true;
        }
        return false;
    }

    private static string DisplayId(string id)
    {
        return string.IsNullOrEmpty(id) ? NoneLabel : id;
    }

    private static string DisplayAsset(string id)
    {
        return string.IsNullOrEmpty(id) ? NoneLabel : id;
    }

    private static string DisplaySpeaker(DialogueSpeakerData speaker)
    {
        if (speaker == null || string.IsNullOrEmpty(speaker.characterId)) return EmptySpeaker;
        return speaker.characterId;
    }

    private static string ParseId(string value)
    {
        if (string.IsNullOrEmpty(value) || value == NoneLabel || value == EmptySpeaker) return "";
        int space = value.IndexOf("  ", StringComparison.Ordinal);
        return space > 0 ? value.Substring(0, space) : value;
    }

    private static string MatchChoice(List<string> choices, string id)
    {
        if (choices == null || choices.Count == 0) return NoneLabel;
        if (string.IsNullOrEmpty(id)) return NoneLabel;
        for (int i = 0; i < choices.Count; i++)
        {
            if (choices[i] == id || choices[i].StartsWith(id + "  ", StringComparison.Ordinal))
                return choices[i];
        }
        return id;
    }

    private List<string> SpeakerChoices()
    {
        var list = new List<string> { EmptySpeaker };
        string folder = CharacterFolder();
        if (!AssetDatabase.IsValidFolder(folder)) return list;
        string[] guids = AssetDatabase.FindAssets("t:CharacterProfile", new[] { folder });
        for (int i = 0; i < guids.Length; i++)
        {
            var profile = AssetDatabase.LoadAssetAtPath<CharacterProfile>(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (profile != null && !string.IsNullOrEmpty(profile.CharacterID))
                list.Add(profile.CharacterID);
        }
        return list;
    }

    private List<string> EmotionChoices(string characterId)
    {
        var list = new List<string> { NoneLabel };
        if (string.IsNullOrEmpty(characterId)) return list;
        string folder = CharacterFolder();
        if (!AssetDatabase.IsValidFolder(folder)) return list;
        string[] guids = AssetDatabase.FindAssets("t:CharacterProfile", new[] { folder });
        for (int i = 0; i < guids.Length; i++)
        {
            var profile = AssetDatabase.LoadAssetAtPath<CharacterProfile>(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (profile == null || profile.CharacterID != characterId || profile.ElementSprites == null) continue;
            for (int n = 0; n < profile.ElementSprites.Count; n++)
            {
                if (profile.ElementSprites[n] != null && !string.IsNullOrEmpty(profile.ElementSprites[n].Element))
                    list.Add(profile.ElementSprites[n].Element);
            }
        }
        return list;
    }

    private static List<string> ResourceChoices(string folder, string filter)
    {
        var list = new List<string> { NoneLabel };
        if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder)) return list;
        string[] guids = AssetDatabase.FindAssets(filter, new[] { folder });
        var seen = new HashSet<string>();
        for (int i = 0; i < guids.Length; i++)
        {
            string name = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (seen.Add(name)) list.Add(name);
        }
        return list;
    }

    private static string BackgroundFolder()
    {
        return ResourceFolder(VNProjectConfig.Instance != null ? VNProjectConfig.Instance.BackgroundResPath : "VNovelizerRes/Backgrounds");
    }

    private static string VideoFolder()
    {
        return ResourceFolder(VNProjectConfig.Instance != null ? VNProjectConfig.Instance.VideoResPath : "VNovelizerRes/Videos");
    }

    private static string BgmFolder()
    {
        return ResourceFolder(VNProjectConfig.Instance != null ? VNProjectConfig.Instance.BgmResPath : "VNovelizerRes/Audio/Music/BGM");
    }

    private static string VoiceFolder()
    {
        return ResourceFolder(VNProjectConfig.Instance != null ? VNProjectConfig.Instance.VoiceResPath : "VNovelizerRes/Audio/Voice");
    }

    private static string CharacterFolder()
    {
        return ResourceFolder(VNProjectConfig.Instance != null ? VNProjectConfig.Instance.CharacterResPath : "VNovelizerRes/Characters");
    }

    private static string ResourceFolder(string relative)
    {
        return "Assets/Resources/" + (relative ?? "").Trim('/');
    }

    private static Label Header(string text)
    {
        return new Label(text) { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 13, marginBottom = 8 } };
    }

    private static VisualElement TextRow(string title, string value, Action<string> changed)
    {
        var field = new TextField(title) { value = value ?? "" };
        field.RegisterCallback<FocusOutEvent>(_ => changed(field.value));
        field.style.marginBottom = 4;
        return field;
    }

    private static VisualElement AreaRow(string title, string value, Action<string> changed)
    {
        var field = new TextField(title) { value = value ?? "", multiline = true };
        field.style.minHeight = 72;
        field.style.marginBottom = 4;
        field.RegisterCallback<FocusOutEvent>(_ => changed(field.value));
        return field;
    }

    private static VisualElement DropdownRow(string title, List<string> choices, string current, Action<string> changed)
    {
        if (choices == null) choices = new List<string> { NoneLabel };
        string selected = current;
        if (!choices.Contains(selected))
        {
            if (!string.IsNullOrEmpty(current) && current != NoneLabel)
                choices.Insert(1, current);
            else
                selected = choices[0];
        }
        var field = new DropdownField(title, choices, selected);
        field.RegisterValueChangedCallback(evt => changed(evt.newValue));
        field.style.marginBottom = 4;
        return field;
    }

    private static VisualElement ToggleRow(string title, bool value, Action<bool> changed)
    {
        var field = new Toggle(title) { value = value };
        field.RegisterValueChangedCallback(evt => changed(evt.newValue));
        field.style.marginBottom = 4;
        return field;
    }

    private enum RowKind
    {
        Chapter,
        Segment,
        Content
    }

    private sealed class TreeRow
    {
        public RowKind Kind;
        public ChapterData Chapter;
        public SegmentData Segment;
        public DialogueContent Content;
        public string Label;
    }
}
