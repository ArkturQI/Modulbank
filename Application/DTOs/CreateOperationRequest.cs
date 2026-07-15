using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

public class CreateOperationRequest
{
    [Required(ErrorMessage = "OperationId is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "OperationId must be between 1 and 100 characters")]
    public string? OperationId { get; set; }
}