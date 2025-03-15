// TODO: Add console parameters to override config.json
// Move config json and the .exe to C:\Users\%CURRENT_USER%\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup
#if DEBUG
File.Copy(@"..\..\..\config.json", "config.json", true);
#endif
Config config = Json.Net.JsonNet.Deserialize<Config>(File.ReadAllText("config.json"));

string source = config.Source;
string destination = config.Destination;
int maxRetries = config.MaxRetries;
int msRetryDelay = config.RetryDelayMillis;

FileSystemWatcher watcher = new FileSystemWatcher(){
    Path = source,
    Filter = "*.*",
    IncludeSubdirectories = true,
    EnableRaisingEvents = true,
    InternalBufferSize = 64 * 1024
};

watcher.Created += (_, e) => Task.Run(() => handleFileOperation(e.FullPath, e.ChangeType));
watcher.Changed += (_, e) => Task.Run(() => handleFileOperation(e.FullPath, e.ChangeType));
watcher.Deleted += (_, e) => Task.Run(() => handleFileOperation(e.FullPath, e.ChangeType));
watcher.Renamed += (_, e) => Task.Run(() => handleFileOperation(e.OldFullPath, e.ChangeType, e.FullPath));
await Task.Delay(Timeout.Infinite);


async Task handleFileOperation(string fullPath, WatcherChangeTypes operation,
    string? newFullPath = null)
{
    bool isDir = !Path.HasExtension(fullPath);
    if (operation == WatcherChangeTypes.Changed && isDir)// escape directory changed event raised when CRUD ops performed on inner files
    {
        return;
    }
    // TODO: Active event pool by filepath to escape extra Changed events
    await Task.Delay(1000);
    int retryCount = 0;
    while (retryCount < maxRetries)
    {
        try
        {
            string destinationPath = getDestination(fullPath);

            if (!isDir)
            {
                switch (operation)
                {
                    case WatcherChangeTypes.Changed:
                    case WatcherChangeTypes.Created:
                        await copyAsync(fullPath, destinationPath);
                        break;
                    case WatcherChangeTypes.Deleted:
                        File.Delete(destinationPath);
                        break;
                    case WatcherChangeTypes.Renamed:
                        await renameAsync(destinationPath, getDestination(newFullPath!));
                        break;
                }

                break;
            }

            switch (operation)
            {
                case WatcherChangeTypes.Created:
                    Directory.CreateDirectory(destinationPath);
                    break;
                case WatcherChangeTypes.Deleted:
                    Directory.Delete(destinationPath, true);
                    break;
                case WatcherChangeTypes.Renamed:
                    Directory.Move(destinationPath, getDestination(newFullPath!));
                    break;
            }

            break;

            
        }
        catch (FileNotFoundException)
        {
            break;
        }
        catch (DirectoryNotFoundException)
        {
            break;
        }
        catch (Exception e)
        {
            await Task.Delay(msRetryDelay);
            retryCount++;
        }
    }
}

static async Task copyAsync(string sourceFile, string destFile)
{
    await using FileStream destStream = new(
        destFile,
        FileMode.Create, FileAccess.Write, FileShare.None,
        bufferSize: 4096, useAsync: true);
    
    await using FileStream sourceStream = new(
        sourceFile,
        FileMode.Open, FileAccess.Read, FileShare.Read,
        bufferSize: 4096, useAsync: true);
    
    await sourceStream.CopyToAsync(destStream);
}

static async Task renameAsync(string sourceFile, string destFile)
{
    await copyAsync(sourceFile, destFile);
    File.Delete(sourceFile);
}

string getDestination(string s)
    => string.Concat(destination, Path.DirectorySeparatorChar, s.Replace(source, null));