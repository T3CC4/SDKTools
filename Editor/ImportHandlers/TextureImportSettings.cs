#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SDKTools
{
    [InitializeOnLoad]
    public class TextureImportSettings : AssetPostprocessor
    {
        static TextureImportSettings()
        {
            EditorApplication.delayCall += SetTextureImportSettings;
        }

        public static void SetTextureImportSettings()
        {
            string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });

            foreach (string textureGuid in textureGuids)
            {
                string texturePath = AssetDatabase.GUIDToAssetPath(textureGuid);

                if (!texturePath.StartsWith("Assets/"))
                    return;

                TextureImporter textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;

                if (textureImporter != null)
                {
                    if (textureImporter.streamingMipmaps == true) return;
                    if (textureImporter.textureType != TextureImporterType.Default) return;
                    textureImporter.textureType = TextureImporterType.Default;
                    textureImporter.mipmapEnabled = true;
                    textureImporter.streamingMipmaps = true;
                    textureImporter.streamingMipmapsPriority = 0;
                    AssetDatabase.ImportAsset(texturePath);
                    AssetDatabase.Refresh();
                    Debug.Log("Streaming mipmaps enabled for texture: " + texturePath);
                }
            }
        }

        private void OnPostprocessTexture(Texture2D texture)
        {
            TextureImporter textureImporter = assetImporter as TextureImporter;
            if (textureImporter.streamingMipmaps == true) return;
            if (textureImporter.textureType != TextureImporterType.Default) return;
            textureImporter.textureType = TextureImporterType.Default;
            textureImporter.mipmapEnabled = true;
            textureImporter.streamingMipmaps = true;
            textureImporter.streamingMipmapsPriority = 0;
            AssetDatabase.ImportAsset(textureImporter.assetPath);
            AssetDatabase.Refresh();
            Debug.Log("Streaming mipmaps enabled for texture: " + textureImporter.assetPath);
        }
    }

    public class TextureImportEditor : Editor
    {
        [MenuItem("VRChat SDK/SDKTools/QoL/Set Texture Settings")]
        public static void SetTextureSettings()
        {
            TextureImportSettings.SetTextureImportSettings();
        }
    }
}
#endif