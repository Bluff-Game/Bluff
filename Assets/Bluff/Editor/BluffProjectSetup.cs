using System.Collections.Generic;
using System.IO;
using Bluff.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bluff.EditorTools
{
    /// <summary>
    /// Membuat scene MainMenu, Loading, &amp; Game, mendaftarkannya di Build Settings, dan mengatur Player Settings dasar.
    /// Jalan otomatis saat proyek dibuka kalau ada scene yang belum ada; bisa juga lewat menu Bluff > Setup Scenes.
    /// </summary>
    [InitializeOnLoad]
    public static class BluffProjectSetup
    {
        const string ScenesFolder = "Assets/Bluff/Scenes";
        const string MenuScenePath = ScenesFolder + "/" + SceneFlow.MenuScene + ".unity";
        const string LoadingScenePath = ScenesFolder + "/" + SceneFlow.LoadingScene + ".unity";
        const string GameScenePath = ScenesFolder + "/" + SceneFlow.GameScene + ".unity";
        const string CheckedKey = "Bluff.SetupChecked.v2";

        static readonly (string path, System.Type controller)[] Scenes =
        {
            (MenuScenePath, typeof(MainMenuController)),
            (LoadingScenePath, typeof(LoadingController)),
            (GameScenePath, typeof(GameController)),
        };

        static BluffProjectSetup()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        static void AutoSetup()
        {
            if (SessionState.GetBool(CheckedKey, false))
                return;
            SessionState.SetBool(CheckedKey, true);
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isBatchMode)
                return;
            bool missing = false;
            foreach (var s in Scenes)
                missing |= !File.Exists(s.path);
            if (!missing && BuildSettingsComplete())
                return;
            Debug.Log("[Bluff] Menyiapkan scene & Build Settings…");
            Setup(false);
        }

        [MenuItem("Bluff/Setup Scenes + Build Settings", priority = 0)]
        public static void SetupFromMenu() => Setup(true);

        /// <summary>Untuk baris perintah: Unity -batchmode -executeMethod Bluff.EditorTools.BluffProjectSetup.SetupBatch</summary>
        public static void SetupBatch() => Setup(false);

        [MenuItem("Bluff/Buka Scene Main Menu", priority = 20)]
        public static void OpenMenuScene()
        {
            if (!File.Exists(MenuScenePath))
                Setup(false);
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(MenuScenePath);
        }

        [MenuItem("Bluff/Hapus Data Pengaturan (PlayerPrefs)", priority = 40)]
        public static void ClearPrefs()
        {
            PlayerPrefs.DeleteAll();
            Debug.Log("[Bluff] PlayerPrefs dihapus.");
        }

        static bool BuildSettingsComplete()
        {
            var paths = new HashSet<string>();
            foreach (var s in EditorBuildSettings.scenes)
                paths.Add(s.path);
            foreach (var s in Scenes)
            {
                if (!paths.Contains(s.path))
                    return false;
            }
            return true;
        }

        static void Setup(bool openMenu)
        {
            Directory.CreateDirectory(ScenesFolder);
            foreach (var s in Scenes)
            {
                if (!File.Exists(s.path))
                    CreateScene(s.path, s.controller);
            }

            var scenes = new List<EditorBuildSettingsScene>();
            foreach (var s in Scenes)
                scenes.Add(new EditorBuildSettingsScene(s.path, true));
            foreach (var existing in EditorBuildSettings.scenes)
            {
                bool ours = false;
                foreach (var s in Scenes)
                    ours |= existing.path == s.path;
                if (!ours)
                    scenes.Add(existing);
            }
            EditorBuildSettings.scenes = scenes.ToArray();

            PlayerSettings.productName = "Bluff";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Bluff] Scene siap (MainMenu, Loading, Game). Tekan Play di scene MainMenu.");

            if (openMenu && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(MenuScenePath);
        }

        /// <summary>Buat scene baru secara aditif (scene yang sedang terbuka tidak diganggu), simpan, lalu tutup.</summary>
        static void CreateScene(string path, System.Type controller)
        {
            // Scene aditif butuh scene aktif yang sudah tersimpan; kalau masih "Untitled", pakai mode tunggal.
            var active = EditorSceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, string.IsNullOrEmpty(active.path) ? NewSceneMode.Single : NewSceneMode.Additive);

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.043f, 0.071f, 0.125f);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);

            // EventSystem sengaja tidak disimpan di scene: dibuat saat runtime sesuai Input System yang aktif.
            var controllerObject = new GameObject(controller.Name, controller);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(controllerObject, scene);

            EditorSceneManager.SaveScene(scene, path);
            if (EditorSceneManager.sceneCount > 1)
                EditorSceneManager.CloseScene(scene, true);
        }
    }
}
