namespace SalePlius.Client.Components;

/// <summary>
/// Which muscles the card or row under the mouse trains. Pages pass one instance to the body map and
/// to their <see cref="HoverZone"/>s; changing it redraws only the body map, not the whole page.
/// </summary>
public sealed class MuscleHover
{
    public IReadOnlyCollection<string> Primary { get; private set; } = Array.Empty<string>();
    public IReadOnlyCollection<string> Secondary { get; private set; } = Array.Empty<string>();

    public bool IsEmpty => Primary.Count == 0 && Secondary.Count == 0;

    public event Action? Changed;

    public void Show(IReadOnlyCollection<string> primary, IReadOnlyCollection<string> secondary)
    {
        Primary = primary;
        Secondary = secondary;
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (IsEmpty) return;
        Primary = Secondary = Array.Empty<string>();
        Changed?.Invoke();
    }
}
