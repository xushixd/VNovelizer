# Dialogue Content 数据方案

本文记录 Chapter 里 `Dialogue` 和 `Video` 两种 Content 的数据结构与字段含义。

## 所属层级

该结构只用于对话过程，属于故事层级中的 `Content`：

```text
Chapter → Segment → Content → Result → Branch
```

视频使用独立的 `type: Video` Content，和 Dialogue 并列。条件、章节跳转、结局等其它类型以后再补。

## JSON 结构

故事文件是一章，不是一份 line 列表：

```text
Chapter
└── Segment
    └── Content（Dialogue / Video）
```

```json
{
  "id": "001",
  "title": "第一章",
  "entrySegmentId": "001-0001",
  "segments": [
    {
      "id": "001-0001",
      "title": "开场",
      "content": [
        {
          "id": "001-0001-00001",
          "type": "Dialogue",
          "speaker": { "characterId": "shenli", "emotionId": "thinking" },
          "text": "我不是……在医院吗？这是哪里……",
          "stageCharacters": [
            { "slot": "middle", "characterId": "shenli", "emotionId": "thinking" }
          ],
          "backgroundAssetId": "ssjm_dressing_room",
          "voiceAssetId": "",
          "bgmAssetId": "",
          "options": []
        }
      ],
      "nextSegmentId": ""
    }
  ]
}
```

一个 Dialogue Content 的字段如下。它属于 Segment.content，不是独立的 line。

普通对白：

```json
{
  "id": "901-0002-00001",
  "type": "Dialogue",
  "speaker": {
    "characterId": "shenli",
    "emotionId": "thinking"
  },
  "text": "我不是……在医院吗？这是哪里……",
  "stageCharacters": [
    {
      "slot": "middle",
      "characterId": "shenli",
      "emotionId": "thinking"
    }
  ],
  "backgroundAssetId": "ssjm_dressing_room",
  "voiceAssetId": "",
  "bgmAssetId": "",
  "options": []
}
```

旁白、画面上没有人：

```json
{
  "id": "901-0002-00003",
  "type": "Dialogue",
  "speaker": {
    "characterId": "",
    "emotionId": ""
  },
  "text": "门外传来脚步声。",
  "stageCharacters": [],
  "backgroundAssetId": "ssjm_dressing_room",
  "voiceAssetId": "",
  "bgmAssetId": "ssjm_bgm_gameplay",
  "options": []
}
```

## 字段含义

### `id`

当前 Dialogue Content 的稳定 ID，在 Chapter 中必须唯一。

### `type`

固定为 `Dialogue`，表示该 Content 是一条对白。

当前 VStoryFlow 代码里类型判别值是小写 `dialogue`。设计名称仍叫 Dialogue；落地时与现有判别值对齐即可，不要同时存在两套大小写。

### `speaker`

当前是谁在说话，用来显示姓名和对话框头像。

- `characterId`：角色编辑器中的稳定角色 ID。
- `emotionId`：该角色在角色编辑器中登记的情绪 ID，用来解析头像。

故事编辑器应先选择角色，再从该角色的情绪列表中选择情绪。JSON 保存稳定 ID，不使用显示名称作为关联键。

说话人和舞台立绘是两回事：

- `speaker`：对话框里显示谁、用哪张头像。
- `stageCharacters`：画面上站着谁。

所以可以出现这些情况：

- 沈梨在说话，画面上也只有沈梨。
- 沈梨在说话，画面上同时站着沈梨和另一个人。
- 门外有人说话，画面上只站着沈梨。
- 旁白：没有说话人，画面可有可无立绘。

旁白或没有说话人时，两个字段都留空：

```json
"speaker": {
  "characterId": "",
  "emotionId": ""
}
```

不需要为此单独做一个 `narrator` 系统角色。如果填了 `characterId`，`emotionId` 必须是该角色已登记的情绪；两者都空才表示没有说话人。

### `text`

当前对白的文本内容。

可以为空。空文本表示这一句没有正文，只用来换画面或换音乐。

### `stageCharacters`

当前这句对白显示时，画面中应当出现的完整角色立绘状态。每项包含：

- `slot`：角色在画面中的位置，只允许 `left`、`middle`、`right`。
- `characterId`：角色编辑器中的稳定角色 ID。
- `emotionId`：该角色当前使用的情绪 ID；运行时通过它解析对应立绘。

数组为空表示当前画面不显示角色立绘。

同一句里同一个 `slot` 不能出现两次，这是数据错误。同一角色出现在两个不同位置，当前允许。

每条 Dialogue 都保存完整状态，因此不使用 `Keep`、`Set`、`Clear` 等 action，也暂不包含角色淡入、淡出或其他过渡动画。画面上没有列出的位置，就是空的。

### `backgroundAssetId`

当前对白画面使用的背景资产 ID，对应资产管理器中的背景资源。

每条 Dialogue 都必须设置背景。背景为空属于数据错误，应在故事编辑器或数据校验时报告。

### `voiceAssetId`

当前对白使用的语音资产 ID，对应资产管理器中的语音资源。

这是可选项。空字符串表示当前对白没有语音。语音只属于当前 Dialogue，不继承上一句。进入下一句时，上一句语音应停止。

### `bgmAssetId`

当前对白期望使用的 BGM 资产 ID，对应资产管理器中的 BGM 资源。

这是可选项。运行时根据上一句与当前句的 BGM ID 判断行为：

| 上一句 | 当前句 | 运行行为 |
|---|---|---|
| `bgm_a` | `bgm_a` | 继续播放，不中断、不重新开始 |
| `bgm_a` | `bgm_b` | 停止上一首并切换到 `bgm_b` |
| `bgm_a` | 空 | 停止 BGM |
| 空 | `bgm_b` | 开始播放 `bgm_b` |
| 空 | 空 | 保持无 BGM |

因此，`bgmAssetId` 表示当前 Dialogue 的完整 BGM 状态，而不是一条播放命令。

### `options`

当前对白显示后弹出的选项。空数组表示没有选项，播完走进下一条 Content / `nextSegmentId`。

每个选项：

- `id`：这条选项自己的稳定 ID，和 Chapter / Segment / Content 同一套编号。
- `text`：按钮上显示的文字。
- `result`：点选后要去的 **Segment ID**。

统一编号：

```text
Chapter   001
Segment   001-0001
Content   001-0001-00001
Option    001-0001-00001-01
```

选项 `id` = 所属 Content ID + 两位序号（`01`、`02`…）。
`result` = 目标段落的 Segment ID，例如 `001-0003`。

```json
{
  "id": "001-0002-00002-01",
  "text": "好，我信你",
  "result": "001-0003"
}
```

运行时用现成 `ChoicePanel` 弹出。点选后直接进入 `result` 指出的 Segment。目标不存在时报错，不会改走 `nextSegmentId`。

## Video Content

和 Dialogue 一样，是 Segment 里的一条 Content，不是 Command。

播完直接进下一条：

```json
{
  "id": "001-0001-00000",
  "type": "Video",
  "videoAssetId": "op",
  "playback": "once",
  "skippable": true,
  "options": []
}
```

循环播放，并出选项（不选不能进下一段）：

```json
{
  "id": "001-0002-00003",
  "type": "Video",
  "videoAssetId": "choice_intro",
  "playback": "loop",
  "skippable": true,
  "options": [
    { "id": "001-0002-00003-01", "text": "推开门", "result": "001-0003" },
    { "id": "001-0002-00003-02", "text": "留在这里", "result": "001-0004" }
  ]
}
```

| 字段 | 默认 | 含义 |
|---|---|---|
| `videoAssetId` | 无，必填 | 视频资产 ID，对应 `StreamingAssets/VNovelizerRes/Videos/`，扩展名可省 |
| `playback` | `once` | 只分两种：`once` 播完一遍；`loop` 从头完整循环 |
| `skippable` | `true` | 为 true 时，视频右上角显示可定制的跳过按钮预制件 |
| `options` | `[]` | 与 Dialogue 相同；非空则必须选择才能离开这条 Video |

| `playback` | 行为 |
|---|---|
| `once` | 从开头播到结尾一次。没有选项 → 进下一条 Content / `nextSegmentId`。有选项 → 停在最后一帧等选择。 |
| `loop` | 整段从头循环，不是「先播完再开始循环」。有选项时视频继续循环，选项叠在上面。 |

跳过按钮：

- 独立 UI 预制件，默认放在视频右上角，可换图、可改位置。
- 只有 `skippable: true` 时显示。
- `once` 且无选项：跳过 = 结束这条 Video，进入下一条。
- 有选项：跳过只结束当前这一遍画面，**不能代替选择**，选项仍在。

仍使用现成 `VideoObj` 播片。

## 空值规则

可选项为空表示「当前这句没有这项」；必填项为空表示数据错误。

| 字段 | 空值含义 |
|---|---|
| `speaker.characterId` 和 `speaker.emotionId` 都空 | 这句没有说话人，不显示姓名和头像 |
| `text` | 这句没有正文 |
| `stageCharacters: []` | 这句画面上没有立绘 |
| `voiceAssetId` | 这句没有语音 |
| `bgmAssetId` | 这句没有 BGM，并按上表停止上一首 |
| `options: []` | 这句没有选项 |
| `backgroundAssetId` | 数据错误，必须报错 |

## 已确认的规则

1. 该结构只属于 `Content(type = Dialogue)`。
2. 每条 Dialogue 描述当前对白的完整画面和音频状态，不依赖上一条 Content 补全数据。
3. 角色与情绪必须对应角色编辑器中的数据。
4. 背景、语音和 BGM 必须引用资产管理器中的相应资源。
5. 背景是必填项；未设置时应报错。
6. 语音、BGM 和选项是可选项；不填写表示没有。
7. 舞台角色数组可以为空，表示当前画面没有角色立绘。
8. 不使用背景或舞台角色 action。
9. 暂不实现角色淡入、淡出及其他过渡动画。
10. 相邻 Dialogue 使用相同 BGM 时继续播放，不中断也不重新开始；BGM ID 改变时切换。
11. 说话人可以为空，表示旁白或没有说话人。
12. 对白文本可以为空。
13. 舞台位置只允许 `left`、`middle`、`right`；同一句不能重复同一个位置。
14. 本阶段不把音效、头像覆盖、演出命令字符串放进 Dialogue。
15. Dialogue / Video 选项的 `result` 直接存目标 Segment ID；选项 `id` 为 `内容ID-两位序号`。
16. Video 的 `playback` 只有 `once` 和 `loop`。`skippable` 默认 true，右上角用独立跳过按钮预制件。
