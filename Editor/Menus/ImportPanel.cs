#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;
using UnityEngine.Networking;
using System.Linq;
using SDKTools;

namespace SDKTools
{
    public class ImportPanel : EditorWindow
    {
        private string searchQuery = "";
        private Texture2D image;

        private string version = VersionHandler.Version();
        private string latestversion = string.Empty;

        private bool isEditMode = false;

        private bool serverConnected = true;

        private Vector2 scrollPosition;

        private string jsonFilePath;
        private Dictionary<string, List<string>> fileCategories;

        [MenuItem("VRChat SDK/SDKTools/Import Panel")]
        public static void ShowWindow()
        {
            var window = GetWindow<ImportPanel>("Import Panel");
            window.maxSize = new Vector2(650, 900);
        }

        private void OnEnable()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string destinationPath = Path.Combine(appDataPath, "SDKTools");
            if (!Directory.Exists(destinationPath)) Directory.CreateDirectory(destinationPath);
            MoveFilesToFolder(Path.Combine(Directory.GetCurrentDirectory(), "Packages/SDKTools/Runtime/Imports"), destinationPath);
            LoadFileCategories();
            AssignUncategorizedCategory();
            latestversion = VersionHandler.GetStringByURL("https://api.SDKTools.repl.co");
            image = EditorGUIUtility.Load("Packages/SDKTools/Runtime/Resources/SDKTools.png") as Texture2D;
        }

        private void OnGUI()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(image, GUILayout.Height(225));
            GUILayout.EndVertical();
            if(!serverConnected)
            {
                EditorGUILayout.HelpBox("Sadly there was no connection to the server so we don't know if this version is the latest!", MessageType.Error);
                if (GUILayout.Button("Download anyways"))
                {
                    System.Diagnostics.Process.Start("https://mega.nz/folder/hfUXHTxC#Fxzvh40N2ZXfxU5c42csfA");
                }
            }
            else
            {
                if ("v" + version != latestversion)
                {
                    EditorGUILayout.HelpBox("Your current version is v\"" + version + "\", the latest is \"" + latestversion + "\" so you have to update!", MessageType.Info);
                    if (GUILayout.Button("Download"))
                    {
                        System.Diagnostics.Process.Start("https://mega.nz/folder/JSc0HbiJ#ZJP3BMLAcepY3PeWmVeUbQ");
                    }
                }
            }
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Space(10);
            searchQuery = GUILayout.TextField(searchQuery, GUI.skin.FindStyle("ToolbarSeachTextField"), GUILayout.Width(200));
            if (GUILayout.Button("", GUI.skin.FindStyle("ToolbarSeachCancelButton")))
            {
                searchQuery = "";
                GUI.FocusControl(null);
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                LoadFileCategories();
                AssignUncategorizedCategory();
            }
            if (GUILayout.Button(isEditMode ? "Exit Edit Mode" : "Enter Edit Mode", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                isEditMode = !isEditMode;
            }
            if(GUILayout.Button("Add Package", EditorStyles.toolbarButton, GUILayout.Width(90)))
            {
                string filePath = EditorUtility.OpenFilePanel("Select Unity Package", "", "unitypackage");
                if (!string.IsNullOrEmpty(filePath))
                {
                    string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    string destinationPath = Path.Combine(appDataPath, "SDKTools");
                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    File.Copy(filePath, Path.Combine(destinationPath, fileName + ".unitypackage"), true);
                    AssignUncategorizedCategory();
                }
            }
            GUILayout.Space(10);
            EditorGUILayout.EndHorizontal();

            ListFilesByCategories();         
        }

        private void MoveFilesToFolder(string sourceFolderPath, string destinationFolderPath)
        {
            if (!Directory.Exists(sourceFolderPath)) return;
            if (!Directory.Exists(destinationFolderPath)) Directory.CreateDirectory(destinationFolderPath);
            // Get all files in the source folder
            string[] files = Directory.GetFiles(sourceFolderPath);

            foreach (string filePath in files)
            {
                // Get the file name
                string fileName = Path.GetFileName(filePath);

                // Construct the destination file path
                string destinationFilePath = Path.Combine(destinationFolderPath, fileName);

                // Check if the file already exists in the destination folder
                if (File.Exists(destinationFilePath))
                {
                    // Delete the existing file
                    File.Delete(destinationFilePath);
                }

                // Move the file to the destination folder
                File.Move(filePath, destinationFilePath);
            }

            Directory.Delete(sourceFolderPath, true);
            AssetDatabase.Refresh();
        }


        string[] okok = new string[99999];

        private void ListFilesByCategories()
        {
            int fileAmount = 0;
            GUILayout.Space(10);

            if (fileCategories != null)
            {
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

                var categoriesCopy = new Dictionary<string, List<string>>(fileCategories);

                for (int i = 0; i < categoriesCopy.Count; i++)
                {
                    GUI.backgroundColor = new Color(0.75f, 0.75f, 0.75f);
                    GUILayout.BeginVertical(GUI.skin.box);

                    var category = categoriesCopy.ElementAt(i);
                    string categoryName = category.Key;

                    EditorGUILayout.LabelField(categoryName, EditorStyles.boldLabel);

                    if (category.Value == null)
                    {
                        EditorGUILayout.LabelField("No files in this category");
                        continue;
                    }

                    var filePathsCopy = new List<string>(category.Value);
                    bool isDark = false;
                    for (int e = 0; e < filePathsCopy.Count; e++)
                    {
                        fileAmount++;
                        string filePath = filePathsCopy[e];
                        if (string.IsNullOrEmpty(searchQuery) || filePath.ToLower().Contains(searchQuery.ToLower()))
                        {
                            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                            string destinationPath = Path.Combine(appDataPath, "SDKTools/");

                            if (isEditMode)
                            {
                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField(filePath);
                                okok[e + filePathsCopy.Count + 232] = EditorGUILayout.TextField(okok[e + filePathsCopy.Count + 232]);
                                if (GUILayout.Button("Save", GUILayout.Width(80)))
                                {
                                    MoveFileToCategory(filePath, categoryName, okok[e + filePathsCopy.Count + 232]);
                                }
                                if (GUILayout.Button("Delete", GUILayout.Width(60)))
                                {
                                    File.Delete(destinationPath + filePath);
                                    LoadFileCategories();
                                    AssignUncategorizedCategory();
                                }
                                EditorGUILayout.EndHorizontal();
                            }
                            else
                            {
                                if (isDark)
                                    GUI.backgroundColor = new Color(0.75f, 0.75f, 0.75f); // Darker color
                                else
                                    GUI.backgroundColor = Color.white; // Lighter color

                                isDark = !isDark; // Toggle isDark variable

                                string fileName = Path.GetFileNameWithoutExtension(filePath); // Get file name without extension
                                if (GUILayout.Button(fileName))
                                {
                                    AssetDatabase.ImportPackage(destinationPath + filePath, true);
                                }
                            }
                        }
                    }
                    EditorGUILayout.Space();
                    GUILayout.EndVertical();
                    EditorGUILayout.Space();
                }
                EditorGUILayout.EndScrollView();
            }
            GUILayout.Label("Fetched Files: " + fileAmount);
        }

        private void LoadFileCategories()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            jsonFilePath = Path.Combine(appDataPath, "SDKTools/categories.json");
            string destinationPath = Path.Combine(appDataPath, "SDKTools");

            if (!string.IsNullOrEmpty(jsonFilePath) && File.Exists(jsonFilePath))
            {
                string json = File.ReadAllText(jsonFilePath);
                fileCategories = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json);

                // Remove categories with non-existent files
                var categoriesToRemove = new List<string>();

                foreach (var category in fileCategories)
                {
                    var filesToRemove = new List<string>();

                    foreach (var filePath in category.Value)
                    {
                        string fullPath = Path.Combine(destinationPath, filePath);

                        if (!File.Exists(fullPath))
                        {
                            filesToRemove.Add(filePath);
                        }
                    }

                    foreach (var filePath in filesToRemove)
                    {
                        category.Value.Remove(filePath);
                    }

                    if (category.Value.Count == 0)
                    {
                        categoriesToRemove.Add(category.Key);
                    }
                }

                foreach (var categoryToRemove in categoriesToRemove)
                {
                    fileCategories.Remove(categoryToRemove);
                }
            }
            else
            {
                // Create a new dictionary if the file doesn't exist
                fileCategories = new Dictionary<string, List<string>>();
            }
        }


        private void AssignUncategorizedCategory()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string destinationPath = Path.Combine(appDataPath, "SDKTools");

            // Check if the "uncategorized" category already exists
            if (!fileCategories.ContainsKey("Uncategorized"))
            {
                fileCategories["Uncategorized"] = new List<string>();
            }

            // Get all Unity packages in the destination folder
            string[] unityPackages = Directory.GetFiles(destinationPath, "*.unitypackage");

            foreach (string packagePath in unityPackages)
            {
                string fileName = Path.GetFileName(packagePath);

                // Check if the package is already assigned to a category
                bool isAssigned = false;
                foreach (var category in fileCategories)
                {
                    if (category.Value.Contains(fileName))
                    {
                        isAssigned = true;
                        break;
                    }
                }

                // Assign the package to the "uncategorized" category if not already assigned
                if (!isAssigned)
                {
                    fileCategories["Uncategorized"].Add(fileName);
                }
            }

            // Save the updated categories to JSON
            SaveCategoriesToJson();
        }

        private void MoveFileToCategory(string filePath, string currentCategory, string newCategory)
        {
            // Remove the file from the current category if it exists
            if (fileCategories.ContainsKey(currentCategory))
            {
                fileCategories[currentCategory].Remove(filePath);

                // Delete the current category if it becomes empty
                if (fileCategories[currentCategory].Count == 0)
                {
                    fileCategories.Remove(currentCategory);
                }
            }

            // Add the file to the new category
            if (!fileCategories.ContainsKey(newCategory))
            {
                fileCategories[newCategory] = new List<string>();
            }
            fileCategories[newCategory].Add(filePath);

            // Save the updated categories to JSON
            SaveCategoriesToJson();
        }

        private void SaveCategoriesToJson()
        {
            string json = JsonConvert.SerializeObject(fileCategories, Formatting.Indented);
            File.WriteAllText(jsonFilePath, json);
        }

    }
}
#endif