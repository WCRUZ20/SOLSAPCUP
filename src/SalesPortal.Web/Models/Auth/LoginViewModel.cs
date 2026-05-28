using System.ComponentModel.DataAnnotations;

namespace SalesPortal.Web.Models.Auth
{
    public sealed class LoginViewModel
    {
        [Required(ErrorMessage = "Ingrese su usuario o correo electrónico.")]
        [Display(Name = "Usuario o correo")]
        public string UserOrEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingrese su contraseña.")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string Password { get; set; } = string.Empty;
    }
}
