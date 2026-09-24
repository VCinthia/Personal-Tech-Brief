namespace PersonalTechBrief.Domain.Interests;

public sealed class Interest
{
    private Interest()
    {
    }

    private Interest(Guid id, string name, InterestPriority priority, DateTime utcNow)
    {
        Id = id;
        Apply(name, priority, isActive: true, utcNow);
        CreatedAtUtc = utcNow;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string NormalizedName { get; private set; } = null!;

    public InterestPriority Priority { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public static Interest Create(string name, InterestPriority priority, DateTime utcNow) =>
        new(Guid.NewGuid(), name, priority, utcNow);

    public void Update(string name, InterestPriority priority, bool isActive, DateTime utcNow) =>
        Apply(name, priority, isActive, utcNow);

    public void Disable(DateTime utcNow) =>
        Apply(Name, Priority, isActive: false, utcNow);

    private void Apply(string name, InterestPriority priority, bool isActive, DateTime utcNow)
    {
        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentOutOfRangeException(nameof(priority), "Interest priority must be High, Medium, or Low.");
        }

        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Interest timestamps must be UTC.", nameof(utcNow));
        }

        Name = InterestName.Clean(name);
        NormalizedName = InterestName.Normalize(name);
        Priority = priority;
        IsActive = isActive;
        UpdatedAtUtc = utcNow;
    }
}
