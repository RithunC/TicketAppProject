namespace TicketWebApp.Models.DTOs
{
    public class AuditLogDtos

    {
        public class AuditLogQueryDto
        {
            public DateTime? FromUtc { get; set; }
            public DateTime? ToUtc { get; set; }
            public long? ActorUserId { get; set; }
            public string? Action { get; set; }
            public string? Path { get; set; }

            public int Page { get; set; } = 1;
            public int PageSize { get; set; } = 50;
        }

        public class AuditLogResponseDto
        {
            public long Id { get; set; }

            public long? ActorUserId { get; set; }
            public string? ActorUserName { get; set; }
            public string? ActorRole { get; set; }

            public string Action { get; set; } = "";
            public string Path { get; set; } = "";
            public string? Description { get; set; }   // ✅ new for UI (uses Message)
            public int? DurationMs { get; set; }       // ✅ extracted from MetadataJson

            public bool Success { get; set; }
            public int StatusCode { get; set; }

            public DateTime OccurredAtUtc { get; set; }
        }
    }
}


