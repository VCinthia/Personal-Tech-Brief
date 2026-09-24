namespace PersonalTechBrief.Domain.Messaging;

public enum OutboxMessageStatus
{
    Pending,
    Dispatched,
    Quarantined,
}
