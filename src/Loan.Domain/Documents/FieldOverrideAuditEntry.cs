namespace Loan.Domain.Documents;

public class FieldOverrideAuditEntry
{
    public string AuditId { get; }
    public string ApplicationId { get; }
    public string FieldName { get; }
    public string DocumentId { get; }
    public string ActorId { get; }
    public string ActorRole { get; }
    public DateTime TimestampUtc { get; }
    public string Action { get; } // "Confirm" or "Override"
    public string Reason { get; }
    public string CorrelationId { get; }

    public FieldOverrideAuditEntry(
        string auditId,
        string applicationId,
        string fieldName,
        string documentId,
        string actorId,
        string actorRole,
        DateTime timestampUtc,
        string action,
        string reason,
        string correlationId)
    {
        AuditId = auditId;
        ApplicationId = applicationId;
        FieldName = fieldName;
        DocumentId = documentId;
        ActorId = actorId;
        ActorRole = actorRole;
        TimestampUtc = timestampUtc;
        Action = action;
        Reason = reason;
        CorrelationId = correlationId;
    }
}
