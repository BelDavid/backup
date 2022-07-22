# Output
### Exit codes:
 - **0** -> _Everything OK_  
 - **1** -> _Prechecks Failed_  
 - **2** -> _Backup Failed_  

### Colors:
 - **Green:**  _Success_  
 - **Blue:**  _Info_  
 - **Yellow:**  _Help_  
 - **DarkYellow:**  _Warning_ | _Danger_ | _Deleting_  
 - **Red:**  _Error_ | _Invalid syntax/params_  
  
  
   
# Backup Config File
Default value: backup-config.yaml  
Can be overriden with -C, --config parameter  

If a relative path is specified, the script looks for the config file in the working directory and the directory where the executable is located, respectively.    
Can process JSON, or YAML format. (Determined by extension)

## Backup methods
 - all  
   - Backs up whole directory with saves.  
   - Used when there is no good way to backup each save individualy.  
 - map  
   - Used to create mapping TODO  
 - listd  
   - TODO  
 - listf  
   - TODO  

# Backup Database 
> backup-db.json

Located in the same place as the executable.
Created if not found.  
Stores LastWriteTime of the backed up files/folders.


# TAB Completition
> Register-ArgumentCompleter -Native -CommandName backup -ScriptBlock {  
>   param($wordToComplete)  
>   backup list_parameters_plain | where {$_ -like "$wordToComplete*"}  
> }  
