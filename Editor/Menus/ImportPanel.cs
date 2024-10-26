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
using CG.Web.MegaApiClient;

namespace SDKTools
{
    public class ImportPanel : EditorWindow
    {
        private string searchQuery = "";
        private Texture2D image;

        private bool isEditMode = false;

        private bool serverConnected = true;

        private Vector2 scrollPosition;

        private static string jsonFilePath;
        public static Dictionary<string, List<string>> fileCategories;

        private bool addMegaPackage = false;

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
            image = EditorGUIUtility.Load("Packages/SDKTools/Runtime/Resources/SDKTools.png") as Texture2D;
        }

        private void OnGUI()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(image, GUILayout.Height(225));
            GUILayout.EndVertical();
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Space(10);
            searchQuery = GUILayout.TextField(searchQuery, EditorStyles.toolbarSearchField, GUILayout.Width(200));
            if (GUILayout.Button("X", EditorStyles.toolbarButton))
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
                //if (EditorUtility.DisplayDialog("Add Package", "Add package from PC or Mega.nz?", "PC", "Mega.nz"))
                //{
                    string filePath = EditorUtility.OpenFilePanel("Select Unity Package", "", "unitypackage");
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        string destinationPath = Path.Combine(appDataPath, "SDKTools");
                        string fileName = Path.GetFileNameWithoutExtension(filePath);
                        File.Copy(filePath, Path.Combine(destinationPath, fileName + ".unitypackage"), true);
                        AssignUncategorizedCategory();
                    }
                //}
                //else
                //{
                //    addMegaPackage = true;
                //}
            }
            GUILayout.Space(10);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            if (addMegaPackage)
            {
                string megaLink = EditorGUILayout.TextField("Mega.nz Link:", "");
                string packageName = EditorGUILayout.TextField("Package Name:", "");
                string category = EditorGUILayout.TextField("Category:", "");
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Save"))
                {
                    AssignCategoryMega(megaLink, packageName, category);
                    addMegaPackage = false;
                }
                if(GUILayout.Button("Cancel"))
                {
                    addMegaPackage = false;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.Space();
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

                // Kopie der Kategorien, um Modifikationen zu vermeiden
                var categoriesCopy = new Dictionary<string, List<string>>(fileCategories);

                foreach (var category in categoriesCopy)
                {
                    string categoryName = category.Key;

                    // Filtere Dateien, die zur Suchanfrage passen
                    var matchingFiles = category.Value
                        .Where(filePath => string.IsNullOrEmpty(searchQuery) || filePath.ToLower().Contains(searchQuery.ToLower()))
                        .ToList();

                    // Überspringe Kategorie, wenn keine Dateien zur Suchanfrage passen
                    if (matchingFiles.Count == 0)
                        continue;

                    GUI.backgroundColor = new Color(0.75f, 0.75f, 0.75f);
                    GUILayout.BeginVertical(GUI.skin.box);

                    EditorGUILayout.LabelField(categoryName, EditorStyles.boldLabel);

                    bool isDark = false;
                    foreach (var filePath in matchingFiles)
                    {
                        fileAmount++;
                        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        string destinationPath = Path.Combine(appDataPath, "SDKTools/");

                        if (isEditMode)
                        {
                            EditorGUILayout.BeginHorizontal();
                            if (!filePath.Contains("|"))
                            {
                                EditorGUILayout.LabelField(filePath);

                                // Begin checking for changes in the input field
                                EditorGUI.BeginChangeCheck();
                                okok[fileAmount] = EditorGUILayout.TextField(okok[fileAmount]);
                                if (GUILayout.Button("Save", GUILayout.Width(80)))
                                {
                                    MoveFileToCategory(filePath, categoryName, okok[fileAmount]);
                                }

                                if (GUILayout.Button("Delete", GUILayout.Width(60)))
                                {
                                    File.Delete(destinationPath + filePath);
                                    LoadFileCategories();
                                    AssignUncategorizedCategory();
                                }
                            }
                            else
                            {
                                string[] packaginfo = filePath.Split('|');
                                EditorGUILayout.LabelField(packaginfo[0] + " (from Mega.nz)");

                                okok[fileAmount] = EditorGUILayout.TextField(okok[fileAmount]);
                                if (GUILayout.Button("Save", GUILayout.Width(80)))
                                {
                                    MoveFileToCategory(filePath, categoryName, okok[fileAmount]);
                                }

                                if (GUILayout.Button("Delete", GUILayout.Width(60)))
                                {
                                    File.Delete(destinationPath + filePath);
                                    LoadFileCategories();
                                    AssignUncategorizedCategory();
                                }
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                        else
                        {
                            GUI.backgroundColor = isDark ? new Color(0.75f, 0.75f, 0.75f) : Color.white;
                            isDark = !isDark;

                            if (!filePath.Contains("|"))
                            {
                                string fileName = Path.GetFileNameWithoutExtension(filePath);
                                if (GUILayout.Button(fileName))
                                {
                                    AssetDatabase.ImportPackage(destinationPath + filePath, true);
                                }
                            }
                            else
                            {
                                string[] packaginfo = filePath.Split('|');
                                if (GUILayout.Button(packaginfo[0] + " (from Mega.nz)"))
                                {
                                    if (packaginfo.Length <= 3)
                                    {
                                        while (!MegaAPI.DownloadFile(packaginfo)) { }
                                        AssetDatabase.ImportPackage(destinationPath + packaginfo[3], true);
                                    }
                                    else
                                    {
                                        AssetDatabase.ImportPackage(destinationPath + packaginfo[3], true);
                                    }
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

        private void AssignCategoryMega(string megaLink, string packageName, string category)
        {
            // Check if the specified category already exists
            if (!fileCategories.ContainsKey(category))
            {
                fileCategories[category] = new List<string>();
            }

            fileCategories[category].Add($"{packageName}|{megaLink}|{category}");

            // Save the updated categories to JSON
            SaveCategoriesToJson();
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

        public static void SaveCategoriesToJson()
        {
            string json = JsonConvert.SerializeObject(fileCategories, Formatting.Indented);
            File.WriteAllText(jsonFilePath, json);
        }

    }
}
#endif