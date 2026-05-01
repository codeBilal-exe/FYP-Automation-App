using FYP_AutomationSystem.Data;
using FYP_AutomationSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace FYP_AutomationSystem.Services
{
    public class DocumentService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private const long MaxFileSize = 20 * 1024 * 1024; // 20MB

        public DocumentService(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        /// <summary>
        /// Uploads a document to the server and stores metadata in database
        /// </summary>
        public async Task<Document?> UploadDocument(IFormFile file, int projectId, int uploadedById)
        {
            try
            {
                // Validate file
                if (file == null || file.Length == 0)
                    return null;

                if (!ValidateFileSize(file, MaxFileSize))
                    return null;

                // Verify project and user exist
                var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == uploadedById);

                if (project == null || user == null)
                    return null;

                // Create upload directory if it doesn't exist
                var uploadDir = Path.Combine(_environment.WebRootPath, "uploads", projectId.ToString());
                if (!Directory.Exists(uploadDir))
                    Directory.CreateDirectory(uploadDir);

                // Generate unique filename
                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
                var filePath = Path.Combine(uploadDir, fileName);
                var relativePath = Path.Combine("uploads", projectId.ToString(), fileName);

                // Save file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Create document record
                var document = new Document
                {
                    FileName = file.FileName,
                    FilePath = relativePath,
                    FileType = Path.GetExtension(file.FileName),
                    FileSizeBytes = file.Length,
                    Version = 1,
                    ProjectId = projectId,
                    UploadedById = uploadedById,
                    UploadedAt = DateTime.UtcNow
                };

                _context.Documents.Add(document);
                await _context.SaveChangesAsync();
                return document;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Upload document error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Retrieves all documents for a project
        /// </summary>
        public async Task<List<Document>> GetDocumentsByProject(int projectId)
        {
            try
            {
                return await _context.Documents
                    .Where(d => d.ProjectId == projectId)
                    .OrderByDescending(d => d.UploadedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get documents by project error: {ex.Message}");
                return new List<Document>();
            }
        }

        /// <summary>
        /// Deletes a document from database and file system
        /// </summary>
        public async Task<bool> DeleteDocument(int id)
        {
            try
            {
                var document = await _context.Documents.FirstOrDefaultAsync(d => d.Id == id);
                if (document == null)
                    return false;

                // Delete file from disk
                var filePath = Path.Combine(_environment.WebRootPath, document.FilePath);
                if (File.Exists(filePath))
                    File.Delete(filePath);

                // Delete from database
                _context.Documents.Remove(document);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Delete document error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Returns the file path for download
        /// </summary>
        public async Task<string?> DownloadDocument(int id)
        {
            try
            {
                var document = await _context.Documents.FirstOrDefaultAsync(d => d.Id == id);
                if (document == null)
                    return null;

                var filePath = Path.Combine(_environment.WebRootPath, document.FilePath);
                if (!File.Exists(filePath))
                    return null;

                return filePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Download document error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Validates if file size is within allowed limit
        /// </summary>
        public bool ValidateFileSize(IFormFile file, long maxBytes)
        {
            try
            {
                return file != null && file.Length > 0 && file.Length <= maxBytes;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Validate file size error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets document by ID
        /// </summary>
        public async Task<Document?> GetDocumentById(int id)
        {
            try
            {
                return await _context.Documents.FirstOrDefaultAsync(d => d.Id == id);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get document by id error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets all documents uploaded by a user
        /// </summary>
        public async Task<List<Document>> GetDocumentsByUser(int userId)
        {
            try
            {
                return await _context.Documents
                    .Where(d => d.UploadedById == userId)
                    .OrderByDescending(d => d.UploadedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get documents by user error: {ex.Message}");
                return new List<Document>();
            }
        }

        /// <summary>
        /// Validates allowed file extensions
        /// </summary>
        public bool ValidateFileType(IFormFile file, string[] allowedExtensions)
        {
            try
            {
                if (file == null)
                    return false;

                var fileExtension = Path.GetExtension(file.FileName).ToLower();
                return allowedExtensions.Contains(fileExtension);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Validate file type error: {ex.Message}");
                return false;
            }
        }
    }
}
