// ─── FILE VALIDATION UNIT TESTS ───────────────────────────────────────────────
// Tests the IsValidPdf() and SaveContractFileAsync() methods in FileService.
//
// WHAT WE TEST:
// - Rejected file types: .exe, .docx, .jpg, .txt
// - Accepted file type: .pdf
// - Null file handling
// - That invalid files throw InvalidOperationException
// - That valid PDFs are actually saved to disk
//
// MOCKING: We use Moq to create a fake IWebHostEnvironment
// so we don't need a real web server to run these tests.
// The mock returns Path.GetTempPath() as the content root
// so files are saved to the system temp folder during testing.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Moq;
using Xunit;
using GLMS.Web.Services;

namespace GLMS.Tests
{
    public class FileValidationTests
    {
        private readonly FileService _sut;

        public FileValidationTests()
        {
            // Mock IWebHostEnvironment — returns temp folder as content root
            var mockEnv = new Mock<IWebHostEnvironment>();
            mockEnv.Setup(e => e.ContentRootPath).Returns(Path.GetTempPath());
            _sut = new FileService(mockEnv.Object);
        }

        // Helper method — creates a fake IFormFile with the given filename
        private IFormFile CreateMockFile(string fileName, string content = "fake content")
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            var stream = new MemoryStream(bytes);
            return new FormFile(stream, 0, bytes.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/octet-stream"
            };
        }

        [Fact]
        public void IsValidPdf_ReturnsFalse_ForExeFile()
        {
            var file = CreateMockFile("malware.exe");
            Assert.False(_sut.IsValidPdf(file));
        }

        [Fact]
        public void IsValidPdf_ReturnsFalse_ForDocxFile()
        {
            var file = CreateMockFile("contract.docx");
            Assert.False(_sut.IsValidPdf(file));
        }

        [Fact]
        public void IsValidPdf_ReturnsFalse_ForJpgFile()
        {
            var file = CreateMockFile("image.jpg");
            Assert.False(_sut.IsValidPdf(file));
        }

        [Fact]
        public void IsValidPdf_ReturnsFalse_ForTxtFile()
        {
            var file = CreateMockFile("notes.txt");
            Assert.False(_sut.IsValidPdf(file));
        }

        [Fact]
        public void IsValidPdf_ReturnsTrue_ForPdfFile()
        {
            var file = CreateMockFile("agreement.pdf");
            Assert.True(_sut.IsValidPdf(file));
        }

        [Fact]
        public void IsValidPdf_ReturnsFalse_ForNullFile()
        {
            // Null file should not crash — just return false
            Assert.False(_sut.IsValidPdf(null!));
        }

        [Fact]
        public async Task SaveContractFileAsync_ThrowsException_ForExeFile()
        {
            var file = CreateMockFile("dangerous.exe");
            // Saving an exe should throw InvalidOperationException
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _sut.SaveContractFileAsync(file));
        }

        [Fact]
        public async Task SaveContractFileAsync_ThrowsException_ForDocxFile()
        {
            var file = CreateMockFile("contract.docx");
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _sut.SaveContractFileAsync(file));
        }

        [Fact]
        public async Task SaveContractFileAsync_SavesFile_ForValidPdf()
        {
            var file = CreateMockFile("test_agreement.pdf", "%PDF-1.4 fake pdf content");
            var (path, name) = await _sut.SaveContractFileAsync(file);

            // File should exist on disk after saving
            Assert.True(File.Exists(path));
            // Original filename should be preserved
            Assert.Equal("test_agreement.pdf", name);

            // Cleanup — delete the test file from temp folder
            File.Delete(path);
        }
    }
}