using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackupNS;

public class BackupException : Exception
{
    public BackupException(string? message) : base(message)
    {
    }
}

public class SaveParamNotValidException : Exception
{
    public IEnumerable<string> availableSaves;
    public string AvailableSavesMessage => $"Available saves: [{string.Join(", ", availableSaves)}]";

    public SaveParamNotValidException(string? message, IEnumerable<string> availableSaves) : base(message)
    {
        this.availableSaves = availableSaves;
    }
}
