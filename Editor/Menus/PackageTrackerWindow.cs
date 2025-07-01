#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;
using System.Linq;
using System.Security.Cryptography;

namespace SDKTools
{
    [System.Serializable]
    public class PackageImportRecord
    {
        public string packageName;
        public string packagePath;
        public string packageHash;
        public DateTime importTime;
        public List<ImportedFileRecord> importedFiles;
        public bool isActive;
        public string importSource;

        public PackageImportRecord(string name, string path, string hash, string source = "Manual")
        {
            packageName = name;
            packagePath = path;
            packageHash = hash;
            importTime = DateTime.Now;
            importedFiles = new List<ImportedFileRecord>();
            isActive = true;
            importSource = source;
        }
    }

    [System.Serializable]
    public class ImportedFileRecord
    {
        public string relativePath;
        public string fullPath;
        public bool isDirectory;
        public long fileSize;
        public DateTime importTime;
        public bool stillExists;
        public string fileHash;

        public ImportedFileRecord(string relPath, string fullPath, bool isDir, long size, string hash = "")
        {
            relativePath = relPath;
            this.fullPath = fullPath;
            isDirectory = isDir;
            fileSize = size;
            importTime = DateTime.Now;
            stillExists = File.Exists(fullPath) || Directory.Exists(fullPath);
            fileHash = hash;
        }
    }

    public class PackageTrackerWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private string searchQuery = "";
        private bool showInactivePackages = true;
        private bool showFileDetails = true;
        private Dictionary<string, bool> packageFoldStates = new Dictionary<string, bool>();
        private Dictionary<string, Dictionary<string, bool>> fileFoldStates = new Dictionary<string, Dictionary<string, bool>>();
        private Dictionary<string, bool> selectedFiles = new Dictionary<string, bool>();

        private static List<PackageImportRecord> importRecords = new List<PackageImportRecord>();
        private static string trackingDataPath;
        private static Dictionary<string, HashSet<string>> beforeImportFiles = new Dictionary<string, HashSet<string>>();

        private const float ITEM_HEIGHT = 20f;
        private const float INDENT_WIDTH = 20f;
        private const float BUTTON_WIDTH = 80f;
        private const float SMALL_BUTTON_WIDTH = 60f;

        [MenuItem("VRChat SDK/SDKTools/Package Tracker")]
        public static void ShowWindow()
        {
            var window = GetWindow<PackageTrackerWindow>("Package Tracker");
            window.minSize = new Vector2(650, 400);
            window.Show();
        }

        private void OnEnable()
        {
            InitializeTracker();
            LoadTrackingData();
            UpdateAllFileStatus();

            AssetDatabase.importPackageStarted += OnImportStarted;
            AssetDatabase.importPackageCompleted += OnImportCompleted;
            AssetDatabase.importPackageFailed += OnImportFailed;
        }

        private void OnDisable()
        {
            AssetDatabase.importPackageStarted -= OnImportStarted;
            AssetDatabase.importPackageCompleted -= OnImportCompleted;
            AssetDatabase.importPackageFailed -= OnImportFailed;
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawToolbar();
            DrawPackageList();
            DrawFooter();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(10);

            var titleStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
            EditorGUILayout.LabelField("📦 Package Import Tracker", titleStyle);

            var subtitleStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Italic
            };
            EditorGUILayout.LabelField("Track, manage, and remove imported Unity packages", subtitleStyle);

            EditorGUILayout.Space(10);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("🔍", GUILayout.Width(20));
            searchQuery = EditorGUILayout.TextField(searchQuery, EditorStyles.toolbarSearchField, GUILayout.Width(200));

            if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(20)))
            {
                searchQuery = "";
                GUI.FocusControl(null);
            }

            GUILayout.Space(10);

            showInactivePackages = GUILayout.Toggle(showInactivePackages, "Show Removed", EditorStyles.toolbarButton);
            showFileDetails = GUILayout.Toggle(showFileDetails, "File Details", EditorStyles.toolbarButton);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(new GUIContent("🔄 Refresh"), EditorStyles.toolbarButton, GUILayout.Width(BUTTON_WIDTH + 10)))
            {
                UpdateAllFileStatus();
            }

            if (GUILayout.Button(new GUIContent("📁 Track Package"), EditorStyles.toolbarButton, GUILayout.Width(110)))
            {
                TrackExistingPackage();
            }

            if (GUILayout.Button(new GUIContent("🗑️ Clear History"), EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                ClearTrackingHistory();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawPackageList()
        {
            if (importRecords == null || importRecords.Count == 0)
            {
                EditorGUILayout.Space(50);
                var emptyStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    fontSize = 14
                };
                EditorGUILayout.LabelField("No packages tracked yet", emptyStyle);
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Import a .unitypackage to start tracking", emptyStyle);
                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            var filteredRecords = importRecords
                .Where(record =>
                    (showInactivePackages || record.isActive) &&
                    (string.IsNullOrEmpty(searchQuery) ||
                     record.packageName.ToLower().Contains(searchQuery.ToLower())))
                .OrderByDescending(r => r.importTime)
                .ToList();

            foreach (var record in filteredRecords)
            {
                DrawPackageRecord(record);
                EditorGUILayout.Space(5);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawPackageRecord(PackageImportRecord record)
        {
            var backgroundColor = record.isActive ? new Color(0.2f, 0.4f, 0.2f, 0.3f) : new Color(0.4f, 0.2f, 0.2f, 0.3f);
            var rect = GUILayoutUtility.GetRect(0, GetPackageRecordHeight(record), GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, backgroundColor);

            GUILayout.Space(-GetPackageRecordHeight(record));

            EditorGUILayout.BeginVertical(GUILayout.Height(GetPackageRecordHeight(record)));

            EditorGUILayout.BeginHorizontal(GUILayout.Height(ITEM_HEIGHT + 5));

            string packageKey = record.packageName + record.importTime.Ticks;
            if (!packageFoldStates.ContainsKey(packageKey))
                packageFoldStates[packageKey] = false;

            var icon = record.isActive ? "📦" : "📦💀";
            var foldoutLabel = $"{icon} {record.packageName} ({record.importedFiles.Count} files)";

            packageFoldStates[packageKey] = EditorGUILayout.Foldout(packageFoldStates[packageKey], foldoutLabel, true);

            GUILayout.FlexibleSpace();

            var timeText = record.importTime.ToString("MMM dd, yyyy HH:mm");
            EditorGUILayout.LabelField(timeText, EditorStyles.miniLabel, GUILayout.Width(120));

            EditorGUILayout.LabelField(record.importSource, EditorStyles.miniLabel, GUILayout.Width(80));

            if (record.isActive)
            {
                if (GUILayout.Button(new GUIContent("🗑️ Remove"), EditorStyles.miniButton, GUILayout.Width(SMALL_BUTTON_WIDTH + 15)))
                {
                    RemovePackageFiles(record);
                }
            }
            else
            {
                GUI.enabled = !string.IsNullOrEmpty(record.packagePath) && File.Exists(record.packagePath);
                if (GUILayout.Button(new GUIContent("↻ Reimport"), EditorStyles.miniButton, GUILayout.Width(SMALL_BUTTON_WIDTH + 15)))
                {
                    ReimportPackage(record);
                }
                GUI.enabled = true;

                if (string.IsNullOrEmpty(record.packagePath) || !File.Exists(record.packagePath))
                {
                    if (GUILayout.Button(new GUIContent("📁 Locate"), EditorStyles.miniButton, GUILayout.Width(55)))
                    {
                        FindAndSetPackagePath(record);
                    }
                }
            }

            if (GUILayout.Button("⋮", EditorStyles.miniButton, GUILayout.Width(20)))
            {
                ShowPackageContextMenu(record);
            }

            EditorGUILayout.EndHorizontal();

            if (packageFoldStates[packageKey])
            {
                EditorGUI.indentLevel++;
                DrawPackageDetails(record);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPackageDetails(PackageImportRecord record)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Path:", GUILayout.Width(40));

            if (string.IsNullOrEmpty(record.packagePath))
            {
                EditorGUILayout.LabelField("Package file not tracked", EditorStyles.miniLabel);
                if (GUILayout.Button(new GUIContent("📁 Find"), EditorStyles.miniButton, GUILayout.Width(50)))
                {
                    FindAndSetPackagePath(record);
                }
            }
            else
            {
                EditorGUILayout.LabelField(record.packagePath, EditorStyles.miniLabel);
                if (GUILayout.Button(new GUIContent("📁 Show"), EditorStyles.miniButton, GUILayout.Width(50)))
                {
                    if (File.Exists(record.packagePath))
                        EditorUtility.RevealInFinder(record.packagePath);
                    else
                    {
                        if (EditorUtility.DisplayDialog("File Not Found",
                            $"Package file not found at:\n{record.packagePath}\n\nWould you like to locate it manually?",
                            "Locate", "Cancel"))
                        {
                            FindAndSetPackagePath(record);
                        }
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Source:", GUILayout.Width(40));
            EditorGUILayout.LabelField(record.importSource, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            if (showFileDetails && record.importedFiles.Count > 0)
            {
                EditorGUILayout.Space(5);
                DrawFileTree(record);
            }
        }

        private void FindAndSetPackagePath(PackageImportRecord record)
        {
            string packagePath = EditorUtility.OpenFilePanel("Locate Package File", "", "unitypackage");
            if (!string.IsNullOrEmpty(packagePath))
            {
                record.packagePath = packagePath;
                SaveTrackingData();
                EditorUtility.DisplayDialog("Success", "Package path updated!", "OK");
            }
        }

        private void DrawFileTree(PackageImportRecord record)
        {
            var filesByDirectory = GroupFilesByDirectory(record.importedFiles);

            foreach (var dirGroup in filesByDirectory.OrderBy(kvp => kvp.Key))
            {
                DrawDirectoryGroup(record, dirGroup.Key, dirGroup.Value);
            }
        }

        private void DrawDirectoryGroup(PackageImportRecord record, string directory, List<ImportedFileRecord> files)
        {
            string dirKey = record.packageName + directory;

            if (!fileFoldStates.ContainsKey(record.packageName))
                fileFoldStates[record.packageName] = new Dictionary<string, bool>();

            if (!fileFoldStates[record.packageName].ContainsKey(dirKey))
                fileFoldStates[record.packageName][dirKey] = false;

            EditorGUILayout.BeginHorizontal();

            var existingCount = files.Count(f => f.stillExists);
            string dirIcon;
            if (existingCount == files.Count)
                dirIcon = "📁";
            else if (existingCount > 0)
                dirIcon = "📁⚠️";
            else
                dirIcon = "📁💀";

            var dirLabel = $"{dirIcon} {directory} ({existingCount}/{files.Count})";

            fileFoldStates[record.packageName][dirKey] = EditorGUILayout.Foldout(
                fileFoldStates[record.packageName][dirKey], dirLabel, true);

            GUILayout.FlexibleSpace();

            if (existingCount > 0 && GUILayout.Button("🗑️", EditorStyles.miniButton, GUILayout.Width(25)))
            {
                RemoveDirectoryFiles(record, directory, files);
            }

            EditorGUILayout.EndHorizontal();

            if (fileFoldStates[record.packageName][dirKey])
            {
                EditorGUI.indentLevel++;
                foreach (var file in files.OrderBy(f => f.relativePath))
                {
                    DrawFileItem(record, file);
                }
                EditorGUI.indentLevel--;
            }
        }

        private void DrawFileItem(PackageImportRecord record, ImportedFileRecord file)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.Height(18));

            string fileKey = record.packageName + file.fullPath;
            if (!selectedFiles.ContainsKey(fileKey))
                selectedFiles[fileKey] = false;

            if (file.stillExists)
            {
                selectedFiles[fileKey] = EditorGUILayout.Toggle(selectedFiles[fileKey], GUILayout.Width(15));
            }
            else
            {
                GUILayout.Space(15);
            }

            string fileIcon;
            if (file.isDirectory)
            {
                fileIcon = file.stillExists ? "📁" : "📁💀";
            }
            else
            {
                fileIcon = file.stillExists ? "📄" : "📄💀";
            }

            var iconStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label(fileIcon, iconStyle, GUILayout.Width(25));

            var fileName = Path.GetFileName(file.relativePath);
            if (string.IsNullOrEmpty(fileName)) fileName = file.relativePath;

            var labelStyle = file.stillExists ? EditorStyles.miniLabel : EditorStyles.centeredGreyMiniLabel;
            EditorGUILayout.LabelField(fileName, labelStyle);

            GUILayout.FlexibleSpace();

            if (!file.isDirectory && file.stillExists)
            {
                EditorGUILayout.LabelField(FormatFileSize(file.fileSize), EditorStyles.miniLabel, GUILayout.Width(60));
            }

            if (file.stillExists && GUILayout.Button("🗑️", EditorStyles.miniButton, GUILayout.Width(20)))
            {
                RemoveSingleFile(record, file);
            }

            EditorGUILayout.EndHorizontal();
        }

        private float GetPackageRecordHeight(PackageImportRecord record)
        {
            float height = ITEM_HEIGHT + 5;

            string packageKey = record.packageName + record.importTime.Ticks;
            if (packageFoldStates.ContainsKey(packageKey) && packageFoldStates[packageKey])
            {
                height += 25;

                if (showFileDetails && record.importedFiles.Count > 0)
                {
                    var filesByDirectory = GroupFilesByDirectory(record.importedFiles);
                    height += filesByDirectory.Count * 20;

                    foreach (var dirGroup in filesByDirectory)
                    {
                        string dirKey = record.packageName + dirGroup.Key;
                        if (fileFoldStates.ContainsKey(record.packageName) &&
                            fileFoldStates[record.packageName].ContainsKey(dirKey) &&
                            fileFoldStates[record.packageName][dirKey])
                        {
                            height += dirGroup.Value.Count * 18;
                        }
                    }
                }
            }

            return height;
        }

        private Dictionary<string, List<ImportedFileRecord>> GroupFilesByDirectory(List<ImportedFileRecord> files)
        {
            var groups = new Dictionary<string, List<ImportedFileRecord>>();

            foreach (var file in files)
            {
                var directory = Path.GetDirectoryName(file.relativePath)?.Replace('\\', '/') ?? "";
                if (string.IsNullOrEmpty(directory)) directory = "Root";

                if (!groups.ContainsKey(directory))
                    groups[directory] = new List<ImportedFileRecord>();

                groups[directory].Add(file);
            }

            return groups;
        }

        private void DrawFooter()
        {
            EditorGUILayout.Space(5);

            var separatorRect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(separatorRect, Color.gray);

            EditorGUILayout.BeginHorizontal();

            var totalPackages = importRecords.Count;
            var activePackages = importRecords.Count(r => r.isActive);
            var totalFiles = importRecords.Sum(r => r.importedFiles.Count);
            var activeFiles = importRecords.Sum(r => r.importedFiles.Count(f => f.stillExists));

            EditorGUILayout.LabelField("📊 Packages: " + $"{activePackages}/{totalPackages} active", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("📄 Files: " + $"{activeFiles}/{totalFiles} exist", EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        #region Tracking Logic

        private static void InitializeTracker()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            trackingDataPath = Path.Combine(appDataPath, "SDKTools/package_tracking.json");
        }

        public static void StartTrackingImport(string packagePath)
        {
            string key = Path.GetFileName(packagePath) + "_" + DateTime.Now.Ticks;
            beforeImportFiles[key] = GetAllProjectFiles();
            Debug.Log($"[PackageTracker] Started tracking import for: {Path.GetFileName(packagePath)} with key: {key}");
        }

        private static void OnImportStarted(string packageName)
        {
            Debug.Log($"[PackageTracker] Import started: {packageName}");
            string key = packageName + "_" + DateTime.Now.Ticks;
            if (!beforeImportFiles.ContainsKey(key))
            {
                beforeImportFiles[key] = GetAllProjectFiles();
                Debug.Log($"[PackageTracker] Created tracking key for import: {key}");
            }
        }

        private static void OnImportCompleted(string packageName)
        {
            Debug.Log($"[PackageTracker] Import completed: {packageName}");

            EditorApplication.delayCall += () => {
                var matchingKey = beforeImportFiles.Keys.FirstOrDefault(k => k.StartsWith(packageName) || k.Contains(packageName));
                if (matchingKey != null)
                {
                    string packagePath = FindPackageFile(packageName);
                    CompleteTracking(packageName, packagePath, matchingKey);
                }
                else
                {
                    Debug.Log($"[PackageTracker] No before-state found for {packageName}, creating fallback tracking");
                    string packagePath = FindPackageFile(packageName);
                    CreateFallbackTracking(packageName, packagePath);
                }
            };
        }

        private static string FindPackageFile(string packageName)
        {
            string[] searchPaths = {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Downloads"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SDKTools")
            };

            foreach (var searchPath in searchPaths)
            {
                if (!Directory.Exists(searchPath)) continue;

                try
                {
                    var packageFiles = Directory.GetFiles(searchPath, "*.unitypackage", SearchOption.TopDirectoryOnly);
                    var matchingFile = packageFiles.FirstOrDefault(f =>
                        Path.GetFileNameWithoutExtension(f).Equals(packageName, StringComparison.OrdinalIgnoreCase));

                    if (!string.IsNullOrEmpty(matchingFile))
                    {
                        Debug.Log($"[PackageTracker] Found package file: {matchingFile}");
                        return matchingFile;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error searching for package in {searchPath}: {e.Message}");
                }
            }

            Debug.Log($"[PackageTracker] Could not find package file for: {packageName}");
            return "";
        }

        private static void OnImportFailed(string packageName, string errorMessage)
        {
            Debug.LogError($"[PackageTracker] Import failed: {packageName} - {errorMessage}");
            var matchingKey = beforeImportFiles.Keys.FirstOrDefault(k => k.StartsWith(packageName));
            if (matchingKey != null)
            {
                beforeImportFiles.Remove(matchingKey);
            }
        }

        public static void CompleteTracking(string packageName, string packagePath, string key)
        {
            if (!beforeImportFiles.ContainsKey(key))
            {
                Debug.LogWarning($"[PackageTracker] No before-state found for key: {key}");
                return;
            }

            var beforeFiles = beforeImportFiles[key];
            var afterFiles = GetAllProjectFiles();
            var newFiles = afterFiles.Except(beforeFiles).ToList();

            Debug.Log($"[PackageTracker] Found {newFiles.Count} new files for package: {packageName}");

            if (newFiles.Count > 0)
            {
                string hash = !string.IsNullOrEmpty(packagePath) ? CalculatePackageHash(packagePath) : "";
                var record = new PackageImportRecord(packageName, packagePath, hash, "Unity Import");

                foreach (var filePath in newFiles)
                {
                    try
                    {
                        if (File.Exists(filePath))
                        {
                            var fileInfo = new FileInfo(filePath);
                            string relPath = GetRelativePath(filePath);
                            string fileHash = CalculateFileHash(filePath);
                            record.importedFiles.Add(new ImportedFileRecord(relPath, filePath, false, fileInfo.Length, fileHash));
                        }
                        else if (Directory.Exists(filePath))
                        {
                            string relPath = GetRelativePath(filePath);
                            record.importedFiles.Add(new ImportedFileRecord(relPath, filePath, true, 0));
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error tracking file {filePath}: {e.Message}");
                    }
                }

                importRecords.RemoveAll(r => r.packageName == packageName);
                importRecords.Add(record);

                UpdateRecordFileStatus(record);

                SaveTrackingData();
                Debug.Log($"[PackageTracker] Successfully tracked {record.importedFiles.Count} files for package: {packageName}");
            }
            else
            {
                Debug.LogWarning($"[PackageTracker] No new files detected for package: {packageName}");
                CreateFallbackTracking(packageName);
            }

            beforeImportFiles.Remove(key);
        }

        private static void CreateFallbackTracking(string packageName, string packagePath = "")
        {
            var record = new PackageImportRecord(packageName, packagePath, "", "Unity Import (Auto-detected)");

            var recentFiles = GetRecentlyModifiedFiles(TimeSpan.FromMinutes(2));

            foreach (var filePath in recentFiles)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        var fileInfo = new FileInfo(filePath);
                        string relPath = GetRelativePath(filePath);
                        string fileHash = CalculateFileHash(filePath);
                        record.importedFiles.Add(new ImportedFileRecord(relPath, filePath, false, fileInfo.Length, fileHash));
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error adding recent file {filePath}: {e.Message}");
                }
            }

            importRecords.RemoveAll(r => r.packageName == packageName);
            importRecords.Add(record);
            SaveTrackingData();

            Debug.Log($"[PackageTracker] Created fallback tracking for {packageName} with {record.importedFiles.Count} recent files");
        }

        private static List<string> GetRecentlyModifiedFiles(TimeSpan timeSpan)
        {
            var recentFiles = new List<string>();
            var cutoffTime = DateTime.Now - timeSpan;

            try
            {
                if (Directory.Exists("Assets"))
                {
                    AddRecentFilesFromDirectory(recentFiles, "Assets", cutoffTime);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error getting recent files: {e.Message}");
            }

            return recentFiles;
        }

        private static void AddRecentFilesFromDirectory(List<string> recentFiles, string directory, DateTime cutoffTime)
        {
            try
            {
                foreach (var file in Directory.GetFiles(directory))
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTime > cutoffTime || fileInfo.CreationTime > cutoffTime)
                    {
                        recentFiles.Add(file.Replace('\\', '/'));
                    }
                }

                foreach (var subDir in Directory.GetDirectories(directory))
                {
                    var dirInfo = new DirectoryInfo(subDir);
                    if (dirInfo.LastWriteTime > cutoffTime || dirInfo.CreationTime > cutoffTime)
                    {
                        recentFiles.Add(subDir.Replace('\\', '/'));
                        AddRecentFilesFromDirectory(recentFiles, subDir, cutoffTime);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error scanning directory {directory}: {e.Message}");
            }
        }

        private static void UpdateRecordFileStatus(PackageImportRecord record)
        {
            bool hasExistingFiles = false;

            foreach (var file in record.importedFiles)
            {
                bool exists = File.Exists(file.fullPath) || Directory.Exists(file.fullPath);
                file.stillExists = exists;
                if (exists) hasExistingFiles = true;
            }

            record.isActive = hasExistingFiles;
            Debug.Log($"[PackageTracker] Record {record.packageName} status: {record.isActive} (has {record.importedFiles.Count(f => f.stillExists)} existing files)");
        }

        #endregion

        #region File Operations

        private void RemovePackageFiles(PackageImportRecord record)
        {
            if (EditorUtility.DisplayDialog("Remove Package Files",
                $"Remove all {record.importedFiles.Count(f => f.stillExists)} files imported by '{record.packageName}'?\n\nThis action cannot be undone.",
                "Remove All", "Cancel"))
            {
                int removedCount = 0;
                var filesToRemove = record.importedFiles.Where(f => f.stillExists).ToList();

                filesToRemove = filesToRemove.OrderByDescending(f => f.fullPath.Split('/').Length).ToList();

                foreach (var file in filesToRemove)
                {
                    try
                    {
                        if (File.Exists(file.fullPath))
                        {
                            File.Delete(file.fullPath);
                            file.stillExists = false;
                            removedCount++;
                        }
                        else if (Directory.Exists(file.fullPath))
                        {
                            if (Directory.GetFileSystemEntries(file.fullPath).Length == 0)
                            {
                                Directory.Delete(file.fullPath);
                                file.stillExists = false;
                                removedCount++;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Failed to remove {file.fullPath}: {e.Message}");
                    }
                }

                record.isActive = false;
                SaveTrackingData();
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog("Removal Complete", $"Removed {removedCount} files.", "OK");
            }
        }

        private void RemoveDirectoryFiles(PackageImportRecord record, string directory, List<ImportedFileRecord> files)
        {
            var existingFiles = files.Where(f => f.stillExists).ToList();
            if (existingFiles.Count == 0) return;

            if (EditorUtility.DisplayDialog("Remove Directory Files",
                $"Remove {existingFiles.Count} files from directory '{directory}'?",
                "Remove", "Cancel"))
            {
                int removedCount = 0;
                foreach (var file in existingFiles.OrderByDescending(f => f.fullPath.Split('/').Length))
                {
                    try
                    {
                        if (File.Exists(file.fullPath))
                        {
                            File.Delete(file.fullPath);
                            file.stillExists = false;
                            removedCount++;
                        }
                        else if (Directory.Exists(file.fullPath) && Directory.GetFileSystemEntries(file.fullPath).Length == 0)
                        {
                            Directory.Delete(file.fullPath);
                            file.stillExists = false;
                            removedCount++;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Failed to remove {file.fullPath}: {e.Message}");
                    }
                }

                if (record.importedFiles.All(f => !f.stillExists))
                    record.isActive = false;

                SaveTrackingData();
                AssetDatabase.Refresh();
                Debug.Log($"Removed {removedCount} files from {directory}");
            }
        }

        private void RemoveSingleFile(PackageImportRecord record, ImportedFileRecord file)
        {
            try
            {
                if (File.Exists(file.fullPath))
                {
                    File.Delete(file.fullPath);
                    file.stillExists = false;
                    AssetDatabase.Refresh();

                    if (record.importedFiles.All(f => !f.stillExists))
                        record.isActive = false;

                    SaveTrackingData();
                    Debug.Log($"Removed file: {file.relativePath}");
                }
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to remove file: {e.Message}", "OK");
            }
        }

        private void ReimportPackage(PackageImportRecord record)
        {
            if (string.IsNullOrEmpty(record.packagePath) || !File.Exists(record.packagePath))
            {
                if (EditorUtility.DisplayDialog("Package File Not Found",
                    "The original package file is not available. Would you like to locate it?",
                    "Locate", "Cancel"))
                {
                    LocateAndReimportPackage(record);
                }
                return;
            }

            if (EditorUtility.DisplayDialog("Reimport Package",
                $"Reimport package '{record.packageName}' from:\n{record.packagePath}",
                "Reimport", "Cancel"))
            {
                StartTrackingImport(record.packagePath);
                AssetDatabase.ImportPackage(record.packagePath, true);

                record.isActive = true;
                record.importTime = DateTime.Now;
                SaveTrackingData();
            }
        }

        #endregion

        #region Context Menus and Dialogs

        private void ShowPackageContextMenu(PackageImportRecord record)
        {
            GenericMenu menu = new GenericMenu();

            if (record.isActive)
            {
                menu.AddItem(new GUIContent("🗑️ Remove All Files"), false, () => RemovePackageFiles(record));
                menu.AddItem(new GUIContent("📤 Remove Selected Files"), false, () => RemoveSelectedFiles(record));
            }
            else
            {
                if (!string.IsNullOrEmpty(record.packagePath) && File.Exists(record.packagePath))
                {
                    menu.AddItem(new GUIContent("↻ Reimport Package"), false, () => ReimportPackage(record));
                }
                else
                {
                    menu.AddItem(new GUIContent("📁 Locate & Reimport Package"), false, () => LocateAndReimportPackage(record));
                }
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("📋 Copy Package Info"), false, () => CopyPackageInfo(record));

            if (!string.IsNullOrEmpty(record.packagePath) && File.Exists(record.packagePath))
            {
                menu.AddItem(new GUIContent("📁 Show Package File"), false, () => {
                    EditorUtility.RevealInFinder(record.packagePath);
                });
            }
            else
            {
                menu.AddItem(new GUIContent("📁 Locate Package File"), false, () => FindAndSetPackagePath(record));
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("📂 Show Imported Files Folder"), false, () => {
                if (record.importedFiles.Count > 0)
                {
                    var firstFile = record.importedFiles.First(f => f.stillExists);
                    if (firstFile != null)
                    {
                        EditorUtility.RevealInFinder(firstFile.fullPath);
                    }
                }
                else
                {
                    EditorUtility.RevealInFinder("Assets");
                }
            });

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("🗑️ Remove from History"), false, () => RemoveFromHistory(record));

            menu.ShowAsContext();
        }

        private void LocateAndReimportPackage(PackageImportRecord record)
        {
            string packagePath = EditorUtility.OpenFilePanel("Locate Package File to Reimport", "", "unitypackage");
            if (!string.IsNullOrEmpty(packagePath))
            {
                record.packagePath = packagePath;
                SaveTrackingData();
                ReimportPackage(record);
            }
        }

        private void RemoveSelectedFiles(PackageImportRecord record)
        {
            var selectedFilePaths = selectedFiles
                .Where(kvp => kvp.Value && kvp.Key.StartsWith(record.packageName))
                .Select(kvp => kvp.Key.Substring(record.packageName.Length))
                .ToList();

            if (selectedFilePaths.Count == 0)
            {
                EditorUtility.DisplayDialog("No Selection", "Please select files to remove first.", "OK");
                return;
            }

            if (EditorUtility.DisplayDialog("Remove Selected Files",
                $"Remove {selectedFilePaths.Count} selected files?", "Remove", "Cancel"))
            {
                var filesToRemove = record.importedFiles
                    .Where(f => selectedFilePaths.Contains(f.fullPath) && f.stillExists)
                    .ToList();

                int removedCount = 0;
                foreach (var file in filesToRemove.OrderByDescending(f => f.fullPath.Split('/').Length))
                {
                    try
                    {
                        if (File.Exists(file.fullPath))
                        {
                            File.Delete(file.fullPath);
                            file.stillExists = false;
                            removedCount++;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Failed to remove {file.fullPath}: {e.Message}");
                    }
                }

                if (record.importedFiles.All(f => !f.stillExists))
                    record.isActive = false;

                SaveTrackingData();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Removal Complete", $"Removed {removedCount} files.", "OK");
            }
        }

        private void CopyPackageInfo(PackageImportRecord record)
        {
            var info = $"Package: {record.packageName}\n" +
                      $"Import Time: {record.importTime}\n" +
                      $"Source: {record.importSource}\n" +
                      $"Files: {record.importedFiles.Count}\n" +
                      $"Active: {record.isActive}\n" +
                      $"Path: {record.packagePath}";

            EditorGUIUtility.systemCopyBuffer = info;
            Debug.Log("Package info copied to clipboard");
        }

        private void RemoveFromHistory(PackageImportRecord record)
        {
            if (EditorUtility.DisplayDialog("Remove from History",
                $"Remove '{record.packageName}' from tracking history?\n\nThis will not delete any files.",
                "Remove", "Cancel"))
            {
                importRecords.Remove(record);
                SaveTrackingData();
            }
        }

        private void TrackExistingPackage()
        {
            string packagePath = EditorUtility.OpenFilePanel("Select Unity Package to Track", "", "unitypackage");
            if (!string.IsNullOrEmpty(packagePath))
            {
                string packageName = Path.GetFileNameWithoutExtension(packagePath);
                if (importRecords.Any(r => r.packageName == packageName))
                {
                    if (!EditorUtility.DisplayDialog("Package Already Tracked",
                        $"Package '{packageName}' is already being tracked. Import anyway?",
                        "Import", "Cancel"))
                        return;
                }

                StartTrackingImport(packagePath);
                AssetDatabase.ImportPackage(packagePath, true);
            }
        }

        private void ClearTrackingHistory()
        {
            if (EditorUtility.DisplayDialog("Clear Tracking History",
                "Remove all package tracking history?\n\nThis will not delete any files, only the tracking data.",
                "Clear", "Cancel"))
            {
                importRecords.Clear();
                packageFoldStates.Clear();
                fileFoldStates.Clear();
                selectedFiles.Clear();
                SaveTrackingData();
                Debug.Log("[PackageTracker] Cleared all tracking history");
            }
        }

        #endregion

        #region Utility Methods

        private void UpdateAllFileStatus()
        {
            bool anyChanges = false;

            foreach (var record in importRecords)
            {
                int existingCount = 0;

                foreach (var file in record.importedFiles)
                {
                    bool wasExists = file.stillExists;
                    bool nowExists = File.Exists(file.fullPath) || Directory.Exists(file.fullPath);
                    file.stillExists = nowExists;

                    if (nowExists) existingCount++;
                    if (wasExists != nowExists) anyChanges = true;
                }

                bool wasActive = record.isActive;
                record.isActive = existingCount > 0;

                if (wasActive != record.isActive) anyChanges = true;

                Debug.Log($"[PackageTracker] Package {record.packageName}: {existingCount}/{record.importedFiles.Count} files exist, Active: {record.isActive}");
            }

            if (anyChanges)
            {
                SaveTrackingData();
            }

            Repaint();
            Debug.Log($"[PackageTracker] Updated status for {importRecords.Count} packages");
        }

        private static HashSet<string> GetAllProjectFiles()
        {
            var files = new HashSet<string>();

            if (Directory.Exists("Assets"))
            {
                AddDirectoryFiles(files, "Assets");
            }

            string[] otherDirs = { "Packages", "ProjectSettings", "UserSettings" };
            foreach (var dir in otherDirs)
            {
                if (Directory.Exists(dir))
                {
                    AddDirectoryFiles(files, dir);
                }
            }

            return files;
        }

        private static void AddDirectoryFiles(HashSet<string> files, string directory)
        {
            try
            {
                files.Add(directory);

                foreach (var file in Directory.GetFiles(directory))
                {
                    files.Add(file.Replace('\\', '/'));
                }

                foreach (var subDir in Directory.GetDirectories(directory))
                {
                    AddDirectoryFiles(files, subDir.Replace('\\', '/'));
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error scanning directory {directory}: {e.Message}");
            }
        }

        private static string GetRelativePath(string fullPath)
        {
            string projectPath = Directory.GetCurrentDirectory().Replace('\\', '/');
            fullPath = fullPath.Replace('\\', '/');

            if (fullPath.StartsWith(projectPath + "/"))
            {
                return fullPath.Substring(projectPath.Length + 1);
            }
            return fullPath;
        }

        private static string CalculatePackageHash(string packagePath)
        {
            if (!File.Exists(packagePath)) return "";

            try
            {
                using (var md5 = MD5.Create())
                {
                    using (var stream = File.OpenRead(packagePath))
                    {
                        var hash = md5.ComputeHash(stream);
                        return Convert.ToBase64String(hash);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to calculate hash for {packagePath}: {e.Message}");
                return "";
            }
        }

        private static string CalculateFileHash(string filePath)
        {
            if (!File.Exists(filePath)) return "";

            try
            {
                using (var md5 = MD5.Create())
                {
                    using (var stream = File.OpenRead(filePath))
                    {
                        var hash = md5.ComputeHash(stream);
                        return Convert.ToBase64String(hash).Substring(0, 8);
                    }
                }
            }
            catch
            {
                return "";
            }
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024 * 1024):F1} MB";
            return $"{bytes / (1024 * 1024 * 1024):F1} GB";
        }

        private static void LoadTrackingData()
        {
            if (File.Exists(trackingDataPath))
            {
                try
                {
                    string json = File.ReadAllText(trackingDataPath);
                    importRecords = JsonConvert.DeserializeObject<List<PackageImportRecord>>(json) ?? new List<PackageImportRecord>();
                    Debug.Log($"[PackageTracker] Loaded {importRecords.Count} package records");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[PackageTracker] Failed to load tracking data: {e.Message}");
                    importRecords = new List<PackageImportRecord>();
                }
            }
            else
            {
                importRecords = new List<PackageImportRecord>();
            }
        }

        private static void SaveTrackingData()
        {
            try
            {
                string directory = Path.GetDirectoryName(trackingDataPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonConvert.SerializeObject(importRecords, Formatting.Indented);
                File.WriteAllText(trackingDataPath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[PackageTracker] Failed to save tracking data: {e.Message}");
            }
        }

        #endregion

        #region Public API for Integration

        /// <summary>
        /// Call this before importing a package to start tracking
        /// </summary>
        /// <param name="packagePath">Path to the .unitypackage file</param>
        public static void TrackPackageImport(string packagePath)
        {
            InitializeTracker();
            LoadTrackingData();
            StartTrackingImport(packagePath);
        }

        /// <summary>
        /// Call this after import is complete with the package name that was imported
        /// </summary>
        /// <param name="packageName">Name of the imported package</param>
        /// <param name="packagePath">Path to the original package file</param>
        public static void CompletePackageTracking(string packageName, string packagePath)
        {
            InitializeTracker();
            LoadTrackingData();

            var matchingKey = beforeImportFiles.Keys.FirstOrDefault(k =>
                k.Contains(Path.GetFileNameWithoutExtension(packagePath)) ||
                k.StartsWith(packageName));

            if (matchingKey != null)
            {
                CompleteTracking(packageName, packagePath, matchingKey);
            }
            else
            {
                Debug.LogWarning($"[PackageTracker] No matching key found for {packageName}, creating fallback tracking");
                CreateFallbackTracking(packageName);
            }
        }

        /// <summary>
        /// Force refresh file status for all tracked packages
        /// </summary>
        public static void RefreshAllPackageStatus()
        {
            InitializeTracker();
            LoadTrackingData();

            foreach (var record in importRecords)
            {
                UpdateRecordFileStatus(record);
            }

            SaveTrackingData();
            Debug.Log("[PackageTracker] Refreshed status for all packages");
        }

        /// <summary>
        /// Get all tracked packages
        /// </summary>
        /// <returns>List of tracked package records</returns>
        public static List<PackageImportRecord> GetTrackedPackages()
        {
            InitializeTracker();
            LoadTrackingData();
            return new List<PackageImportRecord>(importRecords);
        }

        /// <summary>
        /// Remove files for a specific package
        /// </summary>
        /// <param name="packageName">Name of the package to remove</param>
        /// <returns>True if files were removed</returns>
        public static bool RemovePackage(string packageName)
        {
            InitializeTracker();
            LoadTrackingData();

            var record = importRecords.FirstOrDefault(r => r.packageName == packageName);
            if (record == null) return false;

            int removedCount = 0;
            var filesToRemove = record.importedFiles.Where(f => f.stillExists)
                .OrderByDescending(f => f.fullPath.Split('/').Length).ToList();

            foreach (var file in filesToRemove)
            {
                try
                {
                    if (File.Exists(file.fullPath))
                    {
                        File.Delete(file.fullPath);
                        file.stillExists = false;
                        removedCount++;
                    }
                    else if (Directory.Exists(file.fullPath) && Directory.GetFileSystemEntries(file.fullPath).Length == 0)
                    {
                        Directory.Delete(file.fullPath);
                        file.stillExists = false;
                        removedCount++;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to remove {file.fullPath}: {e.Message}");
                }
            }

            record.isActive = false;
            SaveTrackingData();
            AssetDatabase.Refresh();

            Debug.Log($"[PackageTracker] Removed {removedCount} files for package: {packageName}");
            return removedCount > 0;
        }

        #endregion
    }

    public class PackageImportProcessor : AssetPostprocessor
    {
        private static bool isTrackingEnabled = true;

        public static void SetTrackingEnabled(bool enabled)
        {
            isTrackingEnabled = enabled;
        }

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (!isTrackingEnabled) return;

            if (importedAssets.Length > 0)
            {
                bool likelyPackageImport = importedAssets.Length > 5;

                if (likelyPackageImport)
                {
                    Debug.Log($"[PackageTracker] Detected potential package import: {importedAssets.Length} assets");
                }
            }
        }
    }
}
#endif