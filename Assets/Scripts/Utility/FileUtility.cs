using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class FileUtility
{
#if WINDOWS_UWP
    private static string baseDirPath = Windows.Storage.KnownFolders.CameraRoll.Path;
#else
    private static string baseDirPath = Application.persistentDataPath;
    // C:\Users\[ÉÜÅ[ÉUñº]\AppData\LocalLow\[Company Name]\[Product Name]
#endif
    private static string fileName = "CloundSpatialAnchorIdentifiers.txt";

    private static string GetFilePath()
    {
        var path = Path.Combine(baseDirPath, fileName);
        return path.Replace("/", @"\");
    }

    public static void SaveFile(string identifier)
    {
        File.AppendAllText(GetFilePath(), identifier + Environment.NewLine);
    }

    public static List<string> ReadFile()
    {
        if (!File.Exists(GetFilePath())) return null;

        string readText = File.ReadAllText(GetFilePath());
        readText = readText.Replace(Environment.NewLine, "\r");
        readText = readText.Trim('\r');
        string[] readTexts = readText.Split('\r');

        return new List<string>(readTexts);
    }
}
