using System.ComponentModel.DataAnnotations;

namespace SalesPortal.Web.Models.Auth
{
    public sealed class RegisterViewModel
    {
        [Required(ErrorMessage = "Ingrese el número de identificación.")]
        [Display(Name = "Núm. identificación")]
        public string IdentificationNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingrese el primer nombre.")]
        [Display(Name = "Primer nombre")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingrese el primer apellido.")]
        [Display(Name = "Primer apellido")]
        public string FirstLastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingrese el correo electrónico.")]
        [EmailAddress(ErrorMessage = "Ingrese un correo electrónico válido.")]
        [Display(Name = "Correo electrónico")]
        public string Email { get; set; } = string.Empty;
    }
}
