namespace StudentCenter.ApplicationService.Domain.Enums;

public enum DocumentType
{
    EnrollmentConfirmation = 1,
    IncomeCertificate,
    Transcript,
    IdentityDocument,
    Other
}

public enum DocumentStatus
{
    Pending = 1,
    Valid,
    Invalid
}
