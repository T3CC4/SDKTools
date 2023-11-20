#if UNITY_EDITOR
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using VRC.Localization;

namespace SDKTools
{
    public class VersionHandler
    {
        public static string Version()
        {
            if(File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "Packages/sdktools/package.json"))) return ExtractVersionFromPackageJson(File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "Packages/sdktools/package.json")));
            return null;
        }

        private static string ExtractVersionFromPackageJson(string jsonContent)
        {
            try
            {
                dynamic jsonObject = JsonConvert.DeserializeObject(jsonContent);
                string version = jsonObject?.version;

                return version ?? "";
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error while extracting SDK Tools version: {ex.Message}");
                return "";
            }
        }

        public static string GetStringByURL(string url)
        {
            UnityWebRequest webRequest = UnityWebRequest.Get(url);
            AsyncOperation asyncOperation = webRequest.SendWebRequest();

            while (!asyncOperation.isDone)
            {
            }

            if (webRequest.isNetworkError || webRequest.isHttpError)
            {
                Debug.LogError($"Error: {webRequest.error}");
                webRequest.Dispose();
                return null;
            }
            else
            {
                string responseText = webRequest.downloadHandler.text;

                webRequest.Dispose();
                return responseText;
            }
        }
    }
}
#endif