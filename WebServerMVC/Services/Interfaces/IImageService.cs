// Services/Interfaces/IImageService.cs
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using WebServerMVC.Models;

namespace WebServerMVC.Services.Interfaces
{
    public interface IImageService
    {
        Task<ImageMessage> SaveImage(string senderId, string matchId, IFormFile imageFile);
        Task<byte[]> GetImageBytes(string imageId);
        Task<byte[]> GetThumbnailBytes(string imageId);
        Task DeleteImage(string imageId);
    }
}