using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Serilog;

namespace QuestPatcher.Core.Utils;

public class DownloadMirrorUtil
{
    public static readonly DownloadMirrorUtil Instance = new();

    private const string MirrorUrl = @"https://bs.wgzeyu.com/localization/mods.json";

    private long _lastRefreshTime = 0;

    private readonly HttpClient _client = new();

    private Dictionary<string, string> _mirrorUrls = new();

    private CancellationTokenSource? _cancellationTokenSource;

    private readonly Dictionary<string, string> _staticMirrors = new()
    {
        // TODO Add Static ones if needed
    };

    public IDictionary<string, string> StaticMirrors => _staticMirrors;

    private DownloadMirrorUtil()
    {
    }

    public async Task Refresh()
    {
        Log.Information("Refreshing download mirror URL ");

        // Cancel a previous refresh attempt in case it is not finished
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;
        try
        {
            var res = await _client.GetStringAsync(MirrorUrl, token);

            var jObject = JObject.Parse(res);
            token.ThrowIfCancellationRequested();

            var mirrorUrls = new Dictionary<string, string>(_staticMirrors);

            foreach(var pair in jObject)
            {
                var mirror = pair.Value?["mirrorUrl"]?.ToString();
                if(mirror != null)
                {
                    mirrorUrls[pair.Key] = mirror;
                }
            }

            token.ThrowIfCancellationRequested();

            _mirrorUrls = mirrorUrls;
            _lastRefreshTime = DateTimeOffset.Now.ToUnixTimeSeconds();
        }
        catch(OperationCanceledException)
        {
            Log.Warning("Refresh mirror url cancelled");
        }
        catch(Exception e)
        {
            Log.Error(e, "Cannot fetch mirror url");
            // we don't want to overwrite what we previously have 
        }
    }

    public async Task<string> GetMirrorUrl(string original)
    {
        if(DateTimeOffset.Now.ToUnixTimeSeconds() - _lastRefreshTime > 300)
        {
            Log.Information("Mirror Url cache too old! Refreshing");
            await Refresh();
        }

        if(_mirrorUrls.TryGetValue(original, out var mirror))
        {
            Log.Debug("Mirror Url found: {Mirror}", mirror);
            return mirror;
        }

        Log.Warning("Mirror Url not found for {Original}", original);
        return original;
    }
}
