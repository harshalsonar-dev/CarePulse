using System.Text;
using CarePulse.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CarePulse.Tests
{
    public class AzureBlobStorageServiceTests : IDisposable
    {
        private readonly string _testWebRoot;

        public AzureBlobStorageServiceTests()
        {
            _testWebRoot = Path.Combine(Path.GetTempPath(), "CarePulseTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testWebRoot);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testWebRoot))
            {
                try { Directory.Delete(_testWebRoot, recursive: true); } catch { }
            }
        }

        [Fact]
        public async Task UploadMedicalDocument_LocalFallback_SavesFileAndReturnsRelativeUrl()
        {
            var mockEnv = new Mock<IWebHostEnvironment>();
            mockEnv.Setup(e => e.WebRootPath).Returns(_testWebRoot);

            var configDict = new Dictionary<string, string?>
            {
                { "AzureStorage:ConnectionString", "" }, // No cloud connection string
                { "AzureStorage:ContainerName", "test-records" }
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();

            var service = new AzureBlobStorageService(config, mockEnv.Object, NullLogger<AzureBlobStorageService>.Instance);

            var testData = Encoding.UTF8.GetBytes("Clinical Radiology Report Content");
            using var stream = new MemoryStream(testData);

            var result = await service.UploadMedicalDocumentAsync(stream, "chest-xray.pdf", "application/pdf");

            Assert.True(result.Success);
            Assert.StartsWith("/uploads/records/", result.FileUrl);
            Assert.EndsWith(".pdf", result.BlobName);
            Assert.Equal("application/pdf", result.ContentType);
            Assert.Equal(testData.Length, result.FileSize);

            var expectedLocalPath = Path.Combine(_testWebRoot, "uploads", "records", result.BlobName);
            Assert.True(File.Exists(expectedLocalPath));
        }

        [Fact]
        public async Task DeleteDocument_ExistingLocalFile_DeletesPhysicalFileAndReturnsTrue()
        {
            var mockEnv = new Mock<IWebHostEnvironment>();
            mockEnv.Setup(e => e.WebRootPath).Returns(_testWebRoot);

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
            var service = new AzureBlobStorageService(config, mockEnv.Object, NullLogger<AzureBlobStorageService>.Instance);

            // Create sample file
            var uploadsDir = Path.Combine(_testWebRoot, "uploads", "records");
            Directory.CreateDirectory(uploadsDir);
            var testFileName = "test-delete.pdf";
            var filePath = Path.Combine(uploadsDir, testFileName);
            await File.WriteAllTextAsync(filePath, "dummy content");

            var deleted = await service.DeleteDocumentAsync(testFileName);

            Assert.True(deleted);
            Assert.False(File.Exists(filePath));
        }

        [Fact]
        public async Task DeleteDocument_NonExistentFile_ReturnsFalse()
        {
            var mockEnv = new Mock<IWebHostEnvironment>();
            mockEnv.Setup(e => e.WebRootPath).Returns(_testWebRoot);

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
            var service = new AzureBlobStorageService(config, mockEnv.Object, NullLogger<AzureBlobStorageService>.Instance);

            var deleted = await service.DeleteDocumentAsync("nonexistent-file.pdf");
            Assert.False(deleted);
        }
    }
}
