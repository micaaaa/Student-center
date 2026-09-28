using System.Security.Cryptography;
using System.Text;
using StudentCenter.ApplicationService.Domain.Entities;
using StudentCenter.ApplicationService.Domain.Enums;

namespace StudentCenter.ApplicationService.Domain.Services;

public static class DocumentReviewSnapshot
{
    public static bool AllValid(IReadOnlyCollection<ApplicationDocument> documents) =>
        documents.Count > 0 && documents.All(document => document.Status == DocumentStatus.Valid);

    public static string Fingerprint(IEnumerable<ApplicationDocument> documents)
    {
        var snapshot = string.Join("|", documents.OrderBy(document => document.Id).Select(document =>
            $"{document.Id:N}:{(int)document.Status}:{document.ReviewedAtUtc?.Ticks}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot)));
    }
}
