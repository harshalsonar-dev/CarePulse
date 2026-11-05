using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CarePulse.Services.Interfaces;

namespace CarePulse.Services
{
    public class AzureBlobStorageService : IAzureBlobStorageService
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<AzureBlobStorageService> _logger;
        private readonly string? _azureConnectionString;
        private readonly string _containerName;

        public AzureBlobStorageService(
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ILogger<AzureBlobStorageService> logger)
        {
            _configuration = configuration;
            _environment = environment;
            _logger = logger;
            _azureConnectionString = _configuration["AzureStorage:ConnectionString"];
            _containerName = _configuration["AzureStorage:ContainerName"] ?? "carepulse-medical-records";
        }

        public async Task<BlobUploadResult> UploadMedicalDocumentAsync(Stream fileStream, string originalFileName, string contentType)
        {
            try
            {
                var extension = Path.GetExtension(originalFileName);
                var uniqueBlobName = $"{Guid.NewGuid():N}_{DateTime.UtcNow:yyyyMMddHHmmss}{extension}";

                // 1. If Azure Connection string is configured and valid, use Azure Blob Storage
                if (!string.IsNullOrWhiteSpace(_azureConnectionString) && 
                    !_azureConnectionString.Contains("UseDevelopmentStorage=true") &&
                    !_azureConnectionString.Contains("your-azure-connection-string"))
                {
                    _logger.LogInformation("Uploading {FileName} to Azure Blob Storage container {ContainerName}", originalFileName, _containerName);

                    var blobServiceClient = new BlobServiceClient(_azureConnectionString);
                    var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
                    await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

                    var blobClient = containerClient.GetBlobClient(uniqueBlobName);
                    await blobClient.UploadAsync(fileStream, new BlobHttpHeaders { ContentType = contentType });

                    return new BlobUploadResult
                    {
                        Success = true,
                        BlobName = uniqueBlobName,
                        FileUrl = blobClient.Uri.ToString(),
                        FileSize = fileStream.Length,
                        ContentType = contentType
                    };
                }

                // 2. Fallback: Local Storage under wwwroot/uploads/records/
                _logger.LogInformation("Azure Blob Storage connection string not configured; using local resilient storage fallback for {FileName}.", originalFileName);

                var uploadsDir = Path.Combine(_environment.WebRootPath, "uploads", "records");
                if (!Directory.Exists(uploadsDir))
                {
                    Directory.CreateDirectory(uploadsDir);
                }

                var physicalPath = Path.Combine(uploadsDir, uniqueBlobName);
                using (var destStream = new FileStream(physicalPath, FileMode.Create))
                {
                    if (fileStream.CanSeek)
                    {
                        fileStream.Position = 0;
                    }
                    await fileStream.CopyToAsync(destStream);
                }

                var localRelativeUrl = $"/uploads/records/{uniqueBlobName}";

                return new BlobUploadResult
                {
                    Success = true,
                    BlobName = uniqueBlobName,
                    FileUrl = localRelativeUrl,
                    FileSize = fileStream.Length,
                    ContentType = contentType
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload medical document {FileName}", originalFileName);
                return new BlobUploadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<bool> DeleteDocumentAsync(string blobName)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_azureConnectionString) && 
                    !_azureConnectionString.Contains("your-azure-connection-string"))
                {
                    var blobServiceClient = new BlobServiceClient(_azureConnectionString);
                    var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
                    var blobClient = containerClient.GetBlobClient(blobName);
                    return await blobClient.DeleteIfExistsAsync();
                }

                var uploadsDir = Path.Combine(_environment.WebRootPath, "uploads", "records");
                var physicalPath = Path.Combine(uploadsDir, blobName);
                if (File.Exists(physicalPath))
                {
                    File.Delete(physicalPath);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete blob {BlobName}", blobName);
                return false;
            }
        }
    }
}
