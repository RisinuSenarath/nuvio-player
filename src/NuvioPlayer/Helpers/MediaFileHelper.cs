using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NuvioPlayer.Helpers;

public static class MediaFileHelper
{
    public static readonly string[] VideoExtensions =
    {
        ".mp4", ".mkv", ".avi", ".mov", ".webm",
        ".mpeg", ".mpg", ".ts", ".m2ts", ".flv", ".wmv"
    };

    public static readonly string[] AudioExtensions =
    {
        ".mp3", ".flac", ".wav", ".ogg", ".m4a",
        ".aac", ".wma", ".opus"
    };

    public static readonly string[] SubtitleExtensions =
    {
        ".srt", ".ass", ".ssa", ".vtt", ".sub"
    };

    private static readonly HashSet<string> AllMediaExtensionsSet = new(
        VideoExtensions.Concat(AudioExtensions),
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> SubtitleExtensionsSet = new(
        SubtitleExtensions,
        StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> AllSupportedMediaExtensions =>
        AllMediaExtensionsSet.ToList();

    public static bool IsSupportedMediaFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;
        string ext = Path.GetExtension(filePath);
        return !string.IsNullOrEmpty(ext) && AllMediaExtensionsSet.Contains(ext);
    }

    public static bool IsSupportedSubtitleFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;
        string ext = Path.GetExtension(filePath);
        return !string.IsNullOrEmpty(ext) && SubtitleExtensionsSet.Contains(ext);
    }

    public static string GetOpenFileDialogFilter()
    {
        string allPattern = string.Join(";", AllMediaExtensionsSet.Select(e => $"*{e}"));
        string videoPattern = string.Join(";", VideoExtensions.Select(e => $"*{e}"));
        string audioPattern = string.Join(";", AudioExtensions.Select(e => $"*{e}"));

        return $"All Supported Media|{allPattern}|" +
               $"Video Files|{videoPattern}|" +
               $"Audio Files|{audioPattern}|" +
               $"All Files (*.*)|*.*";
    }

    public static List<string> ScanDirectoryForMedia(string directoryPath, bool recursive = false)
    {
        var results = new List<string>();
        if (!Directory.Exists(directoryPath)) return results;

        try
        {
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.EnumerateFiles(directoryPath, "*.*", searchOption);
            foreach (var file in files)
            {
                if (IsSupportedMediaFile(file))
                {
                    results.Add(file);
                }
            }
            results.Sort(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            // Directory access might be restricted; gracefully return partial or empty list
        }

        return results;
    }
}
