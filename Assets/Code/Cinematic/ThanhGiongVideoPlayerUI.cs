using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Unity Video Player integration for "Thánh Gióng".
/// Uses UnityEngine.Video.VideoPlayer to play the rendered trailer or intro cutscenes in-game.
/// </summary>
public class ThanhGiongVideoPlayerUI : MonoBehaviour
{
    public static ThanhGiongVideoPlayerUI Instance { get; private set; }

    [Header("Video Settings")]
    public string defaultVideoFileName = "ThanhGiong_Trailer_Demo.mp4";
    public bool playOnStart = false;

    private VideoPlayer videoPlayer;
    private AudioSource audioSource;
    private RenderTexture renderTexture;
    private bool isPlaying;
    private GUIStyle hintStyle;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        SetupVideoPlayer();
    }

    private void Start()
    {
        if (playOnStart)
        {
            PlayIntroVideo();
        }
    }

    private void SetupVideoPlayer()
    {
        videoPlayer = gameObject.AddComponent<VideoPlayer>();
        audioSource = gameObject.AddComponent<AudioSource>();

        videoPlayer.playOnAwake = false;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        videoPlayer.SetTargetAudioSource(0, audioSource);
        videoPlayer.isLooping = false;

        renderTexture = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        renderTexture.Create();
        videoPlayer.targetTexture = renderTexture;

        videoPlayer.loopPointReached += OnVideoFinished;
    }

    public void PlayIntroVideo(string customFileName = null)
    {
        string filename = string.IsNullOrEmpty(customFileName) ? defaultVideoFileName : customFileName;
        string videoPath = Path.Combine(Application.dataPath, "..", "Builds", "Videos", filename);

        if (!File.Exists(videoPath))
        {
            // Also check StreamingAssets
            string streamingPath = Path.Combine(Application.streamingAssetsPath, filename);
            if (File.Exists(streamingPath))
            {
                videoPath = streamingPath;
            }
        }

        if (File.Exists(videoPath))
        {
            videoPlayer.url = videoPath;
            videoPlayer.Play();
            isPlaying = true;
        }
        else
        {
            Debug.LogWarning($"[ThanhGiongVideoPlayerUI] Video file not found: {videoPath}. Directing to in-engine trailer.");
            ThanhGiongTrailerDirector.Instance?.PlayTrailer(false);
        }
    }

    public void StopVideo()
    {
        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            videoPlayer.Stop();
        }
        isPlaying = false;
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        isPlaying = false;
    }

    private void Update()
    {
        if (isPlaying && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space)))
        {
            StopVideo();
        }
    }

    private void OnGUI()
    {
        if (!isPlaying || renderTexture == null) return;

        GUI.depth = -2000;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), renderTexture, ScaleMode.ScaleToFit);

        if (hintStyle == null)
        {
            hintStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.LowerRight,
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
            hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
        }

        GUI.Label(new Rect(Screen.width - 250, Screen.height - 35, 230, 25), "[ESC / SPACE: Bỏ qua]", hintStyle);
    }

    private void OnDestroy()
    {
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }
}
