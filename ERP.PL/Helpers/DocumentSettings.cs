using ERP.DAL.Models;

namespace ERP.PL.Helpers
{
    public class DocumentSettings
    {
        // Image Upload Settings
        public const int MaxImageUploadSizeInBytes = 10 * 1024 * 1024; // 10 MB
        public static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
        public static readonly string[] AllowedImageMimeTypes = { "image/jpeg", "image/png", "image/gif", "image/bmp" };

        // Default Image Path
        public const string DefaultMaleAvatar = "avatar-male.png";
        public const string DefaultFemaleAvatar = "avatar-female.png";
        public const string DefaultUserAvatar = "avatar-user.png";

        // Refactor to use IwebHostEnvironment in future if needed
        private readonly IWebHostEnvironment _env;

        public DocumentSettings(IWebHostEnvironment env)
        {
            _env=env;
        }

        public async Task<string> UploadImagePath(IFormFile file, string folderName)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("Invalid file");

            if (file.Length > MaxImageUploadSizeInBytes)
                throw new ArgumentException("File size exceeds limit");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedImageExtensions.Contains(extension))
                throw new ArgumentException("Invalid file extension");

            if (!AllowedImageMimeTypes.Contains(file.ContentType))
                throw new ArgumentException("Invalid MIME type");

            var uploadsRootFolder = Path.Combine(
                _env.WebRootPath,
                "uploads",
                folderName
            );

            if (!Directory.Exists(uploadsRootFolder))
                Directory.CreateDirectory(uploadsRootFolder);

            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsRootFolder, uniqueFileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return uniqueFileName; // store only filename in DB
        }


        public void DeleteImage(string fileName, string folderName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return;

            string filePath = Path.Combine(
                _env.WebRootPath,
                "uploads",
                folderName,
                fileName
            );

            if (File.Exists(filePath))
                File.Delete(filePath);
        }

        public string GetDefaultAvatarByGender(Gender gender)
        {
            return gender switch
            {
                Gender.Male => DefaultMaleAvatar,
                Gender.Female => DefaultFemaleAvatar,
                _ => DefaultUserAvatar
            };
        }

        public bool IsDefaultAvatar(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl))
                return false; // Defensive check (should never happen)

            return imageUrl == DefaultMaleAvatar ||
                   imageUrl == DefaultFemaleAvatar ||
                   imageUrl == DefaultUserAvatar;
        }
    }
}
