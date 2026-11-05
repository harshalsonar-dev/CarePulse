namespace CarePulse.Services.Interfaces
{
    public class BlobUploadResult
    {
        public bool Success { get; set; }
        public string FileUrl { get; set; } = string.Empty;
        public string BlobName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
    }

    public interface IAzureBlobStorageService
    {
        Task<BlobUploadResult> UploadMedicalDocumentAsync(Stream fileStream, string originalFileName, string contentType);
        Task<bool> DeleteDocumentAsync(string blobName);
    }
}
