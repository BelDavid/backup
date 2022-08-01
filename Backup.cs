using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackupNS;

public class Backup
{
    private static readonly string? assemblyPath;
    private static readonly string workingDirectoryPath;
    static Backup()
    {
        assemblyPath = Path.GetDirectoryName(Environment.ProcessPath);
        workingDirectoryPath = Environment.CurrentDirectory;
    }


    public const string backupAllKeyword = "all";

    private readonly string configFilePath;
    private readonly Config config;

    private readonly IDatabase database;

    public int KeepCount { get; set; }

    public bool FlagPermanent { get; set; }
    public bool FlagClear { get; set; }
    public bool FlagOpen { get; set; }
    public bool FlagForce { get; set; }
    public bool FlagOmit { get; set; }
   

    public Backup(string configFilePath, string databaseFilePath)
    {
        this.configFilePath = FindFile(configFilePath) ?? throw new FileNotFoundException($"Cound not find Config file at {configFilePath}.");
        config = LoadConfig(this.configFilePath);

        databaseFilePath = FindFile(databaseFilePath) ?? Path.Combine(assemblyPath ?? workingDirectoryPath, configFilePath);
        database = new FileDatabase(databaseFilePath);
    }

    private static string? FindFile(string filePath)
    {
        if (Path.IsPathRooted(filePath))
        {
            return File.Exists(filePath) ? filePath : null;
        }
        else
        {
            return FindFile(Path.Combine(workingDirectoryPath, filePath))
                ?? (
                    assemblyPath != null
                    ? FindFile(Path.Combine(assemblyPath, filePath))
                    : null
                );
        }
    }

    private static Config LoadConfig(string configFilePath)
    {
        Config? config;
        FileInfo configFileInfo;
        string configFileDataStr;
        try
        {
            configFileInfo = new FileInfo(configFilePath);
            configFileDataStr = File.ReadAllText(configFilePath);
        }
        catch (Exception ex)
        {
            throw new BackupException($"Failed to load config data: {ex.Message}");
        }

        if (configFileInfo.Extension == ".json")
        {
            try
            {
                config = JsonConvert.DeserializeObject<Config>(configFileDataStr);
            }
            catch (Exception ex)
            {
                throw new BackupException($"Failed to parse JSON config data: {ex.Message}");
            }
        }
        else if (configFileInfo.Extension == ".yaml" || configFileInfo.Extension == ".yml")
        {
            try
            {
                var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                    .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.NullNamingConvention.Instance)
                    .Build();
                config = deserializer.Deserialize<Config>(configFileDataStr);
            }
            catch (Exception ex)
            {
                throw new BackupException($"Failed to parse YAML config data: {ex.Message}");
            }
        }
        else
        {
            throw new BackupException($"Unsupported config type: {configFileInfo.Extension}. Use .json, or .yaml/.yml (Extension must match the type.)");
        }

        if (config is null)
        {
            throw new BackupException("No data loaded from the config file!");
        }

        if (!config.ValidateAndBuild())
        {
            throw new BackupException("Config data not valid.");
        }

        return config;
    }

    private static void OpenFolder(string backupDirectoryPath)
    {
#if WINDOWS || LINUX
            try
            {
                if (!Directory.Exists(backupDirectoryPath))
                {
                    throw new DirectoryNotFoundException($"Directory '{backupDirectoryPath}' not found");
                }
#if WINDOWS
                Process.Start("explorer.exe", backupDirectoryPath);
#elif LINUX
                Process.Start("xdg-open", backupDir);
#endif
                PrettyPrint.WriteLine($"Backup folder opened.", OutputType.Info);
            }
            catch (Exception ex)
            {
                PrettyPrint.WriteLine($"Failed to open backup folder: {ex.Message}", OutputType.Error);
            }
#else
    PrettyPrint.WriteLine($"Openig Backup folder is curently not supported on this OS.", ConsoleColor.DarkYellow);
#endif
    }

    public void Execute(string game, string? save, string? note)
    {
        switch (game)
        {
            case "list":
                ListAllSupportedGames();
                break;
            case backupAllKeyword:
                ExecuteAll(note);
                break;
            default:
                ExecuteSingle(game, save, note);
                break;
        }
    }

    private void ListAllSupportedGames()
    {
        PrettyPrint.WriteLine("Supported games:", OutputType.Help);
        foreach (var gc in config.gameConfigs!)
        {
            PrettyPrint.WriteLine($" {gc.shortName}: {gc.fullName}", OutputType.Help);
            try
            {
                var saves = gc.GetSaves();
                if (saves.Where(s => s.save != null).Any())
                {
                    PrettyPrint.WriteLine($"   saves: [{string.Join(", ", saves.Select(s => s.save))}]", OutputType.Help, ConsoleColor.DarkYellow);
                }
            }
            catch (Exception ex)
            {
                PrettyPrint.WriteLine($"   saves: Failed to load potential saves. {ex.Message}", OutputType.Warning, ConsoleColor.Red);
                continue;

            }
        }
    }

    private void ExecuteAll(string? note)
    {
        note = note?.Replace(' ', '-').Replace('_', '-');

        PrettyPrint.WriteLine($"=== Backing up Everything ===.", OutputType.Info);
        foreach (var gameConfig in config.gameConfigs)
        {
            PrettyPrint.WriteLine($"> game: {gameConfig.shortName}", OutputType.Info);
            foreach (var save in gameConfig.GetSaves()) // Every (game, save) pair configured in config
            {
                PrettyPrint.WriteLine($">> save: {gameConfig.shortName}", OutputType.Info);
                try
                {
                    ExecuteSingle(gameConfig, save.save, note, flagOpenOverride: true);
                }
                catch (Exception ex)
                {
                    PrettyPrint.WriteLine($"Backup Failed: {ex.Message}", OutputType.Error);
                }
            }
            PrettyPrint.WriteLine();
        }

        if (FlagOpen)
        {
            OpenFolder(config.backupsDirPath!);
        }
    }

    private void ExecuteSingle(string game, string? save, string? note, bool flagOpenOverride = false)
    {
        if (!config.ContainsGame(game))
        {
            throw new BackupException($"Can not backup '{game}'. No such game is configured.");
        }

        ExecuteSingle(config[game], save, note, flagOpenOverride);
    }

    private void ExecuteSingle(GameConfig gameConfig, string? save, string? note, bool flagOpenOverride = false)
    {
        save = save?.ToLower();
        note = note?.Replace(' ', '-').Replace('_', '-');

        var saveConf = ValidateSaveParameter(gameConfig, ref save, out string id);

        string backupDirPath = Path.Combine(config.backupsDirPath!, gameConfig.backupDirName!);
        if (!FlagOmit)
        {
            if (IsBackupNeeded(saveConf, id, out DateTime latestWriteTime))
            {
                CreateBackup(gameConfig, saveConf, id, save, note, backupDirPath, latestWriteTime);
                database.Update();
            }
            else
            {
                PrettyPrint.WriteLine("Save files did not change since last backup. New backup not created.", OutputType.Info);
            }
        }
        else
        {
            PrettyPrint.WriteLine("Omiting Backup.", OutputType.Info);
        }

        if (FlagClear)
        {
            Clear(id, backupDirPath);
        }
        
        if (FlagOpen && !flagOpenOverride)
        {
            OpenFolder(backupDirPath);
        }
    }

    private static SaveConf ValidateSaveParameter(GameConfig gameConfig, ref string? save, out string id)
    {
        SaveConf saveConfDefault = default;
        switch (gameConfig.BackupMethod)
        {
            case GameConfig.BackupMethods.all:
                save = null;
                id = gameConfig.shortName;
                return gameConfig.GetSaves().First();

            case GameConfig.BackupMethods.map:
                if (save is null || !gameConfig.saveMap.ContainsKey(save))
                {
                    throw new SaveParamNotValidException(
                        $"Value of parameter '{nameof(save)}' is expected to be one of the defined keys in '{nameof(Config.gameConfigs)}[{nameof(gameConfig.shortName)}: {gameConfig.shortName}].{nameof(gameConfig.saveMap)}':",
                        gameConfig.saveMap.Select(kv => kv.Key)
                    );
                }
                break;

            case GameConfig.BackupMethods.list:
                if (save is null)
                {
                    throw new SaveParamNotValidException($"You need to set the '{nameof(save)}' parameter to a name of the save file/folder you want to back up.", gameConfig.GetSaves().Select(sc => sc.save)!);
                }
                saveConfDefault = new SaveConf(save, Path.Combine(gameConfig.saveDirPath, save));
                break;

            default:
                throw new NotImplementedException();
        }

        var saveNormalized = save?.NormalizeSaveName();
        id = $"{gameConfig.shortName}_{saveNormalized}";

        return gameConfig.GetSaves().FirstOrDefault(sc => sc.save == saveNormalized, saveConfDefault);
    }

    private bool IsBackupNeeded(SaveConf saveConf, string id, out DateTime latestWriteTime)
    {
        // Check if backup is necessary with LastWriteTime
        latestWriteTime = Saves.GetSaveType(saveConf.savePath) switch
        {
            SaveType.None => DateTime.MinValue,
            SaveType.Directory => new DirectoryInfo(saveConf.savePath).GetFileSystemInfos("", SearchOption.AllDirectories).Max(fsi => fsi.LastWriteTime),
            SaveType.File => new FileInfo(saveConf.savePath).LastWriteTime,
            _ => throw new NotImplementedException(),
        };

        return !database.ContainsID(id)
            || latestWriteTime > database[id]
            || FlagPermanent
            || FlagForce;
    }

    private void CreateBackup(GameConfig gameConfig, SaveConf saveConf, string id, string? save, string? note, string backupDirPath, DateTime latestWriteTime)
    {
        if (!Saves.Exists(saveConf.savePath))
        {
            throw new BackupException($"Save '{save}' at '{saveConf.savePath}' does not exists. Nothing to backup.");
        }

        string backupFileName = $"{id}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{(FlagPermanent ? "perm" : "temp")}{(note != null ? "_"+note : "")}.zip";
        string backupFilePath = Path.Combine(backupDirPath, backupFileName);

        if (!Directory.Exists(backupDirPath))
        {
            PrettyPrint.WriteLine($"Directory '{backupDirPath}' does not exist. Creating.", OutputType.Info);
            try
            {
                Directory.CreateDirectory(backupDirPath);
                PrettyPrint.WriteLine($" - Directory '{backupDirPath}' created.", OutputType.Success);
            }
            catch (Exception)
            {
                throw new BackupException($" - Failed to create directory '{backupDirPath}'.");
            }
        }

        PrettyPrint.WriteLine($"Backing up '{gameConfig.fullName}{(save != null ? ": " + save : "")}' ...", OutputType.Info);
        try
        {
            switch (Saves.GetSaveType(saveConf.savePath))
            {
                case SaveType.Directory:
                    ZipFile.CreateFromDirectory(saveConf.savePath, backupFilePath, CompressionLevel.SmallestSize, true);
                    break;
                case SaveType.File:
                    using (var fs = new FileStream(backupFilePath, FileMode.Create))
                    {
                        using (var arch = new ZipArchive(fs, ZipArchiveMode.Create))
                        {
                            arch.CreateEntryFromFile(saveConf.savePath, Saves.GetName(saveConf.savePath)!);
                        }
                    }
                    break;
            }
            database[id] = latestWriteTime;
            PrettyPrint.WriteLine($"Backup '{backupFileName}' created.", OutputType.Success);
        }
        catch (Exception ex)
        {
            throw new BackupException($"Backup failed: {ex.Message}");
        }
    }

    private void Clear(string id, string backupDirPath)
    {
        var backupDirInfo = new DirectoryInfo(backupDirPath);
        var tempFiles = backupDirInfo.GetFiles($"{id}_????-??-??_??-??-??_temp*.zip");
        Array.Sort(tempFiles, new FileInfoComparer());
        var tempFilesToDelete = tempFiles.SkipLast(KeepCount);

        long freedMemory = 0;

        foreach (var file in tempFilesToDelete)
        {
            PrettyPrint.Write($" - Deleting '{file.Name}'.", OutputType.Danger);

            try
            {
                File.Delete(file.FullName);
                freedMemory += file.Length;
                PrettyPrint.WriteLine(" Success.", OutputType.Success);
            }
            catch (Exception ex)
            {
                PrettyPrint.WriteLine($" Failed to delete file: {ex.Message}", OutputType.Error);
                continue;
            }
        }
        PrettyPrint.WriteLine($"Total {PrettyPrint.Bytes(freedMemory)} of memory cleared.", OutputType.Info, ConsoleColor.DarkYellow);
    }
}
