#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SDKTools
{
    [InitializeOnLoad]
    public class AudioImportSettings : AssetPostprocessor
    {
        static AudioImportSettings()
        {
            EditorApplication.delayCall += SetAudioImportSettings;
        }

        private static void SetAudioImportSettings()
        {
            string[] audioGuids = AssetDatabase.FindAssets("t:AudioClip");

            foreach (string audioGuid in audioGuids)
            {
                string audioPath = AssetDatabase.GUIDToAssetPath(audioGuid);

                if (!audioPath.StartsWith("Assets/"))
                    return;

                AudioImporter audioImporter = AssetImporter.GetAtPath(audioPath) as AudioImporter;

                if (audioImporter != null)
                {
                    if (audioImporter.loadInBackground == true) return;
                    audioImporter.loadInBackground = true;
                    AssetDatabase.ImportAsset(audioPath);
                    Debug.Log("Load in Background set to true for audio file: " + audioPath);
                }
            }
        }

        private void OnPostprocessAudio(AudioClip audioClip)
        {
            AudioImporter audioImporter = assetImporter as AudioImporter;
            if (audioImporter.loadInBackground == true) return;
            audioImporter.loadInBackground = true;
            AssetDatabase.ImportAsset(audioImporter.assetPath);
            Debug.Log("Load in Background set to true for audio file: " + audioImporter.assetPath);
        }
    }
}
#endif