using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using QuestPatcher.Core.Downgrading.Models;
using Serilog;

namespace QuestPatcher.Core.Downgrading
{
    public class DowngradeManger
    {
        private const string IndexUrl = @"https://github.com/Lauriethefish/mbf-diffs/releases/download/1.0.0/index.json";

        private const string DiffUrlBase = @"https://github.com/Lauriethefish/mbf-diffs/releases/download/1.0.0/";

        private readonly InstallManager _installManager;

        private readonly ExternalFilesDownloader _filesDownloader;

        private readonly IDictionary<string, IList<AppDiff>> _availablePaths = new Dictionary<string, IList<AppDiff>>();

        private readonly HttpClient _httpClient = new();
        
        private bool _loaded = false;
        
        private bool _loading = false;

        public DowngradeManger(InstallManager installManager, ExternalFilesDownloader filesDownloader)
        {
            _installManager = installManager;
            _filesDownloader = filesDownloader;
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
    }
}
