// See https://aka.ms/new-console-template for more information
using BackupNS;
using YamlDotNet;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;

// ==== Parameters ====
var paramGame = new Parameter<string>('g', "game", "A shortName of the game to backup up. Must be defined in the config file. Pass value 'list' to list all supported games.", isMandatory: true, constrain: ParameterConstrains.NotNull);
var paramSave = new Parameter<string>('s', "save", "Specify a specificic save withing the given game.");
var paramNote = new Parameter<string>('n', "note", "Append a short note to the backup file name.");

var paramKeepCount = new Parameter<int>('k', "keepCount", $"Overrides the default count for -c, --clear parameter. Valid range [2, 99 999].", isPositional: false, defaultValue: 10, constrain: n => n >= 2 && n <= 99_999);
var paramConfig = new Parameter<string>('C', "config", "Custom path to the config file.", isPositional: false, defaultValue: "backup-config.yaml", constrain: ParameterConstrains.NotNull);
var paramDatabase = new Parameter<string>('d', "database", "Custom path to the database file.", isPositional: false, defaultValue: "backup-db.json", constrain: ParameterConstrains.NotNull);

var paramPermanent = new SwitchParameter('p', "permanent", "Make backup immune to the -clear parameter.");
var paramOpen = new SwitchParameter('o', "open", "Open backup folder.");
var paramClear = new SwitchParameter('c', "clear", "Clear old temporary saves, but keep 10 newest saves.");
var paramForce = new SwitchParameter('f', "force", "Ignores whether files changed. Create backup anyway.");
var paramOmit = new SwitchParameter('b', "no-backup", "Omit backup process, but do everything else.");
var paramQuiet = new SwitchParameter('q', "quiet", "Supress Info messages");
var paramSuperQuiet = new SwitchParameter('Q', "superQuiet", "Supress All messages");


var parameters = new Parameters(
    // Mandatory Positional Value Parameters
    paramGame,

    // Positional Value Parameters
    paramSave, paramNote,

    // Non-Positional Value Parameters
    paramKeepCount, paramConfig, paramDatabase,

    // Switch Parameters
    paramPermanent, paramClear, paramOpen, paramForce, paramOmit, paramQuiet, paramSuperQuiet
);
var paramsState = parameters.Evaluate(args);

if (paramsState != ParamsState.Correct)
{
    if (paramsState == ParamsState.InvalidError)
    {
        PrettyPrint.WriteLine("Passed arguments are not valid.", OutputType.Error);
        Environment.ExitCode = 1;
    }
    return;
}

if (paramQuiet.IsSet)
{
    PrettyPrint.SupressInfoOutput = true;
}
if (paramSuperQuiet.IsSet)
{
    PrettyPrint.SupressAllOutput = true;
}

// ==== Backup ====
Backup backup;

try
{
    backup = new Backup(paramConfig.Value!, paramDatabase.Value!)
    {
        KeepCount = paramKeepCount.Value,

        FlagPermanent = paramPermanent.IsSet,
        FlagOpen = paramOpen.IsSet,
        FlagClear = paramClear.IsSet,
        FlagForce = paramForce.IsSet,
        FlagOmit = paramOmit.IsSet,
    };
}
catch (Exception ex)
{
    PrettyPrint.WriteLine($"Failed to initialize Backup: {ex.Message}", OutputType.Error);
    Environment.ExitCode = 2;
    return;
}

try
{
    backup.Execute(paramGame.Value!, paramSave.Value, paramNote.Value);
}
catch (SaveParamNotValidException ex)
{
    PrettyPrint.WriteLine($"Backup Failed: {ex.Message}", OutputType.Error);
    PrettyPrint.WriteLine(ex.AvailableSavesMessage, OutputType.Error);
    Environment.ExitCode = 3;
    return;
}
catch (Exception ex)
{
    PrettyPrint.WriteLine($"Backup Failed: {ex.Message}", OutputType.Error);
    Environment.ExitCode = 3;
    return;
}