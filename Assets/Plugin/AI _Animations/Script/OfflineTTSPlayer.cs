using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

public class OfflineTTSPlayer : MonoBehaviour
{
    [Header("Komponen")]
    [SerializeField] private AudioSource audioSource;

    public delegate void AudioEventHandler();
    public event AudioEventHandler OnAudioStart;
    public event AudioEventHandler OnAudioPlaybackComplete;

    private Queue<string> textQueue = new Queue<string>();
    private bool isPlaying = false;

    private string ttsExecutablePath;
    private string tempAudioFilePath;

    void Start()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        }

        // Path ke executable di dalam StreamingAssets
        // Ganti "TextToSpeechCLI.exe" jika nama file Anda berbeda
        ttsExecutablePath = Path.Combine(Application.streamingAssetsPath, "TTS.exe");

        // Path untuk file audio sementara
        tempAudioFilePath = Path.Combine(Application.persistentDataPath, "tts_audio.wav");
    }

    public void PlayText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        textQueue.Enqueue(text);
        if (!isPlaying)
        {
            StartCoroutine(PlayNextInQueue());
        }
    }

    private IEnumerator PlayNextInQueue()
    {
        isPlaying = true;

        while (textQueue.Count > 0)
        {
            string text = textQueue.Dequeue();
            OnAudioStart?.Invoke();

            yield return StartCoroutine(SynthesizeAndPlay(text));
        }

        isPlaying = false;
        UnityEngine.Debug.Log("Antrian audio selesai. Memanggil OnAudioPlaybackComplete.");
        OnAudioPlaybackComplete?.Invoke();
    }

    private IEnumerator SynthesizeAndPlay(string text)
    {
        Process process = new Process();
        process.StartInfo.FileName = ttsExecutablePath;
        process.StartInfo.Arguments = $"\"{tempAudioFilePath}\" \"{text}\"";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;

        try
        {
            process.Start();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"Gagal menjalankan proses TTS: {e.Message}");
            yield break;
        }

        yield return new WaitUntil(() => process.HasExited);

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + tempAudioFilePath, AudioType.WAV))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                audioSource.clip = clip;
                audioSource.Play();
                yield return new WaitWhile(() => audioSource.isPlaying);
            }
            else
            {
                UnityEngine.Debug.LogError("Gagal memuat audio TTS: " + www.error);
            }
        }
    }
}