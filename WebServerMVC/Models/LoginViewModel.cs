using System.ComponentModel.DataAnnotations;

namespace WebServerMVC.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "사용자명을 입력해주세요.")]
        [Display(Name = "사용자명")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "비밀번호를 입력해주세요.")]
        [DataType(DataType.Password)]
        [Display(Name = "비밀번호")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "로그인 상태 유지")]
        public bool RememberMe { get; set; }
    }
}