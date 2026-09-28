using StudentCenter.ApplicationService.Application.Interfaces;

namespace StudentCenter.ApplicationService.Infrastructure.Storage;

// Files are private: never expose this directory through static-file middleware.
public sealed class LocalDocumentStorage : IDocumentStorage
{
    private readonly string root;

    public LocalDocumentStorage(IWebHostEnvironment environment)
    {
        root = Path.Combine(environment.ContentRootPath, "App_Data", "Documents");
    }

    public async Task<string> SaveAsync(byte[] content, CancellationToken ct)
    {
        Directory.CreateDirectory(root);
        var reference = Guid.NewGuid().ToString("N");
        var path = Resolve(reference);
        try
        {
            await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            await stream.WriteAsync(content, ct);
            return reference;
        }
        catch
        {
            File.Delete(path);
            throw;
        }
    }

    public Task<Stream> OpenAsync(string reference, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<Stream>(new FileStream(Resolve(reference), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true));
    }

    public Task DeleteAsync(string reference, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        File.Delete(Resolve(reference));
        return Task.CompletedTask;
    }

    private string Resolve(string reference)
    {
        if (!Guid.TryParseExact(reference, "N", out var id))
            throw new ArgumentException("Invalid storage reference.");
        return Path.Combine(root, id.ToString("N"));
    }
}
