using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Backup
{
    internal static class PrettyPrint
    {
        internal static void WriteLine(string text, ConsoleColor color)
        {
            var currentColor = Console.ForegroundColor;
            Console.ForegroundColor = color;

            Console.WriteLine(text);

            Console.ForegroundColor = currentColor;
        }
        internal static void Write(string text, ConsoleColor color)
        {
            var currentColor = Console.ForegroundColor;
            Console.ForegroundColor = color;

            Console.Write(text);

            Console.ForegroundColor = currentColor;
        }

        internal static void ErrorWriteLine(string text, ConsoleColor color)
        {
            var currentColor = Console.ForegroundColor;
            Console.ForegroundColor = color;

            Console.Error.WriteLine(text);

            Console.ForegroundColor = currentColor;
        }
        internal static void ErrorWrite(string text, ConsoleColor color)
        {
            var currentColor = Console.ForegroundColor;
            Console.ForegroundColor = color;

            Console.Error.Write(text);

            Console.ForegroundColor = currentColor;
        }

        internal static void WriteUniversal<T>(T obj, ConsoleColor color, Action<T> writer)
        {
            var currentColor = Console.ForegroundColor;
            Console.ForegroundColor = color;

            writer(obj);

            Console.ForegroundColor = currentColor;
        }

        internal static void WriteLine()
        {
            Console.WriteLine();
        }


        internal const long KiloByte = 1024;
        internal const long MegaByte = 1024 * KiloByte;
        internal const long GigaByte = 1024 * MegaByte;

        internal static string Bytes(long bytes)
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

    internal static class StringExtensions
    {
        internal static string SubstituteVariables(this string data, Config config) => config.SubstituteVaribales(data);
    }

    internal class FileInfoComparer : IComparer<FileInfo>
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

    internal static class Saves
    {
        internal static FileInfo[] GetFileSaves(string pattern, string dirPath)
        {
            var regex = new Regex(pattern);
            var dirInfo = new DirectoryInfo(dirPath);
            var saves = dirInfo.GetFiles("", SearchOption.TopDirectoryOnly);
            return saves.Where(f => regex.IsMatch(f.Name)).ToArray();
        }
        internal static DirectoryInfo[] GetFolderSaves(string pattern, string dirPath)
        {
            var regex = new Regex(pattern);
            var dirInfo = new DirectoryInfo(dirPath);
            var saves = dirInfo.GetDirectories("", SearchOption.TopDirectoryOnly);
            return saves.Where(d => regex.IsMatch(d.Name)).ToArray();
        }
    }

    internal enum SaveType
    {
        None,
        Directory,
        File
    }
}
