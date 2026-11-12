using System.Text;
using Collector.Core.Lake;

namespace Collector.Lake.Tests;

public static class Writes
{
    public const string Source = "sportm_mk";
    public const string Kind = "product";

    public static readonly DateTime Harvested = new(2026, 11, 12, 9, 30, 0, DateTimeKind.Utc);

    public static LakeWrite For(Guid projectId, string providerId, string text)
    {
        return For(projectId, providerId, Encoding.UTF8.GetBytes(text));
    }

    public static LakeWrite For(Guid projectId, string providerId, byte[] body)
    {
        return new LakeWrite
        {
            ProjectId = projectId,
            Source = Source,
            Kind = Kind,
            ProviderId = providerId,
            Url = $"https://www.sport-m.com.mk/product/{Uri.EscapeDataString(providerId)}",
            ContentType = "text/html; charset=utf-8",
            HarvestedAt = Harvested,
            Body = body,
        };
    }

    public static string KeyOf(LakeWrite write)
    {
        return new LakeKey(write.ProjectId, write.Source, write.Kind, write.ProviderId).ToString();
    }
}
