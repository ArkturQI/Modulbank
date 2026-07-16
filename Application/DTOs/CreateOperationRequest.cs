using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

public class CreateOperationRequest
{
    [Required(ErrorMessage = "OperationId is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "OperationId must be between 1 and 100 characters")]
    public string OperationId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Amount is required")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Currency is required")]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Currency must be exactly 3 characters (e.g., RUB)")]
    public string Currency { get; set; } = "RUB";
}