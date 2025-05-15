// Services/ImageService.cs
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

namespace WebServerMVC.Services
{
    public class ImageService : IImageService
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly string _imagesPath;
        private readonly string _thumbnailsPath;

        public ImageService(IWebHostEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
            _imagesPath = Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot", "uploads", "images");
            _thumbnailsPath = Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot", "uploads", "thumbnails");

            // 디렉토리가 없으면 생성
            if (!Directory.Exists(_imagesPath))
            {
                Directory.CreateDirectory(_imagesPath);
            }

            if (!Directory.Exists(_thumbnailsPath))
            {
                Directory.CreateDirectory(_thumbnailsPath);
            }
        }

        public async Task<ImageMessage> SaveImage(string senderId, string matchId, IFormFile imageFile)
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

            // 저장할 파일 이름 생성
            string fileName = $"{imageId}{extension}";
            string thumbnailName = $"{imageId}_thumb.jpg";

            // 원본 이미지 저장 경로
            string imagePath = Path.Combine(_imagesPath, fileName);
            string thumbnailPath = Path.Combine(_thumbnailsPath, thumbnailName);

            // 원본 이미지 저장
            using (var stream = new FileStream(imagePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            // 썸네일 생성 및 저장 (이미지 압축 포함)
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

        public Task DeleteImage(string imageId)
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

            return Task.CompletedTask;
        }
    }
}