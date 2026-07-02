using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IBulkPolicyInventoryImportService
{
    Task<BulkPolicyInventoryImportStatus> StartAsync(IFormFileCollection files, CancellationToken cancellationToken = default);
    BulkPolicyInventoryImportStatus? GetStatus(string jobId);
}

public class BulkPolicyInventoryImportStatus
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = "queued";
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public int SuccessFiles { get; set; }
    public int FailedFiles { get; set; }
    public int Policies { get; set; }
    public int Rules { get; set; }
    public int Exceptions { get; set; }
    public string? CurrentFile { get; set; }
    public string? Message { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<string> Errors { get; set; } = new();
}

public class BulkPolicyInventoryImportService : IBulkPolicyInventoryImportService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BulkPolicyInventoryImportService> _logger;
    private readonly ConcurrentDictionary<string, BulkPolicyInventoryImportStatus> _jobs = new();

    public BulkPolicyInventoryImportService(
        IServiceScopeFactory scopeFactory,
        ILogger<BulkPolicyInventoryImportService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<BulkPolicyInventoryImportStatus> StartAsync(
        IFormFileCollection files,
        CancellationToken cancellationToken = default)
    {
        if (files == null || files.Count == 0)
        {
            throw new InvalidOperationException("No files uploaded.");
        }

        var jobId = Guid.NewGuid().ToString("N");
        var job = new BulkPolicyInventoryImportStatus
        {
            JobId = jobId,
            Status = "queued",
            TotalFiles = files.Count,
            Message = "Dosyalar alindi. Import siraya eklendi."
        };

        _jobs[jobId] = job;

        var jobDirectory = Path.Combine(Path.GetTempPath(), "dlp-policy-inventory-import", jobId);
        Directory.CreateDirectory(jobDirectory);

        var stagedFiles = new List<(string Path, string FileName)>();
        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            if (file.Length == 0) continue;

            var safeName = Path.GetFileName(file.FileName);
            var stagedPath = Path.Combine(jobDirectory, $"{i + 1:D4}_{safeName}");

            await using var target = File.Create(stagedPath);
            await file.CopyToAsync(target, cancellationToken);
            stagedFiles.Add((stagedPath, safeName));
        }

        if (stagedFiles.Count == 0)
        {
            job.Status = "failed";
            job.Message = "Yuklenen dosyalar bos.";
            job.UpdatedAt = DateTime.UtcNow;
            return job;
        }

        job.TotalFiles = stagedFiles.Count;
        job.UpdatedAt = DateTime.UtcNow;

        _ = Task.Run(() => ProcessJobAsync(jobId, stagedFiles, jobDirectory), CancellationToken.None);

        return Clone(job);
    }

    public BulkPolicyInventoryImportStatus? GetStatus(string jobId)
    {
        return _jobs.TryGetValue(jobId, out var status) ? Clone(status) : null;
    }

    private async Task ProcessJobAsync(
        string jobId,
        List<(string Path, string FileName)> stagedFiles,
        string jobDirectory)
    {
        if (!_jobs.TryGetValue(jobId, out var job)) return;

        job.Status = "running";
        job.Message = "Import basladi.";
        job.UpdatedAt = DateTime.UtcNow;

        try
        {
            foreach (var stagedFile in stagedFiles)
            {
                job.CurrentFile = stagedFile.FileName;
                job.Message = $"{stagedFile.FileName} isleniyor.";
                job.UpdatedAt = DateTime.UtcNow;

                try
                {
                    await using var stream = File.OpenRead(stagedFile.Path);
                    var formFile = new FormFile(stream, 0, stream.Length, "file", stagedFile.FileName)
                    {
                        Headers = new HeaderDictionary(),
                        ContentType = "application/octet-stream"
                    };

                    using var scope = _scopeFactory.CreateScope();
                    var importService = scope.ServiceProvider.GetRequiredService<IPolicyInventoryService>();
                    var result = await importService.ImportFileAsync(formFile);

                    if (result.Success)
                    {
                        job.SuccessFiles++;
                        job.Policies += result.Policies;
                        job.Rules += result.Rules;
                        job.Exceptions += result.Exceptions;
                    }
                    else
                    {
                        job.FailedFiles++;
                        AddError(job, $"{stagedFile.FileName}: {result.Message}");
                    }
                }
                catch (Exception ex)
                {
                    job.FailedFiles++;
                    AddError(job, $"{stagedFile.FileName}: {ex.Message}");
                    _logger.LogError(ex, "Bulk policy inventory import failed for file {FileName} in job {JobId}", stagedFile.FileName, jobId);
                }
                finally
                {
                    job.ProcessedFiles++;
                    job.UpdatedAt = DateTime.UtcNow;
                }
            }

            job.Status = job.FailedFiles == 0 ? "completed" : "completed_with_errors";
            job.CurrentFile = null;
            job.Message = job.FailedFiles == 0
                ? "Toplu import tamamlandi."
                : "Toplu import bazi hatalarla tamamlandi.";
            job.UpdatedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            job.Status = "failed";
            job.Message = ex.Message;
            AddError(job, ex.Message);
            job.UpdatedAt = DateTime.UtcNow;
            _logger.LogError(ex, "Bulk policy inventory import job {JobId} failed", jobId);
        }
        finally
        {
            TryDeleteDirectory(jobDirectory);
        }
    }

    private static void AddError(BulkPolicyInventoryImportStatus job, string error)
    {
        if (job.Errors.Count < 50)
        {
            job.Errors.Add(error);
        }
    }

    private static BulkPolicyInventoryImportStatus Clone(BulkPolicyInventoryImportStatus status)
    {
        return new BulkPolicyInventoryImportStatus
        {
            JobId = status.JobId,
            Status = status.Status,
            TotalFiles = status.TotalFiles,
            ProcessedFiles = status.ProcessedFiles,
            SuccessFiles = status.SuccessFiles,
            FailedFiles = status.FailedFiles,
            Policies = status.Policies,
            Rules = status.Rules,
            Exceptions = status.Exceptions,
            CurrentFile = status.CurrentFile,
            Message = status.Message,
            CreatedAt = status.CreatedAt,
            UpdatedAt = status.UpdatedAt,
            Errors = status.Errors.ToList()
        };
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Temporary import files are best-effort cleanup.
        }
    }
}
