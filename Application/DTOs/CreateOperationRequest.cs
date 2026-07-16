using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

public class CreateOperationRequest
{
    // Nullable to allow service-side generation if the client omits it
    public string? OperationId { get; set; }
    
    [Required(ErrorMessage = "Amount is required")]
    public string Amount { get; set; } = string.Empty;

    [Required(ErrorMessage = "Currency is required")]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Currency must be exactly 3 characters (e.g., RUB)")]
    public string Currency { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required")]
    [StringLength(255, ErrorMessage = "Description must not exceed 255 characters")]
    public string Description { get; set; } = string.Empty;
}