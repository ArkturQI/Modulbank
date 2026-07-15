using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

public class ReceiptRequest
{
    [Required(ErrorMessage = "OperationId is required")]
    public string OperationId { get; set; } = string.Empty;

    [Required(ErrorMessage = "ProviderPaymentId is required")]
    public string ProviderPaymentId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Result is required")]
    [StringLength(20, ErrorMessage = "Result must not exceed 20 characters")]
    public string Result { get; set; } = string.Empty;
}