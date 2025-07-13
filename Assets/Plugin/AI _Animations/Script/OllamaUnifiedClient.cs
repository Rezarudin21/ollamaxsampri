using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NativeWebSocket;
using Debug = UnityEngine.Debug;

public class OllamaUnifiedClient : MonoBehaviour
{
    [Header("UI")]
    public TMP_InputField inputField;
    public TMP_Text outputText;
    public GameObject loadingPanel;

    [Header("WebSocket")]
    public string websocketUrl = "ws://localhost:8765";
    public float responseTimeout = 30f;

    // --- PERUBAHAN DI SINI ---
    [Header("Komponen")]
    public OfflineTTSPlayer audioPlayer; // Mengganti LMNTAudioPlayer dengan OfflineTTSPlayer
    public Animator animationController;
    // --- AKHIR PERUBAHAN ---

    private WebSocket websocket;
    private StringBuilder sentenceBuilder = new StringBuilder();
    private Queue<string> sentenceQueueForAudio = new Queue<string>();
    private Queue<string> sentenceQueueForDisplay = new Queue<string>();

    private bool isAwaitingResponse = false;
    private Coroutine audioRequestCoroutine = null;

    void Start()
    {
        // Nonaktifkan input dan tampilkan panel loading saat mulai
        inputField.interactable = false;
        loadingPanel.SetActive(true);

        // Dengarkan event dari ServerManager
        ServerManager.OnAllServersReady += HandleServersReady;

        if (animationController == null)
            animationController = GetComponent<Animator>() ?? FindAnyObjectByType<Animator>();

        // --- PERUBAHAN DI SINI ---
        if (audioPlayer == null)
            audioPlayer = GetComponent<OfflineTTSPlayer>() ?? FindAnyObjectByType<OfflineTTSPlayer>();
        // --- AKHIR PERUBAHAN ---

        if (audioPlayer != null)
        {
            audioPlayer.OnAudioStart += OnAudioStartHandler;
            audioPlayer.OnAudioPlaybackComplete += HandleAudioPlaybackComplete;
        }

        ConnectWebSocket();
    }

    private void HandleServersReady()
    {
        // Fungsi ini akan dipanggil ketika semua server sudah siap
        UnityEngine.Debug.Log("OllamaUnifiedClient menerima sinyal server siap. Mengaktifkan UI.");

        // Aktifkan input dan sembunyikan panel loading
        inputField.interactable = true;
        loadingPanel.SetActive(false);

        // Sekarang baru kita hubungkan WebSocket
        ConnectWebSocket();
    }

    private async void ConnectWebSocket()
    {
        websocket = new WebSocket(websocketUrl);

        websocket.OnOpen += () => Debug.Log("WebSocket connected");
        websocket.OnError += (e) => Debug.LogError("WebSocket error: " + e);
        websocket.OnClose += (e) => {
            Debug.Log("WebSocket closed");
            if (isAwaitingResponse) ReturnToIdle();
        };

        websocket.OnMessage += (bytes) => {
            string msg = Encoding.UTF8.GetString(bytes);
            try
            {
                OllamaResponse chunk = JsonUtility.FromJson<OllamaResponse>(msg);
                HandleIncomingText(chunk.response, chunk.done);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Gagal parsing JSON: {msg}\n{ex.Message}");
            }
        };

        await websocket.Connect();
    }

    public void OnSendButtonPressed()
    {
        string userInput = inputField.text.Trim();
        if (string.IsNullOrEmpty(userInput) || isAwaitingResponse) return;

        ResetState();
        animationController?.SetBool("isThinking", true);
        isAwaitingResponse = true;

        if (userInput.ToLower().Contains("siapa") && userInput.ToLower().Contains("kamu"))
        {
            string customResponse = "Halo! Saya adalah asisten virtual yang berjalan secara offline, dibuat oleh prodi Informatika UMM. Ada yang bisa saya bantu?";
            ProcessSingleSentence(customResponse);
            // Karena TTS sekarang offline, kita bisa langsung kembali idle setelah memproses.
            // Logika transisi state kini sepenuhnya dikontrol oleh event audio.
            isAwaitingResponse = false;
            return;
        }

        SendMessageToServer(userInput);
    }

    private void ResetState()
    {
        sentenceBuilder.Clear();
        sentenceQueueForAudio.Clear();
        sentenceQueueForDisplay.Clear();
        outputText.text = "";

        if (audioRequestCoroutine != null)
        {
            StopCoroutine(audioRequestCoroutine);
            audioRequestCoroutine = null;
        }
    }

    public async void SendMessageToServer(string message)
    {
        if (websocket.State != WebSocketState.Open)
        {
            Debug.LogWarning("WebSocket not connected. Attempting to reconnect...");
            await websocket.Connect();
        }

        if (websocket.State == WebSocketState.Open)
        {
            await websocket.SendText(message);
        }
        else
        {
            Debug.LogError("Failed to send message, WebSocket is not open.");
            ReturnToIdle();
        }
    }

    private void HandleIncomingText(string chunk, bool isFinal)
    {
        sentenceBuilder.Append(chunk);

        while (true)
        {
            string text = sentenceBuilder.ToString();
            int sentenceEnd = text.IndexOfAny(new char[] { '.', '?', '!' });

            if (sentenceEnd == -1) break;

            string sentence = text.Substring(0, sentenceEnd + 1).Trim();
            sentenceBuilder.Remove(0, sentenceEnd + 1);

            if (!string.IsNullOrWhiteSpace(sentence))
            {
                ProcessSingleSentence(sentence);
            }
        }

        if (isFinal)
        {
            string leftover = sentenceBuilder.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(leftover))
            {
                ProcessSingleSentence(leftover);
            }
            sentenceBuilder.Clear();
            isAwaitingResponse = false;
        }
    }

    private void ProcessSingleSentence(string sentence)
    {
        sentenceQueueForAudio.Enqueue(sentence);
        sentenceQueueForDisplay.Enqueue(sentence);

        if (audioRequestCoroutine == null)
        {
            audioRequestCoroutine = StartCoroutine(RequestAudioSequentially());
        }
    }

    private IEnumerator RequestAudioSequentially()
    {
        while (sentenceQueueForAudio.Count > 0)
        {
            // Cek apakah audio player sedang sibuk
            // Ini mencegah pemanggilan PlayText berkali-kali jika coroutine sebelumnya masih berjalan
            if (audioPlayer != null)
            {
                string sentence = sentenceQueueForAudio.Dequeue();
                audioPlayer.PlayText(sentence);
            }
            // Tunggu sebentar sebelum memproses antrian berikutnya jika perlu
            yield return new WaitForSeconds(0.1f);
        }
        audioRequestCoroutine = null;
    }

    private void OnAudioStartHandler()
    {
        animationController?.SetBool("isThinking", false);
        animationController?.SetBool("isTalking", true);

        if (sentenceQueueForDisplay.Count > 0)
        {
            string sentenceToDisplay = sentenceQueueForDisplay.Dequeue();
            outputText.text += sentenceToDisplay + " ";
        }
    }

    private void HandleAudioPlaybackComplete()
    {
        Debug.Log("Rangkaian audio telah selesai diputar.");
        animationController?.SetBool("isTalking", false);

        if (isAwaitingResponse || sentenceQueueForAudio.Count > 0)
        {
            Debug.Log("Masih menunggu data, masuk ke mode Thinking.");
            animationController?.SetBool("isThinking", true);
        }
        else
        {
            Debug.Log("Semua proses selesai, kembali ke Idle.");
            ReturnToIdle();
        }
    }

    private void ReturnToIdle()
    {
        isAwaitingResponse = false;
        animationController?.SetBool("isTalking", false);
        animationController?.SetBool("isThinking", false);
        Debug.Log("System is now Idle.");
    }

#if !UNITY_WEBGL || UNITY_EDITOR
    void Update()
    {
        websocket?.DispatchMessageQueue();
    }
#endif

    private void OnDestroy()
    {
        ServerManager.OnAllServersReady -= HandleServersReady;

        if (audioPlayer != null)
        {
            audioPlayer.OnAudioStart -= OnAudioStartHandler;
            audioPlayer.OnAudioPlaybackComplete -= HandleAudioPlaybackComplete;
        }
        _ = websocket?.Close();
    }

    [Serializable]
    private class OllamaResponse
    {
        public string response;
        public bool done;
    }
}