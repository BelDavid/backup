# Framework
 - .NET 6.0
 - C# 10


# Output
### Exit codes:
 - 0 -> _Everything OK_  
 - 1 -> _Prechecks Failed_  
 - 2 -> _Backup Failed_  

### Text Output Colors:
 - Green:  _Success_  
 - Blue:  _Info_  
 - Yellow:  _Help_  
 - DarkYellow:  _Warning_ | _Danger_ | _Deleting_  
 - Red:  _Error_ | _Invalid syntax/params_  
  
  
   
# Backup Config File
Default value: _backup-config.yaml_  
Can be overriden with -C, --config parameter  

If a relative path is specified, the script looks for the config file in the working directory first. If not found, it then looks in the directory where the executable is located.  
  
Supported formats (Determined by extension):
 - JSON
 - YAML  

Provided YAML config file contains detailed comments on the file structure.

# Backup Database 
_backup-db.json_

Located in the same directory as the executable.
Created if not found.  
Stores LastWriteTime of the backed up files/folders.



# TAB Completition
## Powershell
```
Register-ArgumentCompleter -Native -CommandName backup -ScriptBlock {  
  param($wordToComplete)  
  backup list_parameters_plain | where {$_ -like "$wordToComplete*"}  
}  
```
