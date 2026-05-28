using System.ComponentModel.DataAnnotations;

namespace SalesPortal.Web.Models.Auth
{
    public sealed class ChangePasswordViewModel
    {
        [Required]
        public string CardCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingrese la nueva contraseña.")]
        [MinLength(8, ErrorMessage = "La contraseña debe tener mínimo 8 caracteres.")]
        [DataType(DataType.Password)]
        [Display(Name = "Nueva contraseña")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirme la nueva contraseña.")]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Las contraseñas no coinciden.")]
        [Display(Name = "Confirmar contraseña")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
