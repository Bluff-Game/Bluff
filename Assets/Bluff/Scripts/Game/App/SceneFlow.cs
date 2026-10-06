using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bluff.Game
{
    /// <summary>
    /// Dasar untuk setiap "layar" (menu utama, loading, meja permainan). Membuat kamera, EventSystem, dan Canvas sendiri,
    /// jadi sebuah scene cukup berisi satu GameObject dengan komponen turunan kelas ini.
    /// </summary>
    public abstract class BluffScreen : MonoBehaviour
    {
        static readonly System.Collections.Generic.List<BluffScreen> active = new System.Collections.Generic.List<BluffScreen>();

        /// <summary>Layar yang sedang hidup (dipakai SceneFlow saat berganti layar tanpa scene).</summary>
        public static BluffScreen[] Active => active.ToArray();

        protected Canvas Canvas { get; private set; }
        protected RectTransform Root { get; private set; }

        protected virtual void OnEnable() => active.Add(this);

        protected virtual void OnDisable() => active.Remove(this);

        protected virtual void Awake()
        {
            Application.targetFrameRate = 60;
            Time.timeScale = 1f;
            UIKit.EnsureCamera();
            UIKit.EnsureEventSystem();
            Canvas = UIKit.CreateCanvas(GetType().Name + " Canvas", transform);
            Root = (RectTransform)Canvas.transform;
        }
    }

    /// <summary>
    /// Perpindahan layar: Menu → Loading → Game. Memakai scene kalau sudah terdaftar di Build Settings;
    /// kalau belum, layar diganti di scene yang sama supaya game tetap jalan.
    /// </summary>
    public static class SceneFlow
    {
        public const string MenuScene = "MainMenu";
        public const string LoadingScene = "Loading";
        public const string GameScene = "Game";

        public static void GoToMenu() => Go<MainMenuController>(MenuScene);

        /// <summary>Masuk ke meja permainan lewat layar loading.</summary>
        public static void GoToGame() => Go<LoadingController>(LoadingScene);

        /// <summary>Dipanggil layar loading saat waktunya pindah ke meja.</summary>
        public static AsyncOperation LoadGameAsync()
        {
            if (!Application.CanStreamedLevelBeLoaded(GameScene))
                return null;
            var op = SceneManager.LoadSceneAsync(GameScene);
            op.allowSceneActivation = false;
            return op;
        }

        /// <summary>Ganti layar di scene yang sama (dipakai kalau scene Game belum terdaftar).</summary>
        public static void SwapInPlace<T>() where T : BluffScreen
        {
            Time.timeScale = 1f;
            foreach (var screen in BluffScreen.Active)
                Object.Destroy(screen.gameObject);
            new GameObject(typeof(T).Name, typeof(T));
        }

        static void Go<T>(string scene) where T : BluffScreen
        {
            Time.timeScale = 1f;
            if (Application.CanStreamedLevelBeLoaded(scene))
            {
                SceneManager.LoadScene(scene);
                return;
            }
            // Scene belum disiapkan (menu Bluff > Setup Scenes belum dijalankan): ganti layar di tempat.
            SwapInPlace<T>();
        }
    }

    static class BluffBootstrap
    {
        /// <summary>
        /// Tekan Play di scene kosong mana pun dan menu utama langsung muncul, tanpa setup apa pun.
        /// Tidak aktif kalau scene sudah punya layar Bluff atau Canvas sendiri.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindFirstObjectByType<BluffScreen>() != null)
                return;
            if (Object.FindFirstObjectByType<Canvas>() != null)
                return;
            new GameObject("MainMenu", typeof(MainMenuController));
        }
    }
}
