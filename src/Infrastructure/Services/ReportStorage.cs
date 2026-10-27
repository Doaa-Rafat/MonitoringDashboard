using System.Text;
using Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services;

public class ReportStorageOptions
{
    public const string SectionName = "Reports";
    public string StoragePath { get; set; } = "App_Data/reports";
}

public class LocalReportFileStore(IOptions<ReportStorageOptions> options) : IReportFileStore
{
    private readonly string _root = options.Value.StoragePath;

    public async Task<string> SaveAsync(int reportId, string content, CancellationToken ct)
    {
        Directory.CreateDirectory(_root);
        var fileName = $"report-{reportId}-{Guid.NewGuid():N}.html";
        var finalPath = Path.Combine(_root, fileName);
        var tempPath = finalPath + ".tmp";

        await File.WriteAllTextAsync(tempPath, content, Encoding.UTF8, ct);
        File.Move(tempPath, finalPath);            // never leave a half-written report visible
        return fileName;
    }

    public Stream? OpenRead(string fileName)
    {
        var path = Path.Combine(_root, Path.GetFileName(fileName));   // never trust a stored path
        return File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
    }
}