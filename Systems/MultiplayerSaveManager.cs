using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace OniMultiplayer.Systems
{
    /// <summary>
    /// Manages multiplayer save files in a separate folder from single-player saves.
    /// 
    /// Features:
    /// - Separate MP save folder (save_files_mp/)
    /// - Save file hash validation (ensures host/client have same save)
    /// - Metadata tracking (players, last session, etc.)
    /// - Import from single-player saves
    /// </summary>
    public class MultiplayerSaveManager
    {
        private static MultiplayerSaveManager _instance;
        public static MultiplayerSaveManager Instance => _instance ??= new MultiplayerSaveManager();

        // Folder name for MP saves (relative to base save folder)
        private const string MP_SAVE_FOLDER = "save_files_mp";
        private const string METADATA_EXTENSION = ".mp.json";

        private string _mpSavePath;
        private Dictionary<string, SaveMetadata> _metadataCache = new Dictionary<string, SaveMetadata>();

        /// <summary>
        /// Metadata stored alongside each MP save file.
        /// </summary>
        [System.Serializable]
        public class SaveMetadata
        {
            public string SaveName;
            public string Hash;
            public string LastPlayedStr; // Date as string for JSON compatibility
            public string HostSteamId;
            public string HostName;
            public List<string> Players = new List<string>();
            public int LastCycle;
            public int Version = 1;
            
            // Dupe assignments: list of "dupeName:playerId" strings
            public List<string> DupeAssignments = new List<string>();
        }

        /// <summary>
        /// Info about an MP save for UI display.
        /// </summary>
        public class MPSaveInfo
        {
            public string FileName { get; set; }
            public string FullPath { get; set; }
            public string Hash { get; set; }
            public SaveMetadata Metadata { get; set; }
            public System.DateTime LastModified { get; set; }
            public long FileSizeBytes { get; set; }
        }

        public MultiplayerSaveManager()
        {
            InitializeMPSaveFolder();
        }

        #region Folder Management

        /// <summary>
        /// Initialize the MP save folder.
        /// </summary>
        private void InitializeMPSaveFolder()
        {
            try
            {
                // Get base save folder from game
                string baseSaveFolder = GetBaseSaveFolder();
                
                if (string.IsNullOrEmpty(baseSaveFolder))
                {
                    OniMultiplayerMod.LogError("[MPSaveManager] Could not determine base save folder!");
                    return;
                }

                // Create MP subfolder
                _mpSavePath = Path.Combine(baseSaveFolder, MP_SAVE_FOLDER);
                
                if (!Directory.Exists(_mpSavePath))
                {
                    Directory.CreateDirectory(_mpSavePath);
                    OniMultiplayerMod.Log($"[MPSaveManager] Created MP save folder: {_mpSavePath}");
                }
                else
                {
                    OniMultiplayerMod.Log($"[MPSaveManager] MP save folder exists: {_mpSavePath}");
                }

                // Load metadata cache
                RefreshMetadataCache();
            }
            catch (Exception ex)
            {
                OniMultiplayerMod.LogError($"[MPSaveManager] Failed to initialize: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the base save folder path.
        /// Always use Documents/Klei/OxygenNotIncluded to avoid cloud_save_files issues.
        /// </summary>
        private string GetBaseSaveFolder()
        {
            // Use the standard ONI save location (not cloud)
            string docsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Klei", "OxygenNotIncluded");

            if (Directory.Exists(docsPath))
            {
                return docsPath;
            }

            // Create if doesn't exist
            try
            {
                Directory.CreateDirectory(docsPath);
                return docsPath;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the MP save folder path.
        /// </summary>
        public string GetMPSaveFolder()
        {
            if (string.IsNullOrEmpty(_mpSavePath))
            {
                InitializeMPSaveFolder();
            }
            return _mpSavePath;
        }

        #endregion

        #region Hash Generation

        /// <summary>
        /// Generate a SHA256 hash of a save file.
        /// </summary>
        public string GenerateHash(string filePath)
        {
            if (!File.Exists(filePath))
            {
                OniMultiplayerMod.LogWarning($"[MPSaveManager] Cannot hash, file not found: {filePath}");
                return null;
            }

            try
            {
                using (var sha256 = SHA256.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hashBytes = sha256.ComputeHash(stream);
                    return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                }
            }
            catch (Exception ex)
            {
                OniMultiplayerMod.LogError($"[MPSaveManager] Hash generation failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Generate a short hash (first 16 chars) for display.
        /// </summary>
        public string GenerateShortHash(string filePath)
        {
            string fullHash = GenerateHash(filePath);
            if (string.IsNullOrEmpty(fullHash)) return null;
            return fullHash.Substring(0, Math.Min(16, fullHash.Length));
        }

        #endregion

        #region Save Listing

        /// <summary>
        /// Get all MP saves.
        /// </summary>
        public List<MPSaveInfo> GetMPSaves()
        {
            var saves = new List<MPSaveInfo>();

            if (string.IsNullOrEmpty(_mpSavePath) || !Directory.Exists(_mpSavePath))
            {
                return saves;
            }

            try
            {
                var files = Directory.GetFiles(_mpSavePath, "*.sav", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    var info = new FileInfo(file);
                    string fileName = Path.GetFileName(file);

                    var saveInfo = new MPSaveInfo
                    {
                        FileName = fileName,
                        FullPath = file,
                        Hash = GenerateShortHash(file),
                        LastModified = info.LastWriteTime,
                        FileSizeBytes = info.Length,
                        Metadata = GetMetadata(fileName)
                    };

                    saves.Add(saveInfo);
                }

                // Sort by last modified (newest first)
                saves.Sort((a, b) => b.LastModified.CompareTo(a.LastModified));
            }
            catch (Exception ex)
            {
                OniMultiplayerMod.LogError($"[MPSaveManager] Failed to list saves: {ex.Message}");
            }

            return saves;
        }

        /// <summary>
        /// Find an MP save by filename.
        /// </summary>
        public string FindMPSave(string fileName)
        {
            if (string.IsNullOrEmpty(_mpSavePath) || string.IsNullOrEmpty(fileName))
            {
                return null;
            }

            string directPath = Path.Combine(_mpSavePath, fileName);
            if (File.Exists(directPath))
            {
                return directPath;
            }

            // Search subdirectories
            try
            {
                var files = Directory.GetFiles(_mpSavePath, fileName, SearchOption.AllDirectories);
                if (files.Length > 0)
                {
                    return files[0];
                }
            }
            catch { }

            return null;
        }

        #endregion

        #region Metadata Management

        /// <summary>
        /// Get metadata path for a save file.
        /// </summary>
        private string GetMetadataPath(string saveFileName)
        {
            if (string.IsNullOrEmpty(_mpSavePath)) return null;
            string baseName = Path.GetFileNameWithoutExtension(saveFileName);
            return Path.Combine(_mpSavePath, baseName + METADATA_EXTENSION);
        }

        /// <summary>
        /// Refresh the metadata cache.
        /// </summary>
        public void RefreshMetadataCache()
        {
            _metadataCache.Clear();

            if (string.IsNullOrEmpty(_mpSavePath) || !Directory.Exists(_mpSavePath))
            {
                return;
            }

            try
            {
                var metaFiles = Directory.GetFiles(_mpSavePath, "*" + METADATA_EXTENSION);
                foreach (var file in metaFiles)
                {
                    try
                    {
                        string content = File.ReadAllText(file);
                        var metadata = DeserializeMetadata(content);
                        if (metadata != null && !string.IsNullOrEmpty(metadata.SaveName))
                        {
                            _metadataCache[metadata.SaveName] = metadata;
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        /// <summary>
        /// Get metadata for a save file.
        /// </summary>
        public SaveMetadata GetMetadata(string saveFileName)
        {
            string baseName = Path.GetFileNameWithoutExtension(saveFileName);
            
            if (_metadataCache.TryGetValue(baseName, out var cached))
            {
                return cached;
            }

            string metaPath = GetMetadataPath(saveFileName);
            if (metaPath != null && File.Exists(metaPath))
            {
                try
                {
                    string content = File.ReadAllText(metaPath);
                    var metadata = DeserializeMetadata(content);
                    if (metadata != null)
                    {
                        _metadataCache[baseName] = metadata;
                        return metadata;
                    }
                }
                catch { }
            }

            return null;
        }

        /// <summary>
        /// Save/update metadata for a save file.
        /// </summary>
        public void WriteMetadata(string saveFileName, SaveMetadata metadata)
        {
            string metaPath = GetMetadataPath(saveFileName);
            if (metaPath == null) return;

            try
            {
                string baseName = Path.GetFileNameWithoutExtension(saveFileName);
                metadata.SaveName = baseName;

                string content = SerializeMetadata(metadata);
                File.WriteAllText(metaPath, content);

                _metadataCache[baseName] = metadata;

                OniMultiplayerMod.Log($"[MPSaveManager] Saved metadata for: {saveFileName}");
            }
            catch (Exception ex)
            {
                OniMultiplayerMod.LogError($"[MPSaveManager] Failed to save metadata: {ex.Message}");
            }
        }

        /// <summary>
        /// Simple serialization for metadata (line-based key=value format).
        /// </summary>
        private string SerializeMetadata(SaveMetadata meta)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"SaveName={meta.SaveName ?? ""}");
            sb.AppendLine($"Hash={meta.Hash ?? ""}");
            sb.AppendLine($"LastPlayed={meta.LastPlayedStr ?? ""}");
            sb.AppendLine($"HostSteamId={meta.HostSteamId ?? ""}");
            sb.AppendLine($"HostName={meta.HostName ?? ""}");
            sb.AppendLine($"LastCycle={meta.LastCycle}");
            sb.AppendLine($"Version={meta.Version}");
            sb.AppendLine($"Players={string.Join(",", meta.Players ?? new List<string>())}");
            return sb.ToString();
        }

        /// <summary>
        /// Simple deserialization for metadata.
        /// </summary>
        private SaveMetadata DeserializeMetadata(string content)
        {
            var meta = new SaveMetadata();
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                
                string key = line.Substring(0, eq);
                string value = line.Substring(eq + 1);
                
                switch (key)
                {
                    case "SaveName": meta.SaveName = value; break;
                    case "Hash": meta.Hash = value; break;
                    case "LastPlayed": meta.LastPlayedStr = value; break;
                    case "HostSteamId": meta.HostSteamId = value; break;
                    case "HostName": meta.HostName = value; break;
                    case "LastCycle": int.TryParse(value, out meta.LastCycle); break;
                    case "Version": int.TryParse(value, out meta.Version); break;
                    case "Players":
                        if (!string.IsNullOrEmpty(value))
                        {
                            meta.Players = new List<string>(value.Split(','));
                        }
                        break;
                }
            }
            
            return meta;
        }

        /// <summary>
        /// Update metadata after an MP session.
        /// </summary>
        public void UpdateSessionMetadata(string saveFileName, string hostSteamId, string hostName, List<string> players, int cycle)
        {
            var metadata = GetMetadata(saveFileName) ?? new SaveMetadata();

            string savePath = FindMPSave(saveFileName);
            if (savePath != null)
            {
                metadata.Hash = GenerateShortHash(savePath);
            }

            metadata.LastPlayedStr = System.DateTime.UtcNow.ToString("o");
            metadata.HostSteamId = hostSteamId;
            metadata.HostName = hostName;
            metadata.Players = players;
            metadata.LastCycle = cycle;

            WriteMetadata(saveFileName, metadata);
        }

        #endregion

        #region Import/Export

        /// <summary>
        /// Import a single-player save to the MP folder.
        /// </summary>
        public bool ImportFromSinglePlayer(string sourcePath, string newName = null)
        {
            if (!File.Exists(sourcePath))
            {
                OniMultiplayerMod.LogError($"[MPSaveManager] Source file not found: {sourcePath}");
                return false;
            }

            if (string.IsNullOrEmpty(_mpSavePath))
            {
                OniMultiplayerMod.LogError("[MPSaveManager] MP save folder not initialized");
                return false;
            }

            try
            {
                string fileName = newName ?? Path.GetFileName(sourcePath);
                string destPath = Path.Combine(_mpSavePath, fileName);

                // Don't overwrite without confirmation
                if (File.Exists(destPath))
                {
                    OniMultiplayerMod.LogWarning($"[MPSaveManager] Save already exists: {fileName}");
                    // Could add a counter like "colony_mp_2.sav"
                    string baseName = Path.GetFileNameWithoutExtension(fileName);
                    string ext = Path.GetExtension(fileName);
                    int counter = 2;
                    while (File.Exists(destPath))
                    {
                        destPath = Path.Combine(_mpSavePath, $"{baseName}_{counter}{ext}");
                        counter++;
                    }
                    fileName = Path.GetFileName(destPath);
                }

                File.Copy(sourcePath, destPath);
                OniMultiplayerMod.Log($"[MPSaveManager] Imported: {sourcePath} -> {destPath}");

                // Create initial metadata
                var metadata = new SaveMetadata
                {
                    SaveName = Path.GetFileNameWithoutExtension(fileName),
                    Hash = GenerateShortHash(destPath),
                    LastPlayedStr = System.DateTime.UtcNow.ToString("o")
                };
                WriteMetadata(fileName, metadata);

                return true;
            }
            catch (Exception ex)
            {
                OniMultiplayerMod.LogError($"[MPSaveManager] Import failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get single-player saves available for import.
        /// Searches both local and cloud save folders.
        /// </summary>
        public List<MPSaveInfo> GetSinglePlayerSaves()
        {
            var saves = new List<MPSaveInfo>();
            var addedPaths = new HashSet<string>();
            
            // Search in multiple save locations
            var searchPaths = new List<string>();
            
            // Standard save folder
            string baseSaveFolder = GetBaseSaveFolder();
            if (!string.IsNullOrEmpty(baseSaveFolder))
            {
                searchPaths.Add(baseSaveFolder);
            }
            
            // Cloud save folder (different location)
            string cloudFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Klei", "OxygenNotIncluded", "cloud_save_files");
            if (Directory.Exists(cloudFolder))
            {
                searchPaths.Add(cloudFolder);
            }
            
            // Also try getting the game's current save path
            try
            {
                string gamePrefix = SaveLoader.GetSavePrefixAndCreateFolder();
                if (!string.IsNullOrEmpty(gamePrefix))
                {
                    string gameFolder = Path.GetDirectoryName(Path.GetDirectoryName(gamePrefix));
                    if (!string.IsNullOrEmpty(gameFolder) && Directory.Exists(gameFolder) && !searchPaths.Contains(gameFolder))
                    {
                        searchPaths.Add(gameFolder);
                    }
                }
            }
            catch { }

            foreach (var searchPath in searchPaths)
            {
                try
                {
                    var files = Directory.GetFiles(searchPath, "*.sav", SearchOption.AllDirectories);

                    foreach (var file in files)
                    {
                        // Skip files in MP folder
                        if (file.Contains(MP_SAVE_FOLDER)) continue;
                        
                        // Skip duplicates
                        if (addedPaths.Contains(file)) continue;
                        addedPaths.Add(file);

                        var info = new FileInfo(file);
                        
                        saves.Add(new MPSaveInfo
                        {
                            FileName = Path.GetFileName(file),
                            FullPath = file,
                            LastModified = info.LastWriteTime,
                            FileSizeBytes = info.Length
                        });
                    }
                }
                catch (Exception ex)
                {
                    OniMultiplayerMod.LogWarning($"[MPSaveManager] Error searching {searchPath}: {ex.Message}");
                }
            }

            // Sort by last modified
            saves.Sort((a, b) => b.LastModified.CompareTo(a.LastModified));

            return saves;
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validate that a client's save matches the host's.
        /// </summary>
        public bool ValidateSaveHash(string saveFileName, string expectedHash)
        {
            string savePath = FindMPSave(saveFileName);
            if (savePath == null)
            {
                OniMultiplayerMod.LogWarning($"[MPSaveManager] Save not found for validation: {saveFileName}");
                return false;
            }

            string actualHash = GenerateShortHash(savePath);
            bool matches = string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);

            if (!matches)
            {
                OniMultiplayerMod.LogWarning($"[MPSaveManager] Hash mismatch! Expected: {expectedHash}, Got: {actualHash}");
            }

            return matches;
        }

        /// <summary>
        /// Check if a save exists in the MP folder.
        /// </summary>
        public bool HasMPSave(string saveFileName)
        {
            return FindMPSave(saveFileName) != null;
        }

        #endregion

        #region Dupe Assignments

        /// <summary>
        /// Save dupe assignments to metadata file.
        /// </summary>
        public void SaveDupeAssignments(string savePath, Dictionary<int, List<string>> assignments)
        {
            try
            {
                string fileName = Path.GetFileName(savePath);
                var metadata = GetMetadata(fileName) ?? new SaveMetadata();
                metadata.DupeAssignments.Clear();

                foreach (var kvp in assignments)
                {
                    int playerId = kvp.Key;
                    foreach (string dupeName in kvp.Value)
                    {
                        metadata.DupeAssignments.Add($"{dupeName}:{playerId}");
                    }
                }

                WriteMetadata(fileName, metadata);
                OniMultiplayerMod.Log($"[MPSaveManager] Saved {metadata.DupeAssignments.Count} dupe assignments");
            }
            catch (Exception ex)
            {
                OniMultiplayerMod.LogError($"[MPSaveManager] Error saving dupe assignments: {ex.Message}");
            }
        }

        /// <summary>
        /// Load dupe assignments from metadata file.
        /// Returns dict of playerId -> list of dupeNames, or null if no assignments saved.
        /// </summary>
        public Dictionary<int, List<string>> LoadDupeAssignments(string savePath)
        {
            try
            {
                string fileName = Path.GetFileName(savePath);
                var metadata = GetMetadata(fileName);
                if (metadata == null || metadata.DupeAssignments == null || metadata.DupeAssignments.Count == 0)
                {
                    return null;
                }

                var result = new Dictionary<int, List<string>>();

                foreach (string assignment in metadata.DupeAssignments)
                {
                    int lastColon = assignment.LastIndexOf(':');
                    if (lastColon > 0 && lastColon < assignment.Length - 1)
                    {
                        string dupeName = assignment.Substring(0, lastColon);
                        if (int.TryParse(assignment.Substring(lastColon + 1), out int playerId))
                        {
                            if (!result.ContainsKey(playerId))
                            {
                                result[playerId] = new List<string>();
                            }
                            result[playerId].Add(dupeName);
                        }
                    }
                }

                OniMultiplayerMod.Log($"[MPSaveManager] Loaded {metadata.DupeAssignments.Count} dupe assignments");
                return result.Count > 0 ? result : null;
            }
            catch (Exception ex)
            {
                OniMultiplayerMod.LogError($"[MPSaveManager] Error loading dupe assignments: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check if a save has dupe assignments stored.
        /// </summary>
        public bool HasDupeAssignments(string savePath)
        {
            string fileName = Path.GetFileName(savePath);
            var metadata = GetMetadata(fileName);
            return metadata?.DupeAssignments?.Count > 0;
        }

        #endregion
    }
}