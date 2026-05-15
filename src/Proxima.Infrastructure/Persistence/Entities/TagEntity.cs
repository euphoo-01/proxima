namespace Proxima.Infrastructure.Persistence;

public sealed class TagEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
