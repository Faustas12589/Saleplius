using SalePlius.Shared.Models;

namespace SalePlius.Client.Services;

/// <summary>
/// The admin-managed categories (muscle groups, equipment, goals), loaded from the API once and
/// shared by every page's dropdowns. The admin page calls <see cref="Invalidate"/> after a change.
/// </summary>
public class CategoryStore(ApiService api)
{
    private List<Category>? _all;

    public async Task<List<Category>> AllAsync() => _all ??= await api.GetCategoriesAsync();

    /// <summary>Names of one type, e.g. await Categories.NamesAsync(CategoryTypes.MuscleGroup).</summary>
    public async Task<List<string>> NamesAsync(string type) =>
        (await AllAsync()).Where(c => c.Type == type).Select(c => c.Name).ToList();

    public void Invalidate() => _all = null;
}
