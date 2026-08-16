namespace Notes.Api.Services;

public interface IFileStorageService
{
    Task<(string FilePath, string FileName, long Size)> SaveAsync(IFormFile file, int noteId);

    Task<(Stream Stream, string ContentType, string FileName)> GetAsync(string filePath);

    void Delete(string filePath);
}

public class FileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _env;

    public FileStorageService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public async Task<(string FilePath, string FileName, long Size)> SaveAsync(IFormFile file, int noteId)
    {
        var uploadsRoot = Path.Combine(_env.ContentRootPath, "Uploads");
        Directory.CreateDirectory(uploadsRoot);

        var noteFolder = Path.Combine(uploadsRoot, $"note_{noteId}");
        Directory.CreateDirectory(noteFolder);

        var safeName = Path.GetFileName(file.FileName);
        var uniqueName = $"{Guid.NewGuid():N}_{safeName}";
        var fullPath = Path.Combine(noteFolder, uniqueName);

        await using var stream = File.Create(fullPath);
        await file.CopyToAsync(stream);

        return (fullPath, safeName, file.Length);
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> GetAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("附件不存在", filePath);
        }

        var stream = File.OpenRead(filePath);
        var fileName = Path.GetFileName(filePath);
        var contentType = GetContentType(fileName);

        return await Task.FromResult((stream, contentType, fileName));
    }

    public void Delete(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    private static string GetContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".doc" or ".docx" => "application/msword",
            ".xls" or ".xlsx" => "application/vnd.ms-excel",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }
}
