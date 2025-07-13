using UnityEngine;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Net.Sockets; // Diperlukan untuk memeriksa port
using System; // Diperlukan untuk Action event
using System.Collections;

public class ServerManager : MonoBehaviour
{
    // Event yang akan dipicu ketika semua server siap
    public static event Action OnAllServersReady;

    // Daftar untuk menyimpan semua proses server yang sedang berjalan
    private List<Process> serverProcesses = new List<Process>();

    // Daftar port yang perlu kita periksa
    private List<int> portsToCheck = new List<int> { 8765, 5000 };
    private HashSet<int> readyPorts = new HashSet<int>();

    void Start()
    {
        // Jalankan kedua server saat game dimulai
        StartServer("servers/whisper_server.py");
        StartServer("servers/ollama_ws_proxy.py");

        // Mulai coroutine untuk memeriksa status server
        StartCoroutine(CheckServerStatus());
    }

    private IEnumerator CheckServerStatus()
    {
        UnityEngine.Debug.Log("Mulai memeriksa status server...");

        // Loop sampai semua port terdeteksi siap
        while (readyPorts.Count < portsToCheck.Count)
        {
            foreach (int port in portsToCheck)
            {
                // Hanya periksa port yang belum dikonfirmasi siap
                if (!readyPorts.Contains(port) && IsPortOpen(port))
                {
                    UnityEngine.Debug.Log($"Port {port} sekarang terbuka! Server siap.");
                    readyPorts.Add(port);
                }
            }

            // Tunggu 1 detik sebelum mencoba lagi
            yield return new WaitForSeconds(1f);
        }

        UnityEngine.Debug.Log("--- Semua server sudah siap! ---");
        // Panggil event untuk memberitahu skrip lain
        OnAllServersReady?.Invoke();
    }

    private bool IsPortOpen(int port)
    {
        try
        {
            // Coba buat koneksi TCP ke port. Jika berhasil, port terbuka.
            using (var client = new TcpClient("localhost", port))
            {
                return true;
            }
        }
        catch (SocketException)
        {
            // Jika koneksi ditolak, port belum terbuka.
            return false;
        }
    }

    void StartServer(string relativeScriptPath)
    {
        // (Fungsi ini tetap sama seperti sebelumnya, tidak perlu diubah)
        string gameRootPath = Path.GetDirectoryName(Application.dataPath);
        if (Application.isEditor)
        {
            gameRootPath = Application.dataPath;
        }
        string fullScriptPath = Path.Combine(gameRootPath, relativeScriptPath);
        if (!File.Exists(fullScriptPath))
        {
            UnityEngine.Debug.LogError($"Server script not found at: {fullScriptPath}");
            return;
        }
        ProcessStartInfo startInfo = new ProcessStartInfo()
        {
            FileName = "pythonw.exe",
            Arguments = $"\"{fullScriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(fullScriptPath)
        };
        try
        {
            Process serverProcess = Process.Start(startInfo);
            if (serverProcess != null)
            {
                serverProcesses.Add(serverProcess);
                UnityEngine.Debug.Log($"Started server: {relativeScriptPath} with PID: {serverProcess.Id}");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"Error starting server {relativeScriptPath}: {e.Message}");
        }
    }

    void OnApplicationQuit()
    {
        // (Fungsi ini tetap sama seperti sebelumnya, tidak perlu diubah)
        UnityEngine.Debug.Log("Quitting application, stopping all servers...");
        foreach (var process in serverProcesses)
        {
            if (!process.HasExited)
            {
                try { process.Kill(); }
                catch { /* Abaikan error jika proses sudah mati */ }
            }
        }
    }
}