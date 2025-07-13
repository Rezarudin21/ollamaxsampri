using UnityEngine;
using UnityEngine.SceneManagement; // Wajib ditambahkan untuk mengelola scene

public class SceneLoader : MonoBehaviour
{
    // Method ini harus public agar bisa diakses oleh Button dari Inspector
    public void RestartLevel()
    {
        // Mengambil index dari scene yang sedang aktif saat ini
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;

        // Memuat ulang scene berdasarkan index-nya
        SceneManager.LoadScene(currentSceneIndex);

        Debug.Log("Level has been restarted!");
    }
}