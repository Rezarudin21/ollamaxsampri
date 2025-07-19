using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

public class OfflineTTSPlayer : MonoBehaviour
{
    [Header("Pengaturan Piper")]
    [Tooltip("Nama file model .onnx yang akan digunakan.")]
    public string modelName = "en_US-ryan-medium.onnx";

    [Header("Komponen")]
    [SerializeField] private AudioSource audioSource;

    public delegate void AudioEventHandler();
    public event AudioEventHandler OnAudioStart;
    public event AudioEventHandler OnAudioPlaybackComplete;

    private Queue<string> textQueue = new Queue<string>();
    private bool isPlaying = false;
    private string piperExecutablePath;
    private string modelPath;
    private string tempAudioFilePath;

    void Start()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        }

        string gameRootPath = Path.GetDirectoryName(Application.dataPath);
        if (Application.isEditor)
        {
            // Saat di editor, asumsikan folder tts-engine ada di root proyek
            gameRootPath = Directory.GetCurrentDirectory();
        }

        piperExecutablePath = Path.Combine(gameRootPath, "tts-engine", "piper.exe");
        modelPath = Path.Combine(gameRootPath, "tts-engine", "model", modelName);
        tempAudioFilePath = Path.Combine(Application.persistentDataPath, "piper_output.wav");
    }

    // ... (Fungsi PlayText dan PlayNextInQueue tetap sama) ...
    public void PlayText(string text) { if (!string.IsNullOrWhiteSpace(text)) { textQueue.Enqueue(text); if (!isPlaying) { StartCoroutine(PlayNextInQueue()); } } }
    private IEnumerator PlayNextInQueue() { isPlaying = true; while (textQueue.Count > 0) { string text = textQueue.Dequeue(); OnAudioStart?.Invoke(); yield return StartCoroutine(SynthesizeAndPlay(text)); } isPlaying = false; UnityEngine.Debug.Log("Antrian audio selesai."); OnAudioPlaybackComplete?.Invoke(); }


    private IEnumerator SynthesizeAndPlay(string text)
    {
        // Hapus file audio lama jika ada untuk menghindari error
        if (File.Exists(tempAudioFilePath))
        {
            File.Delete(tempAudioFilePath);
        }

        string arguments = $"--model \"{modelPath}\" --output_file \"{tempAudioFilePath}\"";

        Process process = new Process();
        process.StartInfo.FileName = "cmd.exe"; // Jalankan via cmd untuk menangani echo
        process.StartInfo.Arguments = $"/C echo \"{text}\" | \"{piperExecutablePath}\" {arguments}";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;

        UnityEngine.Debug.Log($"Menjalankan Piper: {process.StartInfo.Arguments}");

        try
        {
            process.Start();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"Gagal menjalankan proses Piper: {e.Message}\nPastikan path sudah benar: {piperExecutablePath}");
            yield break;
        }

        yield return new WaitUntil(() => process.HasExited);

        if (!File.Exists(tempAudioFilePath))
        {
            UnityEngine.Debug.LogError("Piper selesai, tetapi file output .wav tidak ditemukan!");
            yield break;
        }

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
                UnityEngine.Debug.LogError("Gagal memuat audio dari Piper: " + www.error);
            }
        }
    }
}