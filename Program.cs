const string source = @"YOUR_SOURCE_ROOT_DIRECTORY";
const string destination = @"YOUR_DESTINATION_ROOT_DIRECTORY";
const int maxRetries = 3;
const int msRetryDelay = 5000;


FileSystemWatcher watcher = new FileSystemWatcher(){
    Path = source,
    Filter = "*.*",
    IncludeSubdirectories = true,
    EnableRaisingEvents = true,
    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
    InternalBufferSize = 64 * 1024
};

watcher.Created += (_, e) => Task.Run(() => handleFileOperation(e.FullPath, e.ChangeType));
watcher.Changed += (_, e) => Task.Run(() => handleFileOperation(e.FullPath, e.ChangeType));
watcher.Deleted += (_, e) => Task.Run(() => handleFileOperation(e.FullPath, e.ChangeType));
watcher.Renamed += (_, e) => Task.Run(() => handleFileOperation(e.OldFullPath, e.ChangeType, e.FullPath));
await Task.Delay(Timeout.Infinite);


static async Task handleFileOperation(string fullPath, WatcherChangeTypes operation,
    string? newFullPath = null)
{
    bool isDir = !Path.HasExtension(fullPath);
    if (operation == WatcherChangeTypes.Changed && isDir)
    {
        return;
    }
    await Task.Delay(1000);
    int retryCount = 0;
    while (retryCount < maxRetries)
    {
        try
        {
            string join(string s)
                => string.Concat(destination, s.Replace(source, null));

            string destinationPath = join(fullPath);

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
                        await renameAsync(destinationPath, join(newFullPath!));
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
                    Directory.Move(destinationPath, join(newFullPath!));
                    break;
            }
        }
        catch (Exception ex)
        {
            await Task.Delay(msRetryDelay);
            retryCount++;
        }
    }
    
    
    static async Task copyAsync(string sourceFile, string destFile)
    {
        await using FileStream destStream = new(destFile, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
        await using FileStream sourceStream = new(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
        await sourceStream.CopyToAsync(destStream);
    }

    static async Task renameAsync(string sourceFile, string destFile)
    {
        await copyAsync(sourceFile, destFile);
        File.Delete(sourceFile);
    }
}