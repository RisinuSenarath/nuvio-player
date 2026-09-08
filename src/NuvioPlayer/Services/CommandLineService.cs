using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using NuvioPlayer.Helpers;
using NuvioPlayer.Interfaces;

namespace NuvioPlayer.Services;

public class CommandLineService : ICommandLineService
{
    private readonly ILogger<CommandLineService> _logger;

    public CommandLineService(ILogger<CommandLineService> logger)
    {
        _logger = logger;
    }

    public List<string> ParseMediaPaths(IEnumerable<string> args)
    {
        var mediaFiles = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawArg in args)
        {
            if (string.IsNullOrWhiteSpace(rawArg)) continue;

            string arg = rawArg.Trim('\"', '\'');

            // Skip command switches/flags
            if (arg.StartsWith("-", StringComparison.Ordinal) || arg.StartsWith("/", StringComparison.Ordinal))
            {
                _logger.LogDebug("Skipping command-line switch: {Switch}", arg);
                continue;
            }

            try
            {
                string fullPath = Path.GetFullPath(arg);

                if (Directory.Exists(fullPath))
                {
                    _logger.LogInformation("Scanning directory from command line: {Dir}", fullPath);
                    var scanned = MediaFileHelper.ScanDirectoryForMedia(fullPath, recursive: false);
                    foreach (var file in scanned)
                    {
                        if (seen.Add(file))
                        {
                            mediaFiles.Add(file);
                        }
                    }
                }
                else if (File.Exists(fullPath))
                {
                    if (MediaFileHelper.IsSupportedMediaFile(fullPath))
                    {
                        if (seen.Add(fullPath))
                        {
                            mediaFiles.Add(fullPath);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Command line file not supported: {File}", fullPath);
                    }
                }
                else
                {
                    _logger.LogWarning("Command line target not found: {Path}", fullPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing command line argument: {Arg}", rawArg);
            }
        }

        return mediaFiles;
    }
}
