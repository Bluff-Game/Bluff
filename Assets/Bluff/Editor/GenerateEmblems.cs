using System;
using System.IO;
using Bluff.Game;
using UnityEditor;
using UnityEngine;

namespace Bluff.EditorTools
{
    public static class GenerateEmblems
    {
        [MenuItem("Bluff/Art/Generate Original Icons")]
        public static void Generate()
        {
            string folder = Path.Combine(Application.dataPath, "Bluff/Resources/Icons");
            Directory.CreateDirectory(folder);
            foreach (GameIcon icon in Enum.GetValues(typeof(GameIcon)))
            {
                var texture = SpriteFactory.GenerateEmblem(icon);
                File.WriteAllBytes(Path.Combine(folder, icon + ".png"), texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
            AssetDatabase.Refresh();
            foreach (GameIcon icon in Enum.GetValues(typeof(GameIcon)))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Bluff/Resources/Icons/" + icon + ".png");
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 256;
                importer.SaveAndReimport();
            }
        }
    }
}
