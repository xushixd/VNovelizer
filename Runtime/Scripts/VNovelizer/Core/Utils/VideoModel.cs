using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[RequireComponent(typeof(RawImage))]
[RequireComponent(typeof(VideoPlayer))]
public class VideoModel : MonoBehaviour
{
    private VideoPlayer videoPlayer;
    private RawImage rawImage;
    private Action onComplete;
    private Action onFirstEnd;
    private bool loopAfterEnd;
    private bool holdLastFrame;
    private bool firstPlayEnded;

    // Unity VideoPlayer 支持的视频格式
    private static readonly string[] SupportedExtensions = { ".mp4", ".mov", ".webm", ".avi", ".asf", ".wmv" };

    private void Awake()
    {
        videoPlayer = GetComponent<VideoPlayer>();
        rawImage = GetComponent<RawImage>();
        if (rawImage != null)
            rawImage.raycastTarget = false;

        videoPlayer.loopPointReached += OnLoopPointReached;
        videoPlayer.errorReceived += (vp, msg) => {
            Debug.LogError($"[Video] Error: {msg}");
            NotifyEndedThenClose();
        };
    }

    public void Play(string videoName, Action callback)
    {
        Play(videoName, callback, false, false);
    }

    public void Play(string videoName, Action onEnded, bool loopAfterFirstPlay, bool keepLastFrame)
    {
        onFirstEnd = onEnded;
        onComplete = loopAfterFirstPlay || keepLastFrame ? null : onEnded;
        loopAfterEnd = loopAfterFirstPlay;
        holdLastFrame = keepLastFrame;
        firstPlayEnded = false;

        string subPath = VNProjectConfig.Instance != null && !string.IsNullOrEmpty(VNProjectConfig.Instance.VideoResPath)
            ? VNProjectConfig.Instance.VideoResPath
            : "VNovelizerRes/Videos";
        string resourceName = CombineResourcePath(subPath, StripExtension(videoName));
        VideoClip clip = Resources.Load<VideoClip>(resourceName);
        if (clip != null)
        {
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = clip;
            Debug.Log($"[Video] 准备播放视频: Resources/{resourceName}");
            StartCoroutine(PlayRoutine());
            return;
        }

        string baseDir = Path.Combine(Application.streamingAssetsPath, subPath);
        string fullPath = FindVideoFile(baseDir, videoName);
        if (string.IsNullOrEmpty(fullPath))
        {
            Debug.LogError($"[Video] 无法找到视频文件: {videoName} (Resources/{resourceName} 或 {baseDir})");
            Close();
            return;
        }

        fullPath = fullPath.Replace("\\", "/");
        videoPlayer.source = VideoSource.Url;
        videoPlayer.clip = null;
        videoPlayer.url = GetVideoURL(fullPath);
        Debug.Log($"[Video] 准备播放视频: {videoPlayer.url}");
        StartCoroutine(PlayRoutine());
    }

    private static string CombineResourcePath(string folder, string videoName)
    {
        folder = (folder ?? "").Replace("\\", "/").Trim('/');
        videoName = (videoName ?? "").Replace("\\", "/").Trim('/');
        if (string.IsNullOrEmpty(folder)) return videoName;
        if (string.IsNullOrEmpty(videoName)) return folder;
        return folder + "/" + videoName;
    }

    private static string StripExtension(string videoName)
    {
        if (string.IsNullOrEmpty(videoName)) return videoName;
        foreach (string ext in SupportedExtensions)
        {
            if (videoName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return videoName.Substring(0, videoName.Length - ext.Length);
        }
        return videoName;
    }

    /// <summary>
    /// 查找视频文件（支持多种格式）
    /// </summary>
    private string FindVideoFile(string baseDir, string videoName)
    {
        if (!Directory.Exists(baseDir))
        {
            Debug.LogWarning($"[Video] 视频目录不存在: {baseDir}");
            return null;
        }

        // 如果文件名已经包含扩展名，直接检查
        if (Path.HasExtension(videoName))
        {
            string fullPath = Path.Combine(baseDir, videoName);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        // 如果没有扩展名，尝试所有支持的格式
        foreach (string ext in SupportedExtensions)
        {
            string fullPath = Path.Combine(baseDir, videoName + ext);
            if (File.Exists(fullPath))
            {
                Debug.Log($"[Video] 找到视频文件: {videoName + ext}");
                return fullPath;
            }
        }

        return null;
    }

    /// <summary>
    /// 根据平台获取正确的视频URL
    /// </summary>
    private string GetVideoURL(string filePath)
    {
        // 根据平台添加协议前缀
        #if UNITY_ANDROID && !UNITY_EDITOR
            // Android平台需要使用jar协议
            if (!filePath.StartsWith("jar:file://"))
            {
                filePath = "jar:file://" + filePath;
            }
        #elif UNITY_IOS && !UNITY_EDITOR
            // iOS平台需要使用file协议
            if (!filePath.StartsWith("file://"))
            {
                filePath = "file://" + filePath;
            }
        #elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            // Windows平台需要使用file协议（三个斜杠）
            if (!filePath.StartsWith("file://"))
            {
                filePath = "file:///" + filePath;
            }
        #elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            // Mac平台需要使用file协议
            if (!filePath.StartsWith("file://"))
            {
                filePath = "file://" + filePath;
            }
        #else
            // 其他平台默认使用file协议
            if (!filePath.StartsWith("file://"))
            {
                filePath = "file://" + filePath;
            }
        #endif
        
        return filePath;
    }

    private IEnumerator PlayRoutine()
    {
        videoPlayer.Prepare();

        // 等待准备好
        while (!videoPlayer.isPrepared) yield return null;

        // 绑定材质并播放
        rawImage.texture = videoPlayer.texture;
        videoPlayer.isLooping = loopAfterEnd;
        videoPlayer.Play();
    }

    private void OnLoopPointReached(VideoPlayer vp)
    {
        if (loopAfterEnd)
        {
            if (!firstPlayEnded)
            {
                firstPlayEnded = true;
                onFirstEnd?.Invoke();
            }
            return;
        }

        if (firstPlayEnded)
            return;

        firstPlayEnded = true;

        if (holdLastFrame)
        {
            videoPlayer.Pause();
            onFirstEnd?.Invoke();
            return;
        }

        NotifyEndedThenClose();
    }

    private void NotifyEndedThenClose()
    {
        Action ended = onFirstEnd ?? onComplete;
        onFirstEnd = null;
        onComplete = null;
        if (this != null && gameObject != null)
            Destroy(gameObject);
        ended?.Invoke();
    }

    public void Close()
    {
        onFirstEnd = null;
        onComplete = null;
        if (this != null && gameObject != null)
            Destroy(gameObject);
    }
}