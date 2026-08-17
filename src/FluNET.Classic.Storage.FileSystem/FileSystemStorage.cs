using FluNET.Classic.Core;
using FluNET.Classic.Storage;

namespace FluNET.Classic.Storage.FileSystem;

public sealed class FileSystemStorageModule : LanguageModule
{
    public override string Name => "storage.filesystem";
    public override IReadOnlyCollection<string> Dependencies => new[] { "storage" };
}

public sealed class FileSystemStorageProvider : IStorageProvider
{
    private readonly string _root;
    public FileSystemStorageProvider(DirectoryInfo root) { root.Create(); _root = Path.GetFullPath(root.FullName); }

    public async ValueTask<byte[]?> GetAsync(StorageKey key, CancellationToken cancellationToken = default)
    {
        string path = Resolve(key); if (!File.Exists(path)) return null; return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
    }
    public async ValueTask SaveAsync(StorageKey key, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        string path = Resolve(key); Directory.CreateDirectory(Path.GetDirectoryName(path)!); await File.WriteAllBytesAsync(path, data.ToArray(), cancellationToken).ConfigureAwait(false);
    }
    public ValueTask<bool> DeleteAsync(StorageKey key, CancellationToken cancellationToken = default)
    {
        string path = Resolve(key); bool exists = File.Exists(path); if (exists) File.Delete(path); return ValueTask.FromResult(exists);
    }
    public ValueTask<IReadOnlyList<StorageObject>> ListAsync(StorageContainer container, CancellationToken cancellationToken = default)
    {
        string directory = ResolveContainer(container); if (!Directory.Exists(directory)) return ValueTask.FromResult<IReadOnlyList<StorageObject>>(Array.Empty<StorageObject>());
        StorageObject[] result = Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Select(path => { var file = new FileInfo(path); string relative = Path.GetRelativePath(_root, path).Replace(Path.DirectorySeparatorChar, '/'); return new StorageObject(new StorageKey(relative), new StorageMetadata(file.Length, file.LastWriteTimeUtc)); }).ToArray();
        return ValueTask.FromResult<IReadOnlyList<StorageObject>>(result);
    }
    public async ValueTask CopyAsync(StorageKey source, StorageKey destination, CancellationToken cancellationToken = default) { byte[] data = await GetAsync(source, cancellationToken).ConfigureAwait(false) ?? throw new FileNotFoundException(source.Value); await SaveAsync(destination, data, cancellationToken).ConfigureAwait(false); }
    public async ValueTask MoveAsync(StorageKey source, StorageKey destination, CancellationToken cancellationToken = default) { await CopyAsync(source, destination, cancellationToken).ConfigureAwait(false); await DeleteAsync(source, cancellationToken).ConfigureAwait(false); }

    private string Resolve(StorageKey key) => ResolvePath(key.Value);
    private string ResolveContainer(StorageContainer container) => ResolvePath(container.Value);
    private string ResolvePath(string relative)
    {
        string path = Path.GetFullPath(Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;
        if (!path.Equals(_root, StringComparison.OrdinalIgnoreCase) && !path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("Storage key escapes the configured root.");
        return path;
    }
}
