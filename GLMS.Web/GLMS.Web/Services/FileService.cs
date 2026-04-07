// ─── FILE SERVICE ─────────────────────────────────────────────────────────────
// Handles uploading and validating PDF "Signed Agreement" files for Contracts.
//
// SECURITY: Only .pdf files are accepted — any other file type throws an exception.
// This prevents malicious file uploads (e.g. .exe, .js files).
//
// STORAGE: Files are saved to the server's file system (not stored in the database).
// The file path is stored in the Contract record in the database.
//
// UUID NAMING: Files are renamed using a GUID (globally unique identifier)
// to prevent overwrites if two clients upload files with the same name.
// The original filename is stored separately for display/download purposes.
//
// TESTABILITY: IsValidPdf() is a separate method so it can be unit tested
// independently without actually saving files to disk.

namespace GLMS.Web.Services
{
    public interface IFileService
    {
        Task<(string savedPath, string originalName)> SaveContractFileAsync(IFormFile file);
        bool IsValidPdf(IFormFile file);
    }

    public class FileService : IFileService
    {
        private readonly IWebHostEnvironment _env;

        // Whitelist of allowed file extensions — only PDF is permitted
        private static readonly string[] AllowedExtensions = { ".pdf" };

        // IWebHostEnvironment gives us the server's root path
        public FileService(IWebHostEnvironment env) => _env = env;

        /// <summary>
        /// Validates that the uploaded file is a PDF.
        /// Returns false for null, empty, or non-PDF files.
        /// </summary>
        public bool IsValidPdf(IFormFile file)
        {
            if (file == null || file.Length == 0) return false;

            // Extract and normalise the file extension (e.g. ".PDF" → ".pdf")
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            return AllowedExtensions.Contains(ext);
        }

        /// <summary>
        /// Saves the uploaded PDF to the server's Uploads/Agreements folder.
        /// Throws InvalidOperationException if the file is not a valid PDF.
        /// Returns the saved file path and the original filename.
        /// </summary>
        public async Task<(string savedPath, string originalName)> SaveContractFileAsync(IFormFile file)
        {
            // Validate before saving — throws if not a valid PDF
            if (!IsValidPdf(file))
                throw new InvalidOperationException(
                    "Only PDF files are permitted for signed agreements.");

            // Build the upload directory path inside the project's root
            var uploadDir = Path.Combine(_env.ContentRootPath, "Uploads", "Agreements");

            // Create the directory if it doesn't exist
            Directory.CreateDirectory(uploadDir);

            // Generate a unique filename using GUID to prevent overwrites
            // e.g. "3f2504e0-4f89-11d3-9a0c-0305e82c3301.pdf"
            var uniqueName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadDir, uniqueName);

            // Async file write — does not block the thread
            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            // Return both the server path (for storage) and original name (for display)
            return (filePath, file.FileName);
        }
    }
}