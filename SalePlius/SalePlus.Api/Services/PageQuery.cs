using System.ComponentModel.DataAnnotations;

namespace SalePlus.Api.Services;

/// <summary>?page=1&amp;pageSize=10 on list endpoints. Out-of-range values return 400.</summary>
public class PageQuery
{
    public const int MaxPageSize = 50;

    [Range(1, int.MaxValue, ErrorMessage = "page turi būti 1 arba daugiau")]
    public int Page { get; set; } = 1;

    [Range(1, MaxPageSize, ErrorMessage = "pageSize turi būti nuo 1 iki 50")]
    public int PageSize { get; set; } = 10;
}

/// <summary>Paging plus ?sort=newest|rating|name for programs, workouts and exercises.</summary>
public class SortedPageQuery : PageQuery
{
    public const string Newest = "newest";
    public const string Rating = "rating";
    public const string Name = "name";

    [RegularExpression("^(newest|rating|name)$", ErrorMessage = "sort gali būti: newest, rating arba name")]
    public string Sort { get; set; } = Newest;
}
