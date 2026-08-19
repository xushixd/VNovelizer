using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 剧本行数据结构
/// </summary>
[System.Serializable]
public class StoryLine
{
    public string ID;
    public string Speaker;
    public string HeadProfile;
    public string CharLeft;
    public string CharMid;
    public string CharRight;
    public string Text;
    public string Background;
    public string BGM;
    public string Voice;
    public string Command;
    public string Note;

    /// <summary>
    /// 新版 Dialogue：本行是完整画面/音频状态，空值表示没有，不沿用上一行。
    /// </summary>
    public bool CompleteState;
}