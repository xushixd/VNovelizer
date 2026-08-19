using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;
using System.IO;
using System.Linq;
using ExcelDataReader;
using System.Collections.Generic;
using System.Text;

public class ScriptManagerWindow : EditorWindow
{
    private static readonly string[] Headers =
    {
        "ID", "Speaker", "HeadProfile", "CharLeft", "CharMid", "CharRight",
        "Text", "Background", "BGM", "Voice", "Command", "Note"
    };
    private const string ExcelTemplateGuid = "3a90d1d73ba40aa43b87e5694db9eb61";

    private ListView scriptList;
    private MultiColumnListView previewTable;
    private Label statusLabel;

    private List<ScriptFileEntry> scriptFiles = new List<ScriptFileEntry>();
    private string excelFolderPath;
    private string runtimeScriptFolderPath;
    private ScriptFileEntry selectedFile;

    private enum ScriptFormat
    {
        Excel,
        Csv,
        Json
    }

    private sealed class ScriptFileEntry
    {
        public FileInfo File;
        public ScriptFormat Format;
    }

    [MenuItem("VNovelizer/剧本管理器 (Script Manager)", false, 22)]
    public static void ShowWindow()
    {
        var wnd = GetWindow<ScriptManagerWindow>();
        wnd.titleContent = new GUIContent("剧本管理器");
        wnd.minSize = new Vector2(1000, 600);
    }

    public void CreateGUI()
    {
        var config = VNProjectConfig.Instance;
        if (config == null || config.ExcelSourceFolder == null || string.IsNullOrEmpty(config.VNScriptResPath))
        {
            var error = new Label("请先在 VNProjectConfig 中配置 Excel 源文件夹和 VNScriptResPath！")
            {
                style = { color = Color.red, fontSize = 16, unityTextAlign = TextAnchor.MiddleCenter, paddingTop = 50 }
            };
            rootVisualElement.Add(error);
            return;
        }

        excelFolderPath = Path.GetFullPath(AssetDatabase.GetAssetPath(config.ExcelSourceFolder));
        runtimeScriptFolderPath = Path.Combine(Application.dataPath, "Resources", config.VNScriptResPath);
        Directory.CreateDirectory(runtimeScriptFolderPath);

        var root = rootVisualElement;
        root.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);

        var splitView = new TwoPaneSplitView(0, 420, TwoPaneSplitViewOrientation.Horizontal);
        root.Add(splitView);

        var leftPane = new VisualElement();
        var toolbar = new VisualElement();
        toolbar.style.flexDirection = FlexDirection.Row;
        toolbar.style.paddingTop = 5;
        toolbar.style.paddingBottom = 5;
        toolbar.style.paddingLeft = 5;
        toolbar.style.paddingRight = 5;

        var createBtn = new Button(CreateNewScript) { text = "新建", style = { flexGrow = 1 } };
        var importBtn = new Button(ImportScript) { text = "导入", style = { width = 60 } };
        var convertBtn = new Button(ConvertScripts) { text = "转换", style = { width = 60, backgroundColor = new Color(0.2f, 0.5f, 0.2f) } };
        var sortBtn = new Button(OptimizeJson) { text = "JSON排序", style = { width = 75 } };
        var refreshBtn = new Button(RefreshList) { text = "刷新", style = { width = 50 } };

        toolbar.Add(createBtn);
        toolbar.Add(importBtn);
        toolbar.Add(convertBtn);
        toolbar.Add(sortBtn);
        toolbar.Add(refreshBtn);
        leftPane.Add(toolbar);

        scriptList = new ListView();
        scriptList.fixedItemHeight = 30;
        scriptList.makeItem = () =>
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, paddingLeft = 5, height = 30 } };
            var icon = new Image { style = { width = 16, height = 16, marginRight = 5 } };
            icon.image = EditorGUIUtility.IconContent("TextAsset Icon").image;
            var typeLabel = new Label { name = "Type", style = { width = 42, unityTextAlign = TextAnchor.MiddleCenter, color = Color.cyan } };
            var nameLabel = new Label { name = "Name", style = { flexGrow = 1, unityTextAlign = TextAnchor.MiddleLeft } };
            var btnContainer = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var renameBtn = new Button { text = "改名", name = "Rename", style = { width = 36, height = 20 } };
            var playBtn = new Button { text = "试玩", name = "Play", style = { width = 36, height = 20, backgroundColor = new Color(0.2f, 0.2f, 0.5f) } };
            var delBtn = new Button { text = "删除", name = "Delete", style = { width = 36, height = 20, backgroundColor = new Color(0.6f, 0.2f, 0.2f) } };
            btnContainer.Add(renameBtn);
            btnContainer.Add(playBtn);
            btnContainer.Add(delBtn);
            row.Add(icon);
            row.Add(typeLabel);
            row.Add(nameLabel);
            row.Add(btnContainer);
            return row;
        };

        scriptList.bindItem = (element, index) =>
        {
            if (index >= scriptFiles.Count) return;
            var entry = scriptFiles[index];
            element.Q<Label>("Type").text = entry.Format.ToString().ToUpperInvariant();
            element.Q<Label>("Name").text = entry.File.Name;
            element.Q<Button>("Rename").clickable = new Clickable(() => RenameScript(entry));
            element.Q<Button>("Play").clickable = new Clickable(() => QuickPlay(entry));
            element.Q<Button>("Delete").clickable = new Clickable(() => DeleteScript(entry));

            element.UnregisterCallback<MouseDownEvent>(OnItemMouseDown);
            element.RegisterCallback<MouseDownEvent>(OnItemMouseDown);
            void OnItemMouseDown(MouseDownEvent evt)
            {
                if (evt.clickCount == 2) OpenScriptFile(entry);
            }
        };

        scriptList.itemsSource = scriptFiles;
        scriptList.style.flexGrow = 1;
        scriptList.selectionType = SelectionType.Single;
        scriptList.selectionChanged += OnSelectionChanged;
        leftPane.Add(scriptList);
        splitView.Add(leftPane);

        var rightPane = new VisualElement();
        rightPane.style.paddingLeft = 10;
        rightPane.style.paddingTop = 10;
        rightPane.style.paddingRight = 10;
        rightPane.style.paddingBottom = 10;
        rightPane.Add(new Label("预览区域 (只读)") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 5 } });
        statusLabel = new Label("就绪") { style = { height = 20, color = Color.green } };
        rightPane.Add(statusLabel);
        previewTable = new MultiColumnListView();
        previewTable.style.flexGrow = 1;
        previewTable.showAlternatingRowBackgrounds = AlternatingRowBackground.All;
        rightPane.Add(previewTable);
        splitView.Add(rightPane);

        RefreshList();
    }

    private void RefreshList()
    {
        scriptFiles = new List<ScriptFileEntry>();

        if (Directory.Exists(excelFolderPath))
        {
            scriptFiles.AddRange(new DirectoryInfo(excelFolderPath).GetFiles("*.*", SearchOption.TopDirectoryOnly)
                .Where(f => (f.Extension.Equals(".xlsx", System.StringComparison.OrdinalIgnoreCase) || f.Extension.Equals(".xls", System.StringComparison.OrdinalIgnoreCase)) && !f.Name.StartsWith("~$"))
                .Select(f => new ScriptFileEntry { File = f, Format = ScriptFormat.Excel }));
        }

        if (Directory.Exists(runtimeScriptFolderPath))
        {
            scriptFiles.AddRange(new DirectoryInfo(runtimeScriptFolderPath).GetFiles("*.*", SearchOption.AllDirectories)
                .Where(f => f.Extension.Equals(".json", System.StringComparison.OrdinalIgnoreCase) || f.Extension.Equals(".csv", System.StringComparison.OrdinalIgnoreCase))
                .Select(f => new ScriptFileEntry
                {
                    File = f,
                    Format = f.Extension.Equals(".json", System.StringComparison.OrdinalIgnoreCase) ? ScriptFormat.Json : ScriptFormat.Csv
                }));
        }

        scriptFiles = scriptFiles.OrderByDescending(e => e.File.LastWriteTime).ToList();
        selectedFile = null;
        scriptList.itemsSource = scriptFiles;
        scriptList.Rebuild();
        statusLabel.text = $"刷新完成，共 {scriptFiles.Count} 个剧本";
    }

    private void OnSelectionChanged(IEnumerable<object> selection)
    {
        selectedFile = selection.OfType<ScriptFileEntry>().FirstOrDefault();
        if (selectedFile != null) LoadPreview(selectedFile);
    }

    private void LoadPreview(ScriptFileEntry entry)
    {
        previewTable.columns.Clear();
        previewTable.itemsSource = null;

        try
        {
            if (entry.Format == ScriptFormat.Excel) LoadExcelPreview(entry.File);
            else LoadTextPreview(entry.File);
            statusLabel.text = $"已加载预览：{entry.File.Name}";
        }
        catch (System.Exception e)
        {
            Debug.LogError($"预览失败: {e.Message}");
            statusLabel.text = $"预览失败：{e.Message}";
        }
    }

    private void LoadExcelPreview(FileInfo file)
    {
        using (var stream = File.Open(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = ExcelReaderFactory.CreateReader(stream))
        {
            var headers = new List<string>();
            var tableData = new List<List<string>>();
            bool isFirstRow = true;
            while (reader.Read())
            {
                var row = new List<string>();
                for (int i = 0; i < reader.FieldCount; i++)
                    row.Add(reader.GetValue(i)?.ToString() ?? "");

                if (isFirstRow) { headers = row; isFirstRow = false; }
                else tableData.Add(row);
            }
            BuildPreview(headers, tableData);
        }
    }

    private void LoadTextPreview(FileInfo file)
    {
        ScriptParser.ScriptData data = ScriptParser.ParseText(File.ReadAllText(file.FullName));
        if (data == null) throw new InvalidDataException("不是有效的剧本 JSON/CSV。");

        if (data.IsChapter)
        {
            var tableData = new List<List<string>>();
            foreach (SegmentData segment in data.Chapter.segments)
            {
                if (segment == null || segment.content == null) continue;
                foreach (DialogueContent content in segment.content)
                {
                    if (content == null) continue;
                    tableData.Add(ToPreviewRow(content.ToStoryLine()));
                }
            }
            BuildPreview(Headers.ToList(), tableData);
            return;
        }

        var lineRows = data.Lines.Select(ToPreviewRow).ToList();
        BuildPreview(Headers.ToList(), lineRows);
    }

    private static List<string> ToPreviewRow(StoryLine line)
    {
        return new List<string>
        {
            line.ID, line.Speaker, line.HeadProfile, line.CharLeft, line.CharMid, line.CharRight,
            line.Text, line.Background, line.BGM, line.Voice, line.Command, line.Note
        };
    }

    private void BuildPreview(List<string> headers, List<List<string>> tableData)
    {
        for (int c = 0; c < headers.Count; c++)
        {
            string headerName = string.IsNullOrEmpty(headers[c]) ? $"Col {c}" : headers[c];
            int colIndex = c;
            var col = new Column { name = headerName, title = headerName, width = 100 };
            col.makeCell = () => new Label();
            col.bindCell = (e, i) =>
            {
                if (i >= tableData.Count || colIndex >= tableData[i].Count) return;
                ((Label)e).text = tableData[i][colIndex] ?? "";
            };
            previewTable.columns.Add(col);
        }
        previewTable.itemsSource = tableData;
        previewTable.Rebuild();
    }

    private void CreateNewScript()
    {
        int option = EditorUtility.DisplayDialogComplex("新建剧本", "请选择要创建的剧本格式。", "JSON 剧本", "Excel 剧本", "取消");
        if (option == 0) CreateJsonScript();
        else if (option == 1) CreateExcelScript();
    }

    private void CreateJsonScript()
    {
        string path = EditorUtility.SaveFilePanel("新建 JSON 剧本", runtimeScriptFolderPath, "NewChapter", "json");
        if (string.IsNullOrEmpty(path)) return;
        if (!IsInsideFolder(path, runtimeScriptFolderPath))
        {
            EditorUtility.DisplayDialog("错误", "JSON 剧本必须创建在运行时剧本目录中。", "确定");
            return;
        }

        if (HasRuntimeFormatConflict(path, ".json", out string conflict))
        {
            EditorUtility.DisplayDialog("创建失败", $"运行时剧本目录中已存在同名剧本：\n{conflict}\nJSON 与 CSV 不能使用相同文件名。", "确定");
            return;
        }

        File.WriteAllText(path, ScriptParser.SerializeChapter(ChapterData.CreateSample()), new UTF8Encoding(false));
        AssetDatabase.Refresh();
        RefreshList();
        statusLabel.text = $"已创建：{Path.GetFileName(path)}";
        OpenScriptFile(new ScriptFileEntry { File = new FileInfo(path), Format = ScriptFormat.Json });
    }

    private void CreateExcelScript()
    {
        string packageTemplate = AssetDatabase.GUIDToAssetPath(ExcelTemplateGuid);
        string templatePath = !string.IsNullOrEmpty(packageTemplate)
            ? packageTemplate
            : "Assets/Resources/VNovelizerRes/ExcelVNScripts/Templates/ScriptTemplate.xlsx";
        if (!File.Exists(templatePath))
        {
            EditorUtility.DisplayDialog("错误", $"找不到模板文件：{templatePath}\n请创建模板。", "确定");
            return;
        }

        string path = EditorUtility.SaveFilePanel("新建 Excel 剧本", excelFolderPath, "NewChapter", "xlsx");
        if (string.IsNullOrEmpty(path)) return;
        string generatedCsv = Path.Combine(runtimeScriptFolderPath, Path.GetFileNameWithoutExtension(path) + ".csv");
        if (HasRuntimeFormatConflict(generatedCsv, ".csv", out string conflict))
        {
            EditorUtility.DisplayDialog("创建失败", $"运行时剧本目录中已存在同名剧本：\n{conflict}\nExcel 转换生成的 CSV 不能与 JSON 使用相同文件名。", "确定");
            return;
        }
        try
        {
            File.Copy(templatePath, path);
            RefreshList();
            PrepareLocalization(Path.GetFileNameWithoutExtension(path));
            statusLabel.text = $"已创建：{Path.GetFileName(path)}";
            OpenScriptFile(new ScriptFileEntry { File = new FileInfo(path), Format = ScriptFormat.Excel });
        }
        catch (System.Exception e)
        {
            Debug.LogError($"创建失败: {e.Message}");
        }
    }

    private void ImportScript()
    {
        string source = EditorUtility.OpenFilePanelWithFilters("导入剧本", "", new[] { "支持的剧本", "json,csv,xlsx,xls", "JSON", "json", "CSV", "csv", "Excel", "xlsx,xls" });
        if (string.IsNullOrEmpty(source)) return;

        string extension = Path.GetExtension(source).ToLowerInvariant();
        if (extension != ".json" && extension != ".csv" && extension != ".xlsx" && extension != ".xls")
        {
            EditorUtility.DisplayDialog("导入失败", $"不支持的文件格式：{extension}", "确定");
            return;
        }

        bool isExcel = extension == ".xlsx" || extension == ".xls";
        string targetFolder = isExcel ? excelFolderPath : runtimeScriptFolderPath;
        string target = Path.Combine(targetFolder, Path.GetFileName(source));

        if (isExcel)
        {
            string generatedCsv = Path.Combine(runtimeScriptFolderPath, Path.GetFileNameWithoutExtension(source) + ".csv");
            if (HasRuntimeFormatConflict(generatedCsv, ".csv", out string excelConflict))
            {
                EditorUtility.DisplayDialog("导入失败", $"运行时剧本目录中已存在同名剧本：\n{excelConflict}\nExcel 转换生成的 CSV 不能与 JSON 使用相同文件名。", "确定");
                return;
            }
        }

        if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(target), System.StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog("导入提示", "该文件已经位于目标剧本目录中，无需重复导入。", "确定");
            return;
        }

        if ((extension == ".json" || extension == ".csv") && HasRuntimeFormatConflict(target, extension, out string conflict))
        {
            EditorUtility.DisplayDialog("导入失败", $"运行时剧本目录中已存在同名剧本：\n{conflict}\nJSON 与 CSV 不能使用相同文件名。", "确定");
            return;
        }

        if (extension == ".json")
        {
            string json = File.ReadAllText(source);
            if (ScriptParser.TryParseChapter(json) == null && ScriptParser.TryParseJsonLines(json) == null)
            {
                EditorUtility.DisplayDialog("导入失败", "JSON 不是 Chapter（segments/content）或旧版 lines 剧本。", "确定");
                return;
            }
        }

        if (!ConfirmOverwrite(target)) return;

        Directory.CreateDirectory(targetFolder);
        File.Copy(source, target, true);
        if (isExcel)
        {
            ExcelToCsvConverter.ConvertFile(target, runtimeScriptFolderPath);
            AutoExcelConverter.RefreshAllFileTimestamps();
        }
        AssetDatabase.Refresh();
        RefreshList();
        statusLabel.text = $"已导入：{Path.GetFileName(source)}";
    }

    private void RenameScript(ScriptFileEntry entry)
    {
        RenamePopup.Show(entry.File.Name, newName =>
        {
            if (string.IsNullOrWhiteSpace(newName)) return;
            string extension = entry.File.Extension;
            if (!newName.EndsWith(extension, System.StringComparison.OrdinalIgnoreCase)) newName += extension;
            string newRuntimeName = Path.GetFileNameWithoutExtension(newName);
            string newPath = Path.Combine(entry.File.DirectoryName, newName);
            if (entry.Format == ScriptFormat.Excel)
            {
                string generatedCsv = Path.Combine(runtimeScriptFolderPath, newRuntimeName + ".csv");
                if (HasRuntimeFormatConflict(generatedCsv, ".csv", out string excelConflict))
                {
                    EditorUtility.DisplayDialog("错误", $"运行时剧本目录中已存在同名剧本：\n{excelConflict}\nExcel 转换生成的 CSV 不能与 JSON 使用相同文件名。", "确定");
                    return;
                }
            }
            if ((entry.Format == ScriptFormat.Json || entry.Format == ScriptFormat.Csv) &&
                HasRuntimeFormatConflict(newPath, extension, out string conflict))
            {
                EditorUtility.DisplayDialog("错误", $"运行时剧本目录中已存在同名剧本：\n{conflict}\nJSON 与 CSV 不能使用相同文件名。", "确定");
                return;
            }
            if (File.Exists(newPath))
            {
                EditorUtility.DisplayDialog("错误", "文件名已存在！", "确定");
                return;
            }

            try
            {
                string oldPath = entry.File.FullName;
                string oldName = Path.GetFileNameWithoutExtension(entry.File.Name);
                entry.File.MoveTo(newPath);
                MoveMeta(oldPath, newPath);

                if (entry.Format == ScriptFormat.Excel)
                    RenameGeneratedCsv(oldName, Path.GetFileNameWithoutExtension(newName));

                AssetDatabase.Refresh();
                RefreshList();
            }
            catch (IOException)
            {
                EditorUtility.DisplayDialog("错误", "文件被占用，无法重命名。", "确定");
            }
        });
    }

    private void DeleteScript(ScriptFileEntry entry)
    {
        string extra = entry.Format == ScriptFormat.Excel ? "\n同时删除同名生成 CSV。" : "";
        if (!EditorUtility.DisplayDialog("删除剧本", $"确定要删除 {entry.File.Name} 吗？{extra}\n此操作无法撤销！", "删除", "取消")) return;

        string scriptName = Path.GetFileNameWithoutExtension(entry.File.Name);
        DeleteFileAndMeta(entry.File.FullName);
        if (entry.Format == ScriptFormat.Excel)
            DeleteFileAndMeta(Path.Combine(runtimeScriptFolderPath, scriptName + ".csv"));

        if (!VNLocalizationSyncUtility.DeleteScriptCollection(scriptName, out var error) && !string.IsNullOrEmpty(error) && !error.Contains("未找到 Collection"))
            Debug.LogWarning($"[ScriptManager] 删除本地化 Collection 失败: {error}");

        AssetDatabase.Refresh();
        RefreshList();
    }

    private void ConvertScripts()
    {
        statusLabel.text = "正在转换 Excel...";
        ExcelToCsvConverter.ConvertAllExcelFiles();
        AutoExcelConverter.RefreshAllFileTimestamps();
        statusLabel.text = "Excel 转换完成！";
        RefreshList();
    }

    private void OptimizeJson()
    {
        if (selectedFile != null && selectedFile.Format == ScriptFormat.Json)
        {
            if (JsonScriptOptimizer.OptimizeFile(selectedFile.File.FullName, out bool changed, out string error))
            {
                AssetDatabase.Refresh();
                LoadPreview(selectedFile);
                statusLabel.text = changed ? $"已按 ID 排序：{selectedFile.File.Name}" : $"无需排序：{selectedFile.File.Name}";
            }
            else
            {
                EditorUtility.DisplayDialog("JSON 排序失败", error, "确定");
            }
            return;
        }

        if (EditorUtility.DisplayDialog("JSON 排序", "当前未选中 JSON 剧本，是否优化全部 JSON 剧本？", "全部优化", "取消"))
            JsonScriptOptimizer.OptimizeAll();
    }

    private void QuickPlay(ScriptFileEntry entry)
    {
        if (entry.Format == ScriptFormat.Json || entry.Format == ScriptFormat.Csv)
        {
            ScriptParser.ScriptData data = ScriptParser.ParseText(File.ReadAllText(entry.File.FullName));
            if (data == null || data.Lines.Count == 0)
            {
                EditorUtility.DisplayDialog("无法试玩", "该剧本无法解析或没有有效剧情行。", "确定");
                return;
            }
        }

        string scriptName = Path.GetFileNameWithoutExtension(entry.File.Name);
        PlayerPrefs.SetString("Debug_LastScriptName", scriptName);
        PlayerPrefs.SetString("Debug_LastLineID", "");
        PlayerPrefs.SetInt("Debug_Mode", 1);
        PlayerPrefs.Save();

        if (!EditorApplication.isPlaying)
        {
            string scenePath = "Assets/Scenes/VNDebugScene.unity";
            if (File.Exists(scenePath))
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
                EditorApplication.isPlaying = true;
            }
            else
            {
                Debug.LogError($"找不到 DebugScene，路径错误：{scenePath}");
            }
        }
    }

    private void PrepareLocalization(string scriptName)
    {
        int option = EditorUtility.DisplayDialogComplex("剧本创建方式", "请选择本剧本是否使用剧情本地化。", "多语言", "普通", "取消");
        if (option != 0) return;
        VNLocalizationSyncUtility.EnsureScriptCollection(scriptName, out _, out _);
        if (!VNLocalizationSyncUtility.TrySyncKeysFromCsv(scriptName, true, out var error))
            EditorUtility.DisplayDialog("提示", error ?? "同步 key 未完成（可能是 CSV 未生成）。", "确定");
    }

    private bool HasRuntimeFormatConflict(string targetPath, string targetExtension, out string conflictPath)
    {
        conflictPath = null;
        string fileName = Path.GetFileNameWithoutExtension(targetPath);
        string otherExtension = targetExtension.Equals(".json", System.StringComparison.OrdinalIgnoreCase) ? ".csv" : ".json";
        string candidate = Path.Combine(runtimeScriptFolderPath, fileName + otherExtension);
        if (!File.Exists(candidate)) return false;

        conflictPath = candidate;
        return true;
    }

    private static void OpenScriptFile(ScriptFileEntry entry)
    {
        if (entry == null || entry.File == null || !entry.File.Exists)
        {
            EditorUtility.DisplayDialog("无法打开", "找不到剧本文件。", "确定");
            return;
        }

        string path = entry.File.FullName;
        if (entry.Format == ScriptFormat.Json || entry.Format == ScriptFormat.Csv)
        {
            InternalEditorUtility.OpenFileAtLineExternal(path, 1);
            return;
        }

        Application.OpenURL(path);
    }

    private static bool ConfirmOverwrite(string path)
    {
        return !File.Exists(path) || EditorUtility.DisplayDialog("覆盖文件", $"目标文件已存在：\n{path}\n是否覆盖？", "覆盖", "取消");
    }

    private static bool IsInsideFolder(string file, string folder)
    {
        string fullFile = Path.GetFullPath(file);
        string fullFolder = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(fullFile, fullFolder, System.StringComparison.OrdinalIgnoreCase) ||
               fullFile.StartsWith(fullFolder + Path.DirectorySeparatorChar, System.StringComparison.OrdinalIgnoreCase) ||
               fullFile.StartsWith(fullFolder + Path.AltDirectorySeparatorChar, System.StringComparison.OrdinalIgnoreCase);
    }

    private static void DeleteFileAndMeta(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(path + ".meta")) File.Delete(path + ".meta");
    }

    private static void MoveMeta(string oldPath, string newPath)
    {
        string oldMeta = oldPath + ".meta";
        if (File.Exists(oldMeta)) File.Move(oldMeta, newPath + ".meta");
    }

    private void RenameGeneratedCsv(string oldName, string newName)
    {
        string oldPath = Path.Combine(runtimeScriptFolderPath, oldName + ".csv");
        string newPath = Path.Combine(runtimeScriptFolderPath, newName + ".csv");
        if (!File.Exists(oldPath) || File.Exists(newPath)) return;
        File.Move(oldPath, newPath);
        MoveMeta(oldPath, newPath);
    }
}

public class RenamePopup : EditorWindow
{
    private string fileName;
    private System.Action<string> onConfirm;

    public static void Show(string currentName, System.Action<string> callback)
    {
        var win = GetWindow<RenamePopup>(true, "重命名", true);
        win.fileName = currentName;
        win.onConfirm = callback;
        win.minSize = new Vector2(300, 80);
        win.maxSize = new Vector2(300, 80);
        win.ShowUtility();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        fileName = EditorGUILayout.TextField("新文件名:", fileName);
        EditorGUILayout.Space(10);
        if (GUILayout.Button("确定"))
        {
            onConfirm?.Invoke(fileName);
            Close();
        }
    }
}
