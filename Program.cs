﻿// See https://aka.ms/new-console-template for more information
using Backup;
using YamlDotNet;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;


#region === Parameters ===
var paramGame = new Parameter<string>('g', "game", "A shortName of the game to backup up. Must be defined in the config file. Pass value 'list' to list all supported games.", mandatory: true);
var paramSave = new Parameter<string>('s', "save", "Specify a specificic save withing the given game.");
var paramNote = new Parameter<string>('n', "note", "Append a short note to the backup file name.");

var paramPermanent = new SwitchParameter('p', "permanent", "Make backup immune to the -clear parameter.");
var paramClear = new SwitchParameter('c', "clear", "Clear old temporary saves, but keep 10 newest saves.");
var paramKeepCount = new Parameter<int>('k', "keepCount", $"Overrides the default count for --{paramClear.LongName} parameter. Valid range [2, 99 999].", defaultValue: 10, constrain: i => i >= 2 && i <= 99_999);

var paramOpen = new SwitchParameter('o', "open", "Open backup folder.");

var paramDryRun = new SwitchParameter('d', "dry", "Run the script without actually doing anything.");
var paramForce = new SwitchParameter('f', "force", "Ignores whether files changed. Create backup anyway.");
var paramOmit = new SwitchParameter('O', "omit", "Omit backup process, but do everything else.");
var paramQuiet = new SwitchParameter('q', "quiet", "Supress Info messages");

var paramConfig = new Parameter<string>('C', "config", "Custom path to the config file.", defaultValue: "backup-config.yaml");


var parameters = new Parameters(paramGame, paramSave, paramNote, paramPermanent, paramClear, paramKeepCount, paramOpen, paramForce, paramDryRun, paramOmit, paramQuiet, paramConfig);
var paramsState = parameters.Evaluate(args);

if (paramsState != ParamsState.Correct)
{
    if (paramsState == ParamsState.InvalidError)
    {
        PrettyPrint.WriteLine("Passed arguments are not valid.", OutputType.Error);
    }
    Environment.ExitCode = 1;
    return;
}

if (paramQuiet.IsSet)
{
    PrettyPrint.SupressInfoOutput = true;
}
#endregion

#region === Load Data ===
string exeLocation = Path.GetDirectoryName(Environment.ProcessPath);
string workingDir = Environment.CurrentDirectory;
string configFileName = paramConfig.Value;
string configFilePath = null;
if (!Path.IsPathRooted(configFileName))
{
    configFilePath = Path.Combine(workingDir, configFileName);
    if (!File.Exists(configFilePath))
    {
        configFilePath = Path.Combine(exeLocation, configFileName);
        if (!File.Exists(configFilePath))
        {
            PrettyPrint.WriteLine($"Unable to locate the config file '{configFileName}'!", OutputType.Error);
            Environment.ExitCode = 1;
            return;
        }
    }
}
else
{
    configFilePath = configFileName;
    if (!File.Exists(configFilePath))
    {
        PrettyPrint.WriteLine($"Unable to locate the config file at '{configFileName}'!", OutputType.Error);
        Environment.ExitCode = 1;
        return;
    }
}

string configFileDataStr;
try
{
    configFileDataStr = File.ReadAllText(configFilePath);
}
catch (Exception ex)
{
    PrettyPrint.WriteLine($"{ex.Message}", OutputType.Error);
    PrettyPrint.WriteLine($"Failed to load config data from '{configFileName}'.", OutputType.Error);
    Environment.ExitCode = 1;
    return;
}

Config config = null;
var fileInfo = new FileInfo(configFilePath);

if (fileInfo.Extension == ".json")
{
    try
    {
        config = JsonConvert.DeserializeObject<Config>(configFileDataStr);
    }
    catch (Exception ex)
    {
        PrettyPrint.WriteLine(ex.Message, OutputType.Error);
        PrettyPrint.WriteLine("Failed to Parse JSON config file.", OutputType.Error);
        Environment.ExitCode = 1;
        return;
    }
}
else if (fileInfo.Extension == ".yaml" || fileInfo.Extension == ".yml")
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
        PrettyPrint.WriteLine(ex.Message, OutputType.Error);
        PrettyPrint.WriteLine("Failed to Parse YAML config file.", OutputType.Error);
        Environment.ExitCode = 1;
        return;
    }
}
else
{
    PrettyPrint.WriteLine("Unsupported Config type. Use JSON, or YAML/YML. (Extension must match the type.)", OutputType.Error);
    Environment.ExitCode = 1;
    return;
}

if (config is null)
{
    PrettyPrint.WriteLine("No data loaded from the config file!", OutputType.Error);
    Environment.ExitCode = 1;
    return;
}

if (!config.ValidateAndBuild())
{
    PrettyPrint.WriteLine("Failed to Validate the config data.", OutputType.Error);
    Environment.ExitCode = 1;
    return;
}
#endregion

#region === Resolve Game Parameter ===
// List all supported games and exit
if (paramGame.Value == "list")
{
    PrettyPrint.WriteLine("Supported games:", OutputType.Help);
    foreach (var gc in config.gameConfigs)
    {
        PrettyPrint.WriteLine($" {gc.shortName}: {gc.fullName}", OutputType.Help);
        switch (gc.BackupMethod)
        {
            case GameConfig.BackupMethods.map:
                PrettyPrint.WriteLine($"   saves: [{string.Join(", ", gc.saveMap.Keys)}]", OutputType.Help, ConsoleColor.DarkYellow);
                break;
            case GameConfig.BackupMethods.listd:
                try
                {
                    PrettyPrint.WriteLine($"   saves: [{string.Join(", ", Saves.GetFolderSaves(gc.saveDirPath, gc.savePattern).Select(s => s.Name))}]", OutputType.Help, ConsoleColor.DarkYellow);
                }
                catch (Exception ex)
                {
                    PrettyPrint.WriteLine($"   saves: Failed to load potential saves. {ex.Message}", OutputType.Warning, ConsoleColor.Red);
                    continue;
                }
                break;
            case GameConfig.BackupMethods.listf:
                try
                {
                    PrettyPrint.WriteLine($"   saves: [{string.Join(", ", Saves.GetFileSaves(gc.saveDirPath, gc.savePattern).Select(s => s.Name))}]", OutputType.Help, ConsoleColor.DarkYellow);
                }
                catch (Exception ex)
                {
                    PrettyPrint.WriteLine($"   saves: Failed to load potential saves. {ex.Message}", OutputType.Warning, ConsoleColor.Red);
                    continue;
                }
                break;
        }
    }
    Environment.ExitCode = 0;
    return;
}

// Check if a game by given shortName is supported.
if (!config.ContainsGame(paramGame.Value))
{
    PrettyPrint.WriteLine($"Can not backup '{paramGame.Value}'. No such game is configured.", OutputType.Error);
    Environment.ExitCode = 1;
    return;
}
#endregion

#region === Load JSON DB ===
string dbFileName = "backup-db.json";
string dbFilePath = Path.Combine(exeLocation, dbFileName);
var database = new Dictionary<string, DateTime>();
bool databseChanged = false;

if (!File.Exists(dbFilePath))
{
    PrettyPrint.WriteLine($"Failed to find file '{dbFileName}'. Creating new one.", OutputType.Warning);
    try
    {
        File.WriteAllText(dbFilePath, "{}");
    }
    catch (Exception ex)
    {
        PrettyPrint.WriteLine($"{ex.Message}", OutputType.Error);
        PrettyPrint.WriteLine($"Failed to create '{dbFileName}'.", OutputType.Error);
        Environment.ExitCode = 1;
        return;
    }
}

try
{
    var databaseStr = File.ReadAllText(dbFilePath);
    database = JsonConvert.DeserializeObject<Dictionary<string, DateTime>>(databaseStr);
}
catch (Exception ex)
{
    PrettyPrint.WriteLine($"{ex.Message}", OutputType.Error);
    PrettyPrint.WriteLine($"Failed to load data from '{dbFileName}'.", OutputType.Error);
    Environment.ExitCode = 1;
    return;
}
#endregion

// === BACKUP ===
#region --- Preprocess ---
if (paramDryRun.IsSet)
{
    PrettyPrint.WriteLine("[Dry Run] No actual changes will be made!", OutputType.Info);
}
var gameConfig = config[paramGame.Value];
if (paramSave.IsSet)
{
    paramSave.SetValue(paramSave.Value?.ToLower());
}
if (paramNote.IsSet)
{
    paramNote.SetValue(paramNote.Value?.Replace(' ', '-').Replace('_', '-'));
}

string? savePath;
string? backupDir = Path.Combine(config.backupsDirPath, gameConfig.backupDirName);
string? saveSpaceless = paramSave.Value?.Replace(' ', '-');
SaveType saveType = SaveType.None;
#endregion

#region --- Process ---
switch (gameConfig.BackupMethod)
{
    case GameConfig.BackupMethods.all:
        if (paramSave.IsSet)
        {
            PrettyPrint.WriteLine($"Parameter -{paramSave.Flag}, --{paramSave.LongName} isn't required to backup {gameConfig.fullName}.", OutputType.Help);
            paramSave.Clear();
        }
        savePath = gameConfig.saveDirPath;
        break;

    case GameConfig.BackupMethods.map:
        if (!paramSave.IsSet || !gameConfig.saveMap.ContainsKey(paramSave.Value))
        {
            PrettyPrint.WriteLine($"Value of parameter --{paramSave.LongName} is expected to be one of the defined keys in '{nameof(Config.gameConfigs)}[{nameof(gameConfig.shortName)}: {gameConfig.shortName}].{nameof(gameConfig.saveMap)}':", OutputType.Error);
            PrettyPrint.WriteLine($"Available keys: [{string.Join(", ", gameConfig.saveMap.Select(kv => kv.Key))}]", OutputType.Error);
            Environment.ExitCode = 1;
            return;
        }
        savePath = Path.Combine(gameConfig.saveDirPath, gameConfig.saveMap[paramSave.Value]);
        break;

    case GameConfig.BackupMethods.listd:
        var savesd = Saves.GetFolderSaves(gameConfig.saveDirPath, gameConfig.savePattern);
        DirectoryInfo? dirInfoToBackup = null;

        if (!paramSave.IsSet)
        {
            PrettyPrint.WriteLine($"You need to set the --{paramSave.LongName} parameter to a name of the save folder you want to back up.", OutputType.Error);
            PrettyPrint.WriteLine($"Available saves: [{string.Join(", ", savesd.Select(s => s.Name))}]", OutputType.Error);
            Environment.ExitCode = 1;
            return;
        }
        foreach (var save in savesd)
        {
            if (saveSpaceless == save.Name.ToLower().Replace(' ', '-'))
            {
                dirInfoToBackup = save;
                break;
            }
        }
        if (dirInfoToBackup == null)
        {
            PrettyPrint.WriteLine($"No save folder with the name '{paramSave.Value}' found.", OutputType.Error);
            PrettyPrint.WriteLine($"Available saves: [{string.Join(", ", savesd.Select(s => s.Name))}]", OutputType.Error);
            Environment.ExitCode = 1;
            return;
        }
        savePath = dirInfoToBackup.FullName;

        break;

    case GameConfig.BackupMethods.listf:
        var savesf = Saves.GetFileSaves(gameConfig.saveDirPath, gameConfig.savePattern);
        FileInfo? fileInfoToBackup = null;
        
        if (!paramSave.IsSet)
        {
            PrettyPrint.WriteLine($"You need to set the --{paramSave.LongName} parameter to a name of the save file you want to back up.", OutputType.Error);
            PrettyPrint.WriteLine($"Available saves: [{string.Join(", ", savesf.Select(s => s.Name))}]", OutputType.Error);
            Environment.ExitCode = 1;
            return;
        }
        foreach (var save in savesf)
        {
            if (saveSpaceless == save.Name.ToLower().Replace(' ', '-'))
            {
                fileInfoToBackup = save;
                break;
            }
        }
        if (fileInfoToBackup == null)
        {
            PrettyPrint.WriteLine($"No save file with the name '{paramSave.Value}' found.", OutputType.Error);
            PrettyPrint.WriteLine($"Available saves: [{string.Join(", ", savesf.Select(s => s.Name))}]", OutputType.Error);
            Environment.ExitCode = 1;
            return;
        }
        savePath = fileInfoToBackup.FullName;
        break;

    default:
        PrettyPrint.WriteLine("Unexpected Backup method. Aborting", OutputType.Error);
        Environment.ExitCode = 1;
        return;
}
string id = gameConfig.shortName + (paramSave.IsSet ? "_" + saveSpaceless : "");
string backupFileName = $"{id}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{(paramPermanent.IsSet ? "perm":"temp")}{(paramNote.IsSet ? "_" + paramNote.Value : "")}.zip";
#endregion

#region --- Checks ---
if (Directory.Exists(savePath))
{
    saveType = SaveType.Directory;
}
else if (File.Exists(savePath))
{
    saveType = SaveType.File;
}
else
{
    PrettyPrint.WriteLine($"Save '{savePath}' does not exist. Nothing to back up.", OutputType.Error);
    Environment.ExitCode = 1;
    return;
}

if (!Directory.Exists(backupDir))
{
    PrettyPrint.WriteLine($"Directory '{backupDir}' does not exist. Creating.", OutputType.Info);
    try
    {
        Directory.CreateDirectory(backupDir);
        PrettyPrint.WriteLine($" - Directory '{backupDir}' successfully created.", OutputType.Success);
    }
    catch (Exception)
    {
        PrettyPrint.WriteLine($" - Failed to create directory '{backupDir}'.", OutputType.Error);
        Environment.ExitCode = 1;
        return;
    }
}

// Check if backup is necessary with LastWriteTime
var latestWriteTime = DateTime.MinValue;
DirectoryInfo saveDirInfo = null;
FileInfo saveFileInfo = null;
switch (saveType)
{
    case SaveType.Directory:
        saveDirInfo = new DirectoryInfo(savePath);
        var saveDirFiles = saveDirInfo.GetFiles("", SearchOption.AllDirectories);
        foreach (var file in saveDirFiles)
        {
            if (file.LastWriteTime > latestWriteTime)
            {
                latestWriteTime = file.LastWriteTime;
            }
        }
        break;
    case SaveType.File:
        saveFileInfo = new FileInfo(savePath);
        latestWriteTime = saveFileInfo.LastWriteTime;
        break;
    default:
        PrettyPrint.WriteLine("Unexpected save type.", OutputType.Error);
        Environment.ExitCode = 1;
        return;
}
bool newestBackupAlreadyExists = false;
if (database.ContainsKey(id))
{
    if (latestWriteTime > database[id])
    {
        database[id] = latestWriteTime;
        databseChanged = true;
    }
    else
    {
        newestBackupAlreadyExists = true;
    }
}
else
{
    database[id] = latestWriteTime;
    databseChanged = true;
}
#endregion

#region --- Backup ---
if (!paramOmit.IsSet)
{
    if (paramPermanent.IsSet || !newestBackupAlreadyExists || paramForce.IsSet)
    {
        PrettyPrint.WriteLine($"Backing up '{gameConfig.fullName}{(paramSave.IsSet ? ": " + paramSave.Value : "")}' ...", OutputType.Info);
        try
        {
            if (!paramDryRun.IsSet)
            {
                switch (saveType)
                {
                    case SaveType.Directory: 
                        ZipFile.CreateFromDirectory(savePath, Path.Combine(backupDir, backupFileName), CompressionLevel.SmallestSize, true);
                        break;
                    case SaveType.File:
                        using (var fs = new FileStream(Path.Combine(backupDir, backupFileName), FileMode.Create))
                        {
                            using (var arch = new ZipArchive(fs, ZipArchiveMode.Create))
                            {
                                arch.CreateEntryFromFile(savePath, saveFileInfo.Name);
                            }
                        }
                        break;
                }
            }
            PrettyPrint.WriteLine($"Backup '{backupFileName}' created.", OutputType.Success);
        }
        catch (Exception ex)
        {
            PrettyPrint.WriteLine($"Backup failed: {ex.Message}", OutputType.Error);
            Environment.ExitCode = 2;
            return;
        }
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
#endregion

#region --- Update JSON DB ---
if (databseChanged)
{
    try
    {
        if (!paramDryRun.IsSet)
        {
            var databaseStr = JsonConvert.SerializeObject(database, Formatting.Indented);
            File.WriteAllText(dbFilePath, databaseStr);
        }
    }
    catch (Exception ex)
    {
        PrettyPrint.WriteLine($"Failed to update database: {ex.Message}", OutputType.Error);
    }
}
#endregion


#region === Clear ===
if (paramClear.IsSet)
{
    var backupDirInfo = new DirectoryInfo(backupDir);
    var tempFiles = backupDirInfo.GetFiles($"{id}_????-??-??_??-??-??_temp*.zip");
    Array.Sort(tempFiles, new FileInfoComparer());
    var tempFilesToDelete = tempFiles.SkipLast(paramKeepCount.Value);

    long freedMemory = 0;

    foreach (var file in tempFilesToDelete)
    {
        PrettyPrint.Write($" - Deleting '{file.Name}'.", OutputType.Danger);

        try
        {
            if (!paramDryRun.IsSet)
            {
                File.Delete(file.FullName);
            }
            freedMemory += file.Length;
            PrettyPrint.WriteLine(" Success.", OutputType.Danger, ConsoleColor.Green);
        }
        catch (Exception ex)
        {
            PrettyPrint.WriteLine($" Failed to delete file: {ex.Message}", OutputType.Error);
            continue;
        }
    }
    PrettyPrint.WriteLine($"Total {PrettyPrint.Bytes(freedMemory)} of memory cleared.", OutputType.Danger);
}
#endregion

#region === Open Backup Folder ===
if (paramOpen.IsSet)
{
#if WINDOWS || LINUX
    try 
    {
        if (!paramDryRun.IsSet)
        {
#if WINDOWS
            Process.Start("explorer.exe", backupDir);
#elif LINUX
            Process.Start("xdg-open", backupDir);
#endif
        }
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
#endregion
