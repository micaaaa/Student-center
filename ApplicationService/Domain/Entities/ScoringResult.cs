namespace StudentCenter.ApplicationService.Domain.Entities;

public sealed class ScoringResult
{
    // SQL decimal(18,2) storage bound, not a competition scoring rule.
    public const decimal MaxStoredPoints = 9999999999999999.99m;

    private ScoringResult()
    {
    }

    public ScoringResult(Guid applicationId)
    {
        Id = Guid.NewGuid();
        ApplicationId = applicationId;
    }

    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }
    public decimal AcademicPoints { get; private set; }
    public decimal IncomePoints { get; private set; }
    public decimal ECTSPoints { get; private set; }
    public decimal StudyYearPoints { get; private set; }
    public decimal AdditionalPoints { get; private set; }
    public decimal TotalPoints { get; private set; }
    public DateTime CalculatedAtUtc { get; private set; }
    public Guid CalculatedByUserId { get; private set; }
    public string DocumentReviewFingerprint { get; private set; } = null!;
    public byte[] RowVersion { get; private set; } = [];

    public void Calculate(
        decimal academicPoints,
        decimal incomePoints,
        decimal ectsPoints,
        decimal studyYearPoints,
        decimal additionalPoints,
        Guid calculatedByUserId,
        string documentReviewFingerprint)
    {
        decimal[] values = [academicPoints, incomePoints, ectsPoints, studyYearPoints, additionalPoints];
        if (values.Any(value => value < 0 || value > MaxStoredPoints || decimal.Round(value, 2) != value))
            throw new ArgumentException("Points must be non-negative, have at most two decimal places and fit decimal(18,2).");
        var total = values.Sum();
        if (total > MaxStoredPoints)
            throw new ArgumentException("Total points exceed the supported storage limit.");
        if (calculatedByUserId == Guid.Empty)
            throw new ArgumentException("The scoring user is required.");
        if (string.IsNullOrWhiteSpace(documentReviewFingerprint))
            throw new ArgumentException("Document review information is required.");

        AcademicPoints = academicPoints;
        IncomePoints = incomePoints;
        ECTSPoints = ectsPoints;
        StudyYearPoints = studyYearPoints;
        AdditionalPoints = additionalPoints;
        TotalPoints = total;
        CalculatedByUserId = calculatedByUserId;
        DocumentReviewFingerprint = documentReviewFingerprint;
        CalculatedAtUtc = DateTime.UtcNow;
    }
}
