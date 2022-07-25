using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Backup
{
    public enum OutputType
    {
        Success,
        Info,
        Help,
        Danger,
        Warning,
        Error,
    }

    public static class PrettyPrint
    {
        public static bool supressInfoOutput = false;
        private static void Writer<T>(T obj, OutputType outputType, Func<TextWriter, Action<T>> map, ConsoleColor? color = null)
        {
            switch (outputType)
            {
                case OutputType.Success:
                    WriteUniversal(obj, color ?? ConsoleColor.Green, map(Console.Out));
                    break;
                case OutputType.Info:
                    if (!supressInfoOutput)
                    {
                        WriteUniversal(obj, color ?? ConsoleColor.Blue, map(Console.Out));
                    }
                    break;
                case OutputType.Help:
                    WriteUniversal(obj, color ?? ConsoleColor.Yellow, map(Console.Out));
                    break;
                case OutputType.Danger:
                case OutputType.Warning:
                    WriteUniversal(obj, color ?? ConsoleColor.DarkYellow, map(Console.Out));
                    break;
                case OutputType.Error:
                    WriteUniversal(obj, color ?? ConsoleColor.Red, map(Console.Error));
                    break;
            }
        }
        public static void WriteLine(string text, OutputType outputType, ConsoleColor? color = null)
        {
            Writer(text, outputType, w => w.WriteLine, color);
        }
        public static void Write(string text, OutputType outputType, ConsoleColor? color = null)
        {
            Writer(text, outputType, w => w.Write, color);
        }

        public static void WriteUniversal<T>(T obj, ConsoleColor color, Action<T> writer)
        {
            var currentColor = Console.ForegroundColor;
            Console.ForegroundColor = color;

            writer(obj);

            Console.ForegroundColor = currentColor;
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
        public static string SubstituteVariables(this string data, Config config) => config.SubstituteVaribales(data);
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
        public static FileInfo[] GetFileSaves(string pattern, string dirPath)
        {
            var regex = new Regex(pattern);
            var dirInfo = new DirectoryInfo(dirPath);
            var saves = dirInfo.GetFiles("", SearchOption.TopDirectoryOnly);
            return saves.Where(f => regex.IsMatch(f.Name)).ToArray();
        }
        public static DirectoryInfo[] GetFolderSaves(string pattern, string dirPath)
        {
            var regex = new Regex(pattern);
            var dirInfo = new DirectoryInfo(dirPath);
            var saves = dirInfo.GetDirectories("", SearchOption.TopDirectoryOnly);
            return saves.Where(d => regex.IsMatch(d.Name)).ToArray();
        }
    }

    public enum SaveType
    {
        None,
        Directory,
        File
    }
}
