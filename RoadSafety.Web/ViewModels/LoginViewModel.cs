using System.ComponentModel.DataAnnotations;

namespace RoadSafety.Web.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Force number is required")]
    [Display(Name = "Force Number")]
    public string ForceNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}
