using System.ComponentModel.DataAnnotations;
using StudentCenter.ApplicationService.Domain.Enums;

namespace StudentCenter.ApplicationService.Application.DTOs;

public sealed class ReviewDocumentRequest
{
    [EnumDataType(typeof(DocumentStatus))]
    public DocumentStatus Status { get; init; }

    [MaxLength(2000)]
    public string? Comment { get; init; }
}
