namespace TicketWebApp.Models
{
    public class AuditLog
    {
        public long Id { get; set; }

        public long? ActorUserId { get; set; }
        public string? ActorUserName { get; set; }
        public string? ActorRole { get; set; }

        public string Action { get; set; } = "";
        public string EntityType { get; set; } = "";
        public string? EntityId { get; set; }

        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public string? Message { get; set; }

        public string HttpMethod { get; set; } = "";
        public string Path { get; set; } = "";

        public string? ClientIp { get; set; }
        public string? UserAgent { get; set; }
        public string? CorrelationId { get; set; }

        // ✅ REQUIRED because service uses OccurredAtUtc
        public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

        // keep existing CreatedAt if you need
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? MetadataJson { get; set; }
    }
}