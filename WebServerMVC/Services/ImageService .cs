// WebServerMVC/Services/ImageService.cs 수정
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using WebServerMVC.Models;
using WebServerMVC.Services.Interfaces;
using System.Security.Cryptography;
using System.Linq;
using System.Collections.Concurrent;

namespace WebServerMVC.Services
{
    public class ImageService : IImageService
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly string _imagesPath;
        private readonly string _thumbnailsPath;
        private readonly string _clientImagesPath;
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _clientLocks = new ConcurrentDictionary<string, SemaphoreSlim>();

        public ImageService(IWebHostEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
            _imagesPath = Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot", "uploads", "images");
            _thumbnailsPath = Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot", "uploads", "thumbnails");
            _clientImagesPath = Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot", "uploads", "clients");

            // 디렉토리가 없으면 생성
            EnsureDirectoriesExist();
        }

        private void EnsureDirectoriesExist()
        {
            if (!Directory.Exists(_imagesPath))
            {
                Directory.CreateDirectory(_imagesPath);
            }

            if (!Directory.Exists(_thumbnailsPath))
            {
                Directory.CreateDirectory(_thumbnailsPath);
            }

            if (!Directory.Exists(_clientImagesPath))
            {
                Directory.CreateDirectory(_clientImagesPath);
            }
        }

        public async Task<ImageMessage> SaveImage(string senderId, string matchId, IFormFile imageFile)
        {
            // 클라이언트별 폴더 생성
            string clientFolder = Path.Combine(_clientImagesPath, senderId);
            if (!Directory.Exists(clientFolder))
            {
                Directory.CreateDirectory(clientFolder);
            }

            // 파일 내용의 해시 계산 (중복 체크용)
            string fileHash = await ComputeFileHashAsync(imageFile);

            // 클라이언트별 락 획득
            var clientLock = _clientLocks.GetOrAdd(senderId, new SemaphoreSlim(1, 1));
            await clientLock.WaitAsync();

            try
            {
                // 이미지 ID 생성
                string imageId = Guid.NewGuid().ToString();

                // 원본 파일 확장자 가져오기
                string extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();

                // 허용되는 확장자 확인
                if (extension != ".jpg" && extension != ".jpeg" && extension != ".png" && extension != ".gif")
                {
                    throw new ArgumentException("지원되지 않는 이미지 형식입니다.");
                }

                // 중복 이미지 확인
                string existingImagePath = await CheckForDuplicateImageAsync(clientFolder, fileHash);
                string fileName;
                string thumbnailName;

                if (existingImagePath != null)
                {
                    // 중복된 이미지 사용
                    fileName = Path.GetFileName(existingImagePath);
                    imageId = Path.GetFileNameWithoutExtension(fileName);
                    thumbnailName = $"{imageId}_thumb.jpg";
                }
                else
                {
                    // 새 이미지 저장
                    fileName = $"{imageId}{extension}";
                    thumbnailName = $"{imageId}_thumb.jpg";

                    // 클라이언트 폴더에 파일 저장
                    string clientImagePath = Path.Combine(clientFolder, fileName);
                    string clientImageHashPath = Path.Combine(clientFolder, $"{fileHash}.hash");

                    // 원본 이미지 저장
                    using (var stream = new FileStream(clientImagePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(stream);
                    }

                    // 해시 파일 저장 (중복 체크용)
                    await File.WriteAllTextAsync(clientImageHashPath, imageId);

                    // 공개 이미지 폴더에 복사
                    string imagePath = Path.Combine(_imagesPath, fileName);
                    string thumbnailPath = Path.Combine(_thumbnailsPath, thumbnailName);
                    File.Copy(clientImagePath, imagePath, true);

                    // 썸네일 생성 및 저장
                    await CreateThumbnailAsync(imagePath, thumbnailPath);
                }

                // 웹에서 접근 가능한 URL 경로 생성
                string fileUrl = $"/uploads/images/{fileName}";
                string thumbnailUrl = $"/uploads/thumbnails/{thumbnailName}";

                // 이미지 메시지 객체 생성
                var imageMessage = new ImageMessage
                {
                    Id = imageId,
                    SenderId = senderId,
                    MatchId = matchId,
                    FileName = fileName,
                    FileUrl = fileUrl,
                    ThumbnailUrl = thumbnailUrl,
                    FileSize = imageFile.Length,
                    SentAt = DateTime.UtcNow
                };

                return imageMessage;
            }
            finally
            {
                clientLock.Release();
            }
        }

        private async Task<string> ComputeFileHashAsync(IFormFile file)
        {
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                ms.Position = 0;

                using (var sha256 = SHA256.Create())
                {
                    byte[] hashBytes = sha256.ComputeHash(ms);
                    return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        private async Task<string> CheckForDuplicateImageAsync(string clientFolder, string fileHash)
        {
            string hashFilePath = Path.Combine(clientFolder, $"{fileHash}.hash");

            if (File.Exists(hashFilePath))
            {
                // 해시 파일이 존재하면 이미지 ID 읽기
                string imageId = await File.ReadAllTextAsync(hashFilePath);

                // 실제 이미지 파일 찾기
                var imageFiles = Directory.GetFiles(clientFolder, $"{imageId}.*")
                    .Where(f => !f.EndsWith(".hash"))
                    .FirstOrDefault();

                return imageFiles;
            }

            return null;
        }

        private async Task CreateThumbnailAsync(string imagePath, string thumbnailPath)
        {
            using (var image = await Image.LoadAsync(imagePath))
            {
                // 썸네일 크기 조정 (300px 너비)
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(300, 0),
                    Mode = ResizeMode.Max
                }));

                // 압축 옵션 설정
                var encoder = new JpegEncoder
                {
                    Quality = 75 // 압축률 설정 (0-100)
                };

                // 썸네일 저장
                await image.SaveAsJpegAsync(thumbnailPath, encoder);
            }
        }

        public async Task<byte[]> GetImageBytes(string imageId)
        {
            var files = Directory.GetFiles(_imagesPath, $"{imageId}*");
            if (files.Length == 0)
            {
                return null;
            }

            return await File.ReadAllBytesAsync(files[0]);
        }

        public async Task<byte[]> GetThumbnailBytes(string imageId)
        {
            string thumbnailPath = Path.Combine(_thumbnailsPath, $"{imageId}_thumb.jpg");
            if (!File.Exists(thumbnailPath))
            {
                return null;
            }

            return await File.ReadAllBytesAsync(thumbnailPath);
        }

        public async Task DeleteImage(string imageId)
        {
            var files = Directory.GetFiles(_imagesPath, $"{imageId}*");
            foreach (var file in files)
            {
                File.Delete(file);
            }

            string thumbnailPath = Path.Combine(_thumbnailsPath, $"{imageId}_thumb.jpg");
            if (File.Exists(thumbnailPath))
            {
                File.Delete(thumbnailPath);
            }

            // 클라이언트 폴더들에서도 찾아서 삭제
            await Task.Run(() =>
            {
                foreach (var clientDir in Directory.GetDirectories(_clientImagesPath))
                {
                    var clientFiles = Directory.GetFiles(clientDir, $"{imageId}*");
                    foreach (var file in clientFiles)
                    {
                        File.Delete(file);
                    }

                    // 연결된 해시 파일도 찾아서 삭제
                    var hashFiles = Directory.GetFiles(clientDir, "*.hash");
                    foreach (var hashFile in hashFiles)
                    {
                        string content = File.ReadAllText(hashFile);
                        if (content == imageId)
                        {
                            File.Delete(hashFile);
                        }
                    }
                }
            });
        }
    }
}