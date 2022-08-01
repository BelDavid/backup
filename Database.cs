using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackupNS;

public interface IDatabase
{
    public bool HasChanged { get; }

    public void Update();

    public bool ContainsID(string id);
    public DateTime? GetTimestamp(string id);
    public DateTime? this[string id] { get => GetTimestamp(id); set {  if (value.HasValue) SetTimestamp(id, value.Value); } }
    public void SetTimestamp(string id, DateTime timestamp);
}

public class FileDatabase : IDatabase
{
    private readonly string dbFilePath;
    private readonly Dictionary<string, DateTime> database;
    private bool _hasChanged = false;
    public bool HasChanged => _hasChanged;


    public FileDatabase(string dbFilePath)
    {
        this.dbFilePath = dbFilePath;

        if (!File.Exists(dbFilePath))
        {
            PrettyPrint.WriteLine($"File '{dbFilePath}' not found. Creating new one.", OutputType.Warning);
            try
            {
                File.WriteAllText(dbFilePath, "{}");
                database = new Dictionary<string, DateTime>();
                return;
            }
            catch (Exception ex)
            {
                throw new BackupException($"Failed to create '{dbFilePath}': {ex.Message}.");
            }
        }

        try
        {
            var databaseStr = File.ReadAllText(dbFilePath);
            database = JsonConvert.DeserializeObject<Dictionary<string, DateTime>>(databaseStr) ?? new Dictionary<string, DateTime>();
        }
        catch (Exception ex)
        {
            throw new BackupException($"Failed to load or parse data from '{dbFilePath}': {ex.Message}.");
        }
    }

    public bool ContainsID(string id) => database.ContainsKey(id);
    public DateTime? GetTimestamp(string id) => database.ContainsKey(id) ? database[id] : null;

    public void SetTimestamp(string id, DateTime timestamp)
    {
        _hasChanged = !ContainsID(id) || timestamp != database[id];
        database[id] = timestamp;
    }

    public void Update()
    {
        if (!HasChanged)
        {
            return;
        }

        try
        {
            var databaseStr = JsonConvert.SerializeObject(database, Formatting.Indented);
            File.WriteAllText(dbFilePath, databaseStr);
            _hasChanged = false;
        }
        catch (Exception ex)
        {
            PrettyPrint.WriteLine($"Failed to save database: {ex.Message}.", OutputType.Error);
        }
    }
}
