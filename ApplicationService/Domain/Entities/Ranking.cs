using StudentCenter.ApplicationService.Domain.Enums;
using StudentCenter.ApplicationService.Domain.Exceptions;

namespace StudentCenter.ApplicationService.Domain.Entities;

public sealed class Ranking
{
    private Ranking()
    {
    }

    public Ranking(Guid competitionId, RankingType type = RankingType.Preliminary)
    {
        Id = Guid.NewGuid();
        CompetitionId = competitionId;
        Type = type;
        Status = RankingStatus.Draft;
    }

    public Guid Id { get; private set; }
    public Guid CompetitionId { get; private set; }
    public RankingType Type { get; private set; }
    public RankingStatus Status { get; private set; }
    public RankingTieRule TieRule { get; private set; }
    public DateTime GeneratedAtUtc { get; private set; }
    public Guid GeneratedByUserId { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public Guid? PublishedByUserId { get; private set; }
    public string SourceFingerprint { get; private set; } = null!;
    public byte[] RowVersion { get; private set; } = [];
    public int? AvailablePlaces { get; private set; }

    public void SetCapacity(int places)
    {
        if (Status != RankingStatus.Draft || Type != RankingType.Final)
            throw new ApplicationConflictException("Capacity belongs to a final draft ranking.");
        if (places < 0)
            throw new ArgumentException("Capacity cannot be negative.");
        AvailablePlaces = places;
    }

    private readonly List<RankingEntry> entries = [];
    public IReadOnlyCollection<RankingEntry> Entries => entries.AsReadOnly();

    public void ReplaceDraft(
        IEnumerable<RankingEntry> newEntries, string fingerprint, RankingTieRule tieRule, Guid userId)
    {
        if (Status != RankingStatus.Draft)
            throw new ApplicationConflictException("A published ranking cannot be changed.");
        if (userId == Guid.Empty || !Enum.IsDefined(tieRule))
            throw new ArgumentException("A valid user and tie rule are required.");
        var replacement = newEntries.ToArray();
        if (replacement.Length == 0 || replacement.Any(entry => entry.RankingId != Id))
            throw new ArgumentException("Ranking entries must belong to this ranking.");
        if (replacement.Select(entry => entry.ApplicationId).Distinct().Count() != replacement.Length)
            throw new ArgumentException("An application cannot occur twice in a ranking.");

        entries.Clear();
        entries.AddRange(replacement);
        SourceFingerprint = fingerprint;
        TieRule = tieRule;
        GeneratedAtUtc = DateTime.UtcNow;
        GeneratedByUserId = userId;
    }

    public void Publish(Guid userId)
    {
        if (Status != RankingStatus.Draft || entries.Count == 0)
            throw new ApplicationConflictException("Only a populated draft ranking can be published.");
        if (userId == Guid.Empty)
            throw new ArgumentException("A publishing user is required.");

        Status = RankingStatus.Published;
        PublishedAtUtc = DateTime.UtcNow;
        PublishedByUserId = userId;
    }
}

public sealed class RankingEntry
{
    private RankingEntry()
    {
    }

    public RankingEntry(Guid rankingId, Guid applicationId, Guid studentId, int position, decimal totalPoints)
    {
        if (position < 1 || totalPoints < 0)
            throw new ArgumentException("Position and points are invalid.");

        Id = Guid.NewGuid();
        RankingId = rankingId;
        ApplicationId = applicationId;
        StudentId = studentId;
        Position = position;
        TotalPoints = totalPoints;
    }

    public Guid Id { get; private set; }
    public Guid RankingId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public Guid StudentId { get; private set; }
    public int Position { get; private set; }
    public decimal TotalPoints { get; private set; }
}
