using System;
using System.Diagnostics;
using Serilog;

namespace QuestPatcher.Utils;

public static class Util
{
    public static void OpenWebpage(string url)
    {
        ProcessStartInfo psi = new()
        {
            FileName = url,
            UseShellExecute = true
        };
        try
        {
            Process.Start(psi);
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to open webpage: {Url}", url);
        }
    }
}
