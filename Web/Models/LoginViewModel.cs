using System.ComponentModel.DataAnnotations;

namespace CourierService.Web.Models
{
    public class LoginViewModel
    {
        // AuthService looks users up by username only, so the label says username
        [Required(ErrorMessage = "Username is required.")]
        [Display(Name = "Username")]
        public string UsernameOrEmail { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }
}