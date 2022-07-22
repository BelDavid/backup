using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backup
{
#pragma warning disable 0649
#pragma warning disable 8602
#pragma warning disable 8604
    internal class Config
    {
        // -- DATA --
        public Dictionary<string, string>? variables;
        public string? backupsDirPath;

        public GameConfig[]? gameConfigs;
        // ----------

        private readonly Dictionary<string, string> variableMap = new();
        private readonly Dictionary<string, GameConfig> gameConfigMap = new();

        public string SubstituteVaribales(string data)
        {
            if (data.Contains('$'))
            {
                foreach (var (varName, varValue) in variableMap)
                {
                    data = data.Replace(varName, varValue, StringComparison.CurrentCultureIgnoreCase);
                }
            }

            return data;
        }

        public bool ValidateAndBuild()
        {
            bool valid = true;

            if (variables == null)
            {
                PrettyPrint.ErrorWriteLine($"[Config] Invalid value in '{nameof(variables)}', or missing definition. Dictionary {{}} expected!", ConsoleColor.Red);
                valid = false;
            }
            else
            {
                // Build Variable map
                variableMap.Clear();
                var fields = this.GetType().GetFields().Where(field => field.FieldType == typeof(string));
                foreach (var (varName, varValue) in variables)
                {
                    var varNameLower = "$" + varName.ToLower();
                    if (variableMap.ContainsKey(varNameLower))
                    {
                        PrettyPrint.ErrorWriteLine($"Variable '{varName}' already defined!", ConsoleColor.Red);
                        valid = false;
                        break;
                    }
                    else
                    {
                        variableMap.Add(varNameLower, varValue?.SubstituteVariables(this) ?? string.Empty);
                    }
                }
            }

            if (backupsDirPath == null)
            {
                PrettyPrint.ErrorWriteLine($"[Config] Invalid value in '{nameof(backupsDirPath)}', or missing definition. String \"\" expected!", ConsoleColor.Red);
                valid = false;
            }
            backupsDirPath = backupsDirPath?.SubstituteVariables(this);

            if (gameConfigs == null)
            {
                PrettyPrint.ErrorWriteLine($"[Config] Invalid value in '{nameof(gameConfigs)}', or missing definition. List [] expected!", ConsoleColor.Red);
                valid = false;
            }
            else
            {
                gameConfigMap.Clear();
                for (int i = 0; i < gameConfigs.Length; i++)
                {
                    var gameConfig = gameConfigs[i];
                    if (gameConfig == null)
                    {
                        PrettyPrint.ErrorWriteLine($"[Config] Invalid value in '{nameof(gameConfigs)}[{i}]', or missing definition. Dictionary {{}} expected!", ConsoleColor.Red);
                        valid = false;
                    }
                    else if (string.IsNullOrWhiteSpace(gameConfig.shortName))
                    {
                        PrettyPrint.ErrorWriteLine($"[Config] Invalid value in '{nameof(gameConfigs)}[{i}].{nameof(GameConfig.shortName)}', or missing definition. String \"\" expected!", ConsoleColor.Red);
                        valid = false;
                    }
                    else
                    {
                        valid &= gameConfig.ValidateAndBuild(this);
                    }

                    // Add to the gameConfigMap
                    if (gameConfigMap.ContainsKey(gameConfig.shortName))
                    {
                        PrettyPrint.ErrorWriteLine($"[Config] Duplicite value '{gameConfig.shortName}' in '{nameof(gameConfigs)}[{i}].{nameof(GameConfig.shortName)}'. Config with this shorName has already been defined.", ConsoleColor.Red);
                        valid = false;
                    }
                    else
                    {
                        gameConfigMap.Add(gameConfig.shortName, gameConfig);
                    }
                }
            }
            
            return valid;
        }

        public GameConfig this[string shortName] => gameConfigMap[shortName];
        public bool ContainsGame(string shortName) => gameConfigMap.ContainsKey(shortName);
    }


    internal class GameConfig
    {
        // -- DATA --
        public string? shortName;
        public string? fullName;
        public string? backupDirName;
        public string? savePattern;

        public string? backupMethod;
        public string? saveDirPath;

        public Dictionary<string, string> saveMap;

        private BackupMethods __backupMethod;
        // ----------
        public BackupMethods BackupMethod => __backupMethod;

        public bool ValidateAndBuild(Config config)
        {
            bool valid = true;

            // shortName is validated in GameConfig.Validate() method.
            if (fullName == null)
            {
                PrettyPrint.ErrorWriteLine($"[Config] Invalid value in '{nameof(Config.gameConfigs)}[{nameof(shortName)}: {shortName}].{nameof(fullName)}', or missing definition. String \"\" expected.", ConsoleColor.Red);
                valid = false;
            }
            if (backupDirName == null)
            {
                PrettyPrint.ErrorWriteLine($"[Config] Invalid value in '{nameof(Config.gameConfigs)}[{nameof(shortName)}: {shortName}].{nameof(backupDirName)}', or missing definition. String \"\" expected.", ConsoleColor.Red);
                valid = false;
            }

            if (backupMethod == null)
            {
                PrettyPrint.ErrorWriteLine($"[Config] Invalid value in '{nameof(Config.gameConfigs)}[{nameof(shortName)}: {shortName}].{nameof(backupMethod)}', or missing definition. String \"\" expected.", ConsoleColor.Red);
                valid = false;
            }
            else if (!Enum.TryParse(backupMethod, true, out __backupMethod))
            {
                PrettyPrint.ErrorWriteLine($"[Config] Invalid value in '{nameof(Config.gameConfigs)}[{nameof(shortName)}: {shortName}].{nameof(backupMethod)}'. Allowed values: {string.Join(", ", Enum.GetNames<BackupMethods>())}", ConsoleColor.Red);
                valid = false;
            }

            if (saveDirPath == null)
            {
                PrettyPrint.ErrorWriteLine($"[Config] Invalid value in '{nameof(Config.gameConfigs)}[{nameof(shortName)}: {shortName}].{nameof(saveDirPath)}', or missing definition. String \"\" expected.", ConsoleColor.Red);
                valid = false;
            }
            saveDirPath = saveDirPath?.SubstituteVariables(config);

            // Test additional parameters needed by diferrent BackupMethods
            switch (BackupMethod)
            {
                case BackupMethods.map:
                    if (saveMap == null)
                    {
                        PrettyPrint.ErrorWriteLine($"[Config] Invalid value in '{nameof(Config.gameConfigs)}[{nameof(shortName)}: {shortName}].{nameof(saveMap)}', or missing definition. Dictionary {{}} expected.", ConsoleColor.Red);
                        valid = false;
                    }
                    else
                    {
                        foreach (var (key, val) in saveMap)
                        {
                            if (string.IsNullOrWhiteSpace(key))
                            {
                                PrettyPrint.ErrorWriteLine($"[Config] Invalid key name in '{nameof(Config.gameConfigs)}[{nameof(shortName)}: {shortName}].{nameof(saveMap)}'. Can not be empty, or white space.", ConsoleColor.Red);
                                valid = false;
                            }
                            else if (string.IsNullOrWhiteSpace(val))
                            {
                                PrettyPrint.ErrorWriteLine($"[Config] Invalid value in '{nameof(Config.gameConfigs)}[{nameof(shortName)}: {shortName}].{nameof(saveMap)}[{key}]'. Can not be null, empty, or white space.", ConsoleColor.Red);
                                valid = false;
                            }
                        }
                    }
                    break;
                case BackupMethods.listd:
                case BackupMethods.listf:
                    if (string.IsNullOrWhiteSpace(savePattern))
                    {
                        savePattern = ".+";
                    }
                    break;
            }

            return valid;
        }

        internal enum BackupMethods
        {
            all,
            map,
            listd,
            listf
        }
    }
#pragma warning restore 0649
#pragma warning restore 8602
#pragma warning restore 8604
}
