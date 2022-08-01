using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BackupNS
{
#pragma warning disable 0649
#pragma warning disable 8602
#pragma warning disable 8604
#pragma warning disable 8618
#pragma warning disable 8601
    public class Config
    {
        // -- DATA --
        public Dictionary<string, string> variables;
        public string backupsDirPath;

        public GameConfig[] gameConfigs;
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
                PrettyPrint.WriteLine($"[Config] Invalid value in '{nameof(variables)}', or missing definition. Dictionary {{}} expected!", OutputType.Error);
                valid = false;
            }
            else
            {
                // Build Variable map
                variableMap.Clear();
                foreach (var (varName, varValue) in variables)
                {
                    var varNameLower = "$" + varName.ToLower();
                    if (variableMap.ContainsKey(varNameLower))
                    {
                        PrettyPrint.WriteLine($"Variable '{varName}' already defined!", OutputType.Error);
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
                PrettyPrint.WriteLine($"[Config] Invalid value in '{nameof(backupsDirPath)}', or missing definition. String \"\" expected!", OutputType.Error);
                valid = false;
            }
            backupsDirPath = backupsDirPath?.SubstituteVariables(this);

            if (gameConfigs == null)
            {
                PrettyPrint.WriteLine($"[Config] Invalid value in '{nameof(gameConfigs)}', or missing definition. List [] expected!", OutputType.Error);
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
                        PrettyPrint.WriteLine($"[Config] Invalid value in '{nameof(gameConfigs)}[{i}]', or missing definition. Dictionary {{}} expected!", OutputType.Error);
                        valid = false;
                    }
                    else if (string.IsNullOrWhiteSpace(gameConfig.shortName))
                    {
                        PrettyPrint.WriteLine($"[Config] Invalid value in '{nameof(gameConfigs)}[{i}].{nameof(GameConfig.shortName)}', or missing definition. String \"\" expected!", OutputType.Error);
                        valid = false;
                    }
                    else
                    {
                        valid &= gameConfig.ValidateAndBuild(this);
                    }

                    // Add to the gameConfigMap
                    if (gameConfigMap.ContainsKey(gameConfig.shortName))
                    {
                        PrettyPrint.WriteLine($"[Config] Duplicite value '{gameConfig.shortName}' in '{nameof(gameConfigs)}[{i}].{nameof(GameConfig.shortName)}'. Config with this shorName has already been defined.", OutputType.Error);
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

        /// <summary>
        /// Returns gameConfig
        /// </summary>
        /// <param name="shortName"></param>
        /// <returns></returns>
        public GameConfig this[string shortName] => gameConfigMap[shortName];
        public bool ContainsGame(string shortName) => gameConfigMap.ContainsKey(shortName);
    }


    public class GameConfig
    {
        // -- DATA --
        public string shortName;
        public string fullName;
        public string backupDirName;
        public string savePattern;

        public string backupMethod;
        public string saveDirPath;

        public Dictionary<string, string> saveMap;

        private BackupMethods __backupMethod;
        // ----------
        public BackupMethods BackupMethod => __backupMethod;
        private Regex regexSavePattern;

        public bool ValidateAndBuild(Config config)
        {
            bool valid = true;

            // shortName is validated in GameConfig.Validate() method.
            if (fullName == null)
            {
                PrettyPrint.WriteLine($"[Config] Invalid value in '{nameof(Config.gameConfigs)}[{nameof(shortName)}: {shortName}].{nameof(fullName)}', or missing definition. String \"\" expected.", OutputType.Error);
                valid = false;
            }
            if (backupDirName == null)
            {
                PrettyPrint.WriteLine($"[Config] Invalid value in '{nameof(Config.gameConfigs)}[{nameof(shortName)}: {shortName}].{nameof(backupDirName)}', or missing definition. String \"\" expected.", OutputType.Error);
                valid = false;
            }

            if (backupMethod == null)
            {
                PrettyPrint.WriteLine($"[Config] Invalid value in '{nameof(Config.gameConfigs)}[{nameof(shortName)}: {shortName}].{nameof(backupMethod)}', or missing definition. String \"\" expected.", OutputType.Error);
                valid = false;
            }
            else if (!Enum.TryParse(backupMethod, true, out __backupMethod))
            {
                PrettyPrint.WriteLine($"[Config] Invalid value in '{nameof(Config.gameConfigs)}[{nameof(shortName)}: {shortName}].{nameof(backupMethod)}'. Allowed values: {string.Join(", ", Enum.GetNames<BackupMethods>())}", OutputType.Error);
                valid = false;
            }

            if (saveDirPath == null)
            {
                PrettyPrint.WriteLine($"[Config] Invalid value in '{nameof(Config.gameConfigs)}[{nameof(shortName)}: {shortName}].{nameof(saveDirPath)}', or missing definition. String \"\" expected.", OutputType.Error);
                valid = false;
            }
            saveDirPath = saveDirPath?.SubstituteVariables(config);

            // Test additional parameters needed by diferrent BackupMethods
            switch (BackupMethod)
            {
                case BackupMethods.map:
                    if (saveMap == null)
                    {
                        PrettyPrint.WriteLine($"[Config] Invalid value in '{nameof(Config.gameConfigs)}[{nameof(shortName)}: {shortName}].{nameof(saveMap)}', or missing definition. Dictionary {{}} expected.", OutputType.Error);
                        valid = false;
                    }
                    else
                    {
                        foreach (var (key, val) in saveMap)
                        {
                            if (string.IsNullOrWhiteSpace(key))
                            {
                                PrettyPrint.WriteLine($"[Config] Invalid key name in '{nameof(Config.gameConfigs)}[{nameof(shortName)}: {shortName}].{nameof(saveMap)}'. Can not be empty, or white space.", OutputType.Error);
                                valid = false;
                            }
                            else if (string.IsNullOrWhiteSpace(val))
                            {
                                PrettyPrint.WriteLine($"[Config] Invalid value in '{nameof(Config.gameConfigs)}[{nameof(shortName)}: {shortName}].{nameof(saveMap)}[{key}]'. Can not be null, empty, or white space.", OutputType.Error);
                                valid = false;
                            }
                            else
                            {
                                saveMap[key] = val.SubstituteVariables(config);
                            }
                        }
                    }
                    break;
                case BackupMethods.list:
                    if (string.IsNullOrWhiteSpace(savePattern))
                    {
                        savePattern = ".+";
                    }
                    regexSavePattern = new Regex(savePattern);
                    break;
            }
            return valid;
        }

        public IEnumerable<SaveConf> GetSaves() =>
            BackupMethod switch
            {
                BackupMethods.all =>
                    from _ in Enumerable.Repeat(1, 1)
                    select new SaveConf(null, saveDirPath),
                BackupMethods.map =>
                    from kv in saveMap
                    select new SaveConf(kv.Key, Path.Combine(saveDirPath, kv.Value)),
                BackupMethods.list => 
                    from savePath in Directory.GetFileSystemEntries(saveDirPath)
                    let saveName = Saves.GetName(savePath)
                    where regexSavePattern.IsMatch(saveName)
                    select new SaveConf(saveName.NormalizeSaveName(), savePath),
                _ => throw new NotImplementedException(),
            };


        public enum BackupMethods
        {
            all,
            map,
            list
        }
    }
#pragma warning restore 0649
#pragma warning restore 8602
#pragma warning restore 8604
#pragma warning restore 8618
#pragma warning restore 8601
}
