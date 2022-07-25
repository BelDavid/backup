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

    public enum OutputMode
    {
        Write,
        WriteLine,
    }

    public static class PrettyPrint
    {
        public static bool SupressInfoOutput { get; set; } = false;

        private static void Writer(string text, OutputType outputType, OutputMode outputMode, ConsoleColor? colorOverride = null)
        {
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
        public static FileInfo[] GetFileSaves(string dirPath, string pattern)
        {
            var regex = new Regex(pattern);
            var dirInfo = new DirectoryInfo(dirPath);
            var saves = dirInfo.GetFiles("", SearchOption.TopDirectoryOnly);
            return saves.Where(f => regex.IsMatch(f.Name)).ToArray();
        }
        public static DirectoryInfo[] GetFolderSaves(string dirPath, string pattern)
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
