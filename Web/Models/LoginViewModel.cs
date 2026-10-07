using System.ComponentModel.DataAnnotations;

namespace CourierService.Web.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Username or email is required.")]
        [Display(Name = "Username or email")]
        public string UsernameOrEmail { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }
}