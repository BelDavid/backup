using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BackupNS;

public enum OutputType
{
    Success,
    Info,
    Help,
    Danger,
    Warning,
    Error,
}

public enum OutputMode
{
    Write,
    WriteLine,
}

public static class PrettyPrint
{
    public static bool SupressInfoOutput { get; set; } = false;
    public static bool SupressAllOutput { get; set; } = false;

    private static void Writer(string text, OutputType outputType, OutputMode outputMode, ConsoleColor? colorOverride = null)
    {
        if (SupressAllOutput)
        {
            return;
        }

        TextWriter tw = Console.Out;
        ConsoleColor color = ConsoleColor.White;

        switch (outputType)
        {
            case OutputType.Success:
                color = colorOverride ?? ConsoleColor.Green;
                break;
            case OutputType.Info:
                if (SupressInfoOutput) { return; }
                color = colorOverride ?? ConsoleColor.Blue;
                break;
            case OutputType.Help:
                color = colorOverride ?? ConsoleColor.Yellow;
                break;
            case OutputType.Danger:
            case OutputType.Warning:
                color = colorOverride ?? ConsoleColor.DarkYellow;
                break;
            case OutputType.Error:
                color = colorOverride ?? ConsoleColor.Red;
                tw = Console.Error;
                break;
            default:
                return;
        }

        Action<string> WriteFunc = outputMode switch
        {
            OutputMode.Write => tw.Write,
            OutputMode.WriteLine => tw.WriteLine,
            _ => tw.WriteLine,
        };

        var oldColor = Console.ForegroundColor;
        Console.ForegroundColor = color;

        WriteFunc(text ?? string.Empty);

        Console.ForegroundColor = oldColor;
    }
    public static void Write(string text, OutputType outputType, ConsoleColor? color = null)
    {
        Writer(text, outputType, OutputMode.Write, color);
    }
    public static void WriteLine(string text, OutputType outputType, ConsoleColor? color = null)
    {
        Writer(text, outputType, OutputMode.WriteLine, color);
    }

    public static void WriteLine()
    {
        Console.WriteLine();
    }


    public const long KiloByte = 1024;
    public const long MegaByte = 1024 * KiloByte;
    public const long GigaByte = 1024 * MegaByte;

    public static string Bytes(long bytes)
    {
        if (bytes > GigaByte)
        {
            return $"{(double)bytes / GigaByte:F1} GB";
        }
        else if (bytes > MegaByte)
        {
            return $"{(double)bytes / MegaByte:F1} MB";
        }
        else if (bytes > KiloByte)
        {
            return $"{(double)bytes / KiloByte:F1} KB";
        }

        return $"{bytes} B";
    }
}

public static class StringExtensions
{
    public static string SubstituteVariables(this string text, Config config) => config.SubstituteVaribales(text);
    public static string NormalizeSaveName(this string text) => text.ToLower().Replace(' ', '-').Replace('_', '-');
}

public class FileInfoComparer : IComparer<FileInfo>
{
    public int Compare(FileInfo? x, FileInfo? y)
    {
        if (x == null && y == null)
        {
            return 0;
        }
        else if (x == null)
        {
            return -1;
        }
        else if (y == null)
        {
            return 1;
        }

        return x.Name.CompareTo(y.Name);
    }
}

public static class Saves
{
    public static SaveType GetSaveType(string path) =>
        Directory.Exists(path) ? SaveType.Directory :
        File.Exists(path) ? SaveType.File :
        SaveType.None;

    public static FileSystemInfo? GetFileSystemInfo(string path) =>
        GetSaveType(path) switch
        {
            SaveType.Directory => new DirectoryInfo(path),
            SaveType.File => new FileInfo(path),
            SaveType.None => null,
            _ => throw new NotImplementedException(),
        };

    public static string? GetName(string path) => Path.GetFileName(path);

    public static bool Exists(string path) => Directory.Exists(path) || File.Exists(path);
}

public enum SaveType
{
    None,
    Directory,
    File
}


public struct SaveConf
{
    public string? save;
    public string savePath;

    public SaveConf(string? save, string savePath)
    {
        this.save = save;
        this.savePath = savePath;
    }
}