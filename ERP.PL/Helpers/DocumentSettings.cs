using ERP.DAL.Models;

namespace ERP.PL.Helpers
{
    public class DocumentSettings
    {
        // Image Upload Settings
        public const int MaxImageUploadSizeInBytes = 10 * 1024 * 1024; // 10 MB
        public static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
        public static readonly string[] AllowedImageMimeTypes = { "image/jpeg", "image/png", "image/gif", "image/bmp" };

        // File signature validation (magic numbers)
        private static readonly Dictionary<string, List<byte[]>> FileSignatures = new()
        {
            { ".jpg", new List<byte[]> { new byte[] { 0xFF, 0xD8, 0xFF } } },
            { ".jpeg", new List<byte[]> { new byte[] { 0xFF, 0xD8, 0xFF } } },
            { ".png", new List<byte[]> { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } } },
            { ".gif", new List<byte[]> {
                new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 }, // GIF87a
                new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }  // GIF89a
            }},
            { ".bmp", new List<byte[]> { new byte[] { 0x42, 0x4D } } }
        };

        // Default Image Path
        public const string DefaultMaleAvatar = "avatar-male.png";
        public const string DefaultFemaleAvatar = "avatar-female.png";
        public const string DefaultUserAvatar = "avatar-user.png";

        // Refactor to use IwebHostEnvironment in future if needed
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<DocumentSettings> _logger;

        public DocumentSettings(IWebHostEnvironment env, ILogger<DocumentSettings> logger)
        {
            _env=env;
            _logger=logger;
        }

        /// <summary>
        /// Upload image with comprehensive validation
        /// </summary>
        public async Task<string> UploadImagePath(IFormFile file, string folderName)
        {
            // VALIDATION 1: Null/empty check
            if (file == null || file.Length == 0)
            {
                throw new ArgumentException("No file was uploaded.");
            }

            // VALIDATION 2: File size check (before reading entire file)
            if (file.Length > MaxImageUploadSizeInBytes)
            {
                var maxSizeMB = MaxImageUploadSizeInBytes / (1024.0 * 1024.0);
                throw new ArgumentException($"File size exceeds maximum limit of {maxSizeMB:F1} MB.");
            }

            // VALIDATION 3: Extension validation from filename
            var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !AllowedImageExtensions.Contains(extension))
            {
                throw new ArgumentException(
                    $"Invalid file type. Allowed types: {string.Join(", ", AllowedImageExtensions)}");
            }

            // VALIDATION 4: Content-Type validation (defense in depth)
            var allowedContentTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/bmp" };
            if (!allowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
            {
                throw new ArgumentException(
                    $"Invalid content type '{file.ContentType}'. Expected image file.");
            }

            // VALIDATION 5: File signature validation (magic numbers)
            if (!await ValidateFileSignatureAsync(file, extension))
            {
                throw new ArgumentException(
                    "File content does not match the expected format. The file may be corrupted or renamed.");
            }

            // SECURITY: Sanitize folder name (prevent path traversal)
            if (string.IsNullOrWhiteSpace(folderName) ||
                folderName.Contains("..") ||
                folderName.Contains('/') ||
                folderName.Contains('\\') ||
                folderName.Contains(':'))
            {
                throw new ArgumentException("Invalid folder name.");
            }

            // SECURITY: Create directory structure securely
            var uploadsRootFolder = Path.Combine(_env.WebRootPath, "uploads", folderName);

            try
            {
                if (!Directory.Exists(uploadsRootFolder))
                {
                    Directory.CreateDirectory(uploadsRootFolder);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create upload directory: {Directory}", uploadsRootFolder);
                throw new InvalidOperationException("Failed to prepare upload directory.");
            }

            // SECURITY: Generate cryptographically secure random filename
            var uniqueFileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadsRootFolder, uniqueFileName);

            // SECURITY: Additional check - ensure file doesn't already exist
            if (File.Exists(filePath))
            {
                // Extremely unlikely with GUID, but be defensive
                uniqueFileName = $"{Guid.NewGuid():N}_{DateTime.UtcNow.Ticks}{extension}";
                filePath = Path.Combine(uploadsRootFolder, uniqueFileName);
            }

            try
            {
                // SECURITY: Save file with restricted permissions
                using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await file.CopyToAsync(stream);
                }

                _logger.LogInformation("File uploaded successfully: {FileName}", uniqueFileName);
                return uniqueFileName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save uploaded file: {FileName}", uniqueFileName);

                // Cleanup on failure
                if (File.Exists(filePath))
                {
                    try { File.Delete(filePath); } catch { /* Ignore cleanup errors */ }
                }

                throw new InvalidOperationException("Failed to save the uploaded file.");
            }
        }


        /// <summary>
        /// Delete image file from storage
        /// </summary>
        public void DeleteImage(string fileName, string folderName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return;

            // Validate inputs
            if (fileName.Contains("..") || fileName.Contains("/") || fileName.Contains("\\"))
            {
                _logger.LogWarning("Attempted path traversal in DeleteImage: {FileName}", fileName);
                return;
            }

            if (folderName.Contains("..") || folderName.Contains("/") || folderName.Contains("\\"))
            {
                _logger.LogWarning("Attempted path traversal in DeleteImage folder: {FolderName}", folderName);
                return;
            }

            try
            {
                var filePath = Path.Combine(_env.WebRootPath, "uploads", folderName, fileName);

                //  Ensure file is within allowed directory
                var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads", folderName);
                var fullPath = Path.GetFullPath(filePath);

                if (!fullPath.StartsWith(Path.GetFullPath(uploadsRoot)))
                {
                    _logger.LogWarning("Attempted to delete file outside uploads directory: {FilePath}", filePath);
                    return;
                }

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("File deleted successfully: {FileName}", fileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete image: {FileName}", fileName);
                // Don't throw - deletion failure shouldn't break the application
            }
        }

        /// <summary>
        /// Get default avatar based on gender
        /// </summary>
        public string GetDefaultAvatarByGender(Gender gender)
        {
            return gender switch
            {
                Gender.Male => DefaultMaleAvatar,
                Gender.Female => DefaultFemaleAvatar,
                _ => DefaultUserAvatar
            };
        }


        /// <summary>
        /// Check if the given image URL is a default avatar
        /// </summary>
        public bool IsDefaultAvatar(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl))
                return false; // Defensive check (should never happen)

            return imageUrl == DefaultMaleAvatar ||
                   imageUrl == DefaultFemaleAvatar ||
                   imageUrl == DefaultUserAvatar;
        }

        /// <summary>
        /// Validate file signature (magic numbers) to prevent file type spoofing
        /// </summary>
        private Task<bool> ValidateFileSignatureAsync(IFormFile file, string extension)
        {
            if (!FileSignatures.TryGetValue(extension, out var validSignatures))
            {
                return Task.FromResult(false); // Unknown extension
            }

            try
            {
                using var reader = new BinaryReader(file.OpenReadStream());

                // Read enough bytes to check all possible signatures
                var maxSignatureLength = validSignatures.Max(s => s.Length);
                var headerBytes = reader.ReadBytes(maxSignatureLength);

                // Check if any valid signature matches
                var isValid = validSignatures.Any(signature =>
                    headerBytes.Take(signature.Length).SequenceEqual(signature));

                // Reset stream position for subsequent reads
                file.OpenReadStream().Position = 0;

                return Task.FromResult(isValid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate file signature");
                return Task.FromResult(false);
            }
        }
    }
}
