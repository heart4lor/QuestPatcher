using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using QuestPatcher.Core.Downgrading.Models;
using QuestPatcher.Core.Utils;
using Serilog;

namespace QuestPatcher.Core.Downgrading
{
    public class DowngradeManger
    {
        private const string IndexUrl = @"https://github.com/Lauriethefish/mbf-diffs/releases/download/1.0.0/index.json";

        private const string DiffUrlBase = @"https://github.com/Lauriethefish/mbf-diffs/releases/download/1.0.0/";

        private readonly InstallManager _installManager;

        private readonly ExternalFilesDownloader _filesDownloader;
        
        private readonly AndroidDebugBridge _debugBridge;

        private readonly string _outputFolder;

        private readonly IDictionary<string, IList<AppDiff>> _availablePaths = new Dictionary<string, IList<AppDiff>>();

        private readonly HttpClient _httpClient = new();
        
        private bool _loaded = false;
        
        private bool _loading = false;

        public DowngradeManger(InstallManager installManager, ExternalFilesDownloader filesDownloader, AndroidDebugBridge debugBridge, SpecialFolders specialFolders)
        {
            _installManager = installManager;
            _filesDownloader = filesDownloader;
            _debugBridge = debugBridge;
            _outputFolder = specialFolders.DowngradeFolder;
        }

        public async Task<bool> LoadAvailableDowngrades()
        {
            if (_loaded || _loading) return true;
            _loading = true;
            _loaded = false;
            bool success = false;
            try
            {
                success = await Load();
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to load downgrade index");
            }
            finally
            {   
                _loaded = success;
                _loading = false;
            }
            
            return success;
        }

        private async Task<bool> Load()
        {
            Log.Information("Loading downgrade index...");
            _availablePaths.Clear();
            // Load available downgrades
            string indexJson = await _httpClient.GetStringAsync(IndexUrl);
            var appDiffs = JsonSerializer.Deserialize<IList<AppDiff>>(indexJson);
            if (appDiffs == null)
            {
                Log.Warning("Failed to deserialize downgrade index");
                return false;
                _loading = false;
            }

            Log.Debug("Deserialized {Count} app diffs", appDiffs.Count);

            // Group by from version
            foreach (var appDiff in appDiffs)
            {
                if (_availablePaths.TryGetValue(appDiff.FromVersion, out var existingPaths))
                {
                    existingPaths.Add(appDiff);
                }
                else
                {
                    _availablePaths[appDiff.FromVersion] = new List<AppDiff> { appDiff };
                }
            }
            
            Log.Debug("Downgrade index loaded");
            return true;
        }

        public IList<string> GetAvailablePathFor(string fromVersion)
        {
            if (string.IsNullOrWhiteSpace(fromVersion)) return new List<string>();
            return _availablePaths.TryGetValue(fromVersion, out var paths)
                ? paths.Select(path => path.ToVersion).ToList()
                : new List<string>();
        }
        
        public async Task DowngradeApp(string fromVersion, string toVersion)
        {
            // assume the app is installed and is beat saber
            if (!_availablePaths.TryGetValue(fromVersion, out var paths))
            {
                Log.Warning("No downgrade path found for {FromVersion}", fromVersion);
                throw new DowngradeException("No downgrade path found");
            }

            var path = paths.FirstOrDefault(p => p.ToVersion == toVersion);
            if (path == null)
            {
                Log.Warning("No downgrade path found from {FromVersion} to {ToVersion}", fromVersion, toVersion);
                throw new DowngradeException("Cannot downgrade to specified version");
            }

            await DowngradeApp(path);
        }

        private async Task DowngradeApp(AppDiff appDiff)
        {
            Log.Information("Starting downgrade from {FromVersion} to {ToVersion}", appDiff.FromVersion, appDiff.ToVersion);
            
            // Download and apply diffs
            // TODO check apk crc
            string apkPath = await PatchFile(appDiff.ApkDiff, _installManager.InstalledApp!.Path);
            
            foreach (var fileDiff in appDiff.ObbDiffs)
            {
                bool result = await CheckAndDownloadObbFile(fileDiff);
                if (!result)
                {
                    Log.Error("Obb file {FileName} not found or is corrupted", fileDiff.FileName);
                    throw new DowngradeException("Obb file not found or is corrupted");
                }
                
                await PatchFile(fileDiff);
            }
            
            // Replace the app with the downgraded version
            await ReplaceAppWithDowngraded(apkPath, appDiff.ObbDiffs);
        }

        /// <summary>
        /// Check the source file exists.
        /// Download the file from device if it exists.
        /// TODO check the crc of the file
        /// </summary>
        /// <param name="fileDiff">File diff index</param>
        /// <returns>Whether the file successfully downloaded</returns>
        private async Task<bool> CheckAndDownloadObbFile(FileDiff fileDiff)
        {
            string? path;
            try
            {
                path = await _installManager.DownloadObbFile(fileDiff.FileName, _outputFolder);
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to download obb file {ObbName}", fileDiff.FileName);
                throw new DowngradeException("Failed to download obb file", e);
            }
            
            // TODO check crc
            return path != null;
        }
        
        private async Task<string> PatchFile(FileDiff fileDiff, string? sourcePathOverride = null)
        {
            Log.Information("Patch file {FileName} with {DiffName}", fileDiff.FileName, fileDiff.DiffName);
            string diffPath = Path.Combine(_outputFolder, fileDiff.DiffName);
            string sourcePath = sourcePathOverride ?? Path.Combine(_outputFolder, fileDiff.FileName);
            string outputPath = Path.Combine(_outputFolder, fileDiff.OutputFileName);
            
            // Download the diff file
            string uri = $"{DiffUrlBase}{fileDiff.DiffName}";
            _ = await _filesDownloader.DownloadUri(uri, diffPath);

            await FilePatcher.PatchFileAsync(sourcePath, outputPath, diffPath);
            return outputPath;
        }
        
        // This has a lot of copy-pasted code from PatchingManager.
        // Refactor to a shared method on InstallManager may cause a lot of future upstream merge conflicts
        private async Task ReplaceAppWithDowngraded(string apkPath, IList<FileDiff> obbDiffs)
        {
            // Close any running instance of the app.
            await _debugBridge.ForceStop(SharedConstants.BeatSaberPackageID);
            
            // backup stuff
            Log.Information("Backing up data directory");
            string? dataBackupPath;
            try
            {
                dataBackupPath = await _installManager.CreateDataBackup();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create data backup");
                dataBackupPath = null;
            }

            Log.Information("Backing up obb directory");
            string? obbBackupPath;
            try
            {
                obbBackupPath = await _installManager.CreateObbBackup();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create obb backup");
                obbBackupPath = null; // Indicate that the backup failed
            }

            // Uninstall and reinstall the diff patched apk
            
            try
            {
                await _debugBridge.UninstallApp(SharedConstants.BeatSaberPackageID);
            }
            catch (AdbException)
            {
                Log.Warning("Failed to remove the original APK, likely because it was already removed in a previous patching attempt");
                Log.Warning("Will continue with modding anyway");
            }

            Log.Information("Installing downgraded APK");
            await _debugBridge.InstallApp(apkPath);

            // Restore backups
            
            if (dataBackupPath != null)
            {
                Log.Information("Restoring data backup");
                try
                {
                    await _installManager.RestoreDataBackup(dataBackupPath);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to restore data backup");
                }
            }

            if (obbBackupPath != null)
            {
                Log.Information("Restoring obb backup");
                try
                {
                    await _installManager.RestoreObbBackup(obbBackupPath);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to restore obb backup");
                }
            }
            
            // Push patched obb files

            foreach (var obbDiff in obbDiffs)
            {
                string obbPath = Path.Combine(_outputFolder, obbDiff.OutputFileName);
                string obbName = Path.GetFileName(obbPath);
                Log.Information("Pushing patched obb file {ObbName}", obbName);
                try
                {
                    await _installManager.ReplaceObbFile(obbDiff.FileName, obbDiff.OutputFileName, obbPath);
                }
                catch (Exception e)
                {
                    Log.Error("Failed to push obb file {ObbName}", obbName);
                    throw new DowngradeException("Failed to push patched obb file", e);
                }
            }
            
            await _installManager.NewApkInstalled(apkPath);

            Log.Information("App Downgraded successfully");
        }
    }
}
