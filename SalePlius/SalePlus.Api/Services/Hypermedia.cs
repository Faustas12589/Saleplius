using System.Security.Claims;
using Microsoft.AspNetCore.WebUtilities;
using SalePlius.Shared.Models;

namespace SalePlus.Api.Services;

/// <summary>
/// Builds the "_links" on API responses (HATEOAS). Every resource tells the client where related
/// resources are and which actions *this* user may perform, so the client does not have to
/// hard-code URLs or permission rules. Links depend on who is asking:
///   guest  → read-only links,
///   member → + rate / comment / save / report,
///   owner or admin → + update / delete.
/// </summary>
public static class Hypermedia
{
    private static string? Me(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier);
    private static bool LoggedIn(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true;

    public static Workout WithLinks(this Workout w, ClaimsPrincipal user)
    {
        var self = $"/api/workouts/{w.Id}";
        var links = new List<Link>
        {
            new("self", self),
            new("summary", $"{self}/summary"),
            new("ratings", $"{self}/ratings"),
            new("comments", $"{self}/comments"),
            new("author", $"/api/users/{w.CreatedByUserId}/profile"),
        };

        if (!string.IsNullOrEmpty(w.ProgramId))
        {
            // Position in the hierarchy: Program → this workout → its exercises.
            var nested = $"/api/programs/{w.ProgramId}/workouts/{w.Id}";
            links.Add(new("program", $"/api/programs/{w.ProgramId}"));
            links.Add(new("nested", nested));
            links.Add(new("exercises", $"{nested}/exercises"));
        }

        if (LoggedIn(user))
        {
            links.Add(new("rate", $"{self}/ratings", "POST"));
            links.Add(new("comment", $"{self}/comments", "POST"));
            links.Add(new("save", $"/api/users/{Me(user)}/saved-workouts/{w.Id}", "POST"));
            if (Me(user) != w.CreatedByUserId) links.Add(new("report", "/api/reports", "POST"));
        }

        if (LoggedIn(user) && user.CanModify(w.CreatedByUserId))
        {
            links.Add(new("update", self, "PUT"));
            links.Add(new("delete", self, "DELETE"));
            links.Add(new("add-exercise", $"{self}/exercises", "POST"));
        }

        w.Links = links;
        return w;
    }

    /// <summary>Links for one exercise entry of a workout that belongs to a program.</summary>
    public static WorkoutExerciseItem WithLinks(this WorkoutExerciseItem entry, Workout workout, ClaimsPrincipal user)
    {
        var parent = $"/api/programs/{workout.ProgramId}/workouts/{workout.Id}";
        var self = $"{parent}/exercises/{entry.Id}";
        var links = new List<Link>
        {
            new("self", self),
            new("workout", parent),
            new("program", $"/api/programs/{workout.ProgramId}"),
            new("exercise", $"/api/exercises/{entry.ExerciseId}"),
        };

        if (LoggedIn(user) && user.CanModify(workout.CreatedByUserId))
        {
            links.Add(new("update", self, "PUT"));
            links.Add(new("delete", self, "DELETE"));
        }

        entry.Links = links;
        return entry;
    }

    public static Exercise WithLinks(this Exercise e, ClaimsPrincipal user)
    {
        var self = $"/api/exercises/{e.Id}";
        var links = new List<Link>
        {
            new("self", self),
            new("comments", $"{self}/comments"),
            new("author", $"/api/users/{e.CreatedByUserId}/profile"),
        };

        if (LoggedIn(user))
        {
            links.Add(new("comment", $"{self}/comments", "POST"));
            links.Add(new("save", $"/api/users/{Me(user)}/saved-exercises/{e.Id}", "POST"));
            if (Me(user) != e.CreatedByUserId) links.Add(new("report", "/api/reports", "POST"));
        }

        if (LoggedIn(user) && user.CanModify(e.CreatedByUserId))
        {
            links.Add(new("update", self, "PUT"));
            links.Add(new("delete", self, "DELETE"));
        }

        e.Links = links;
        return e;
    }

    public static TrainingProgram WithLinks(this TrainingProgram p, ClaimsPrincipal user)
    {
        var self = $"/api/programs/{p.Id}";
        var links = new List<Link>
        {
            new("self", self),
            new("workouts", $"{self}/workouts"),
            new("author", $"/api/users/{p.CreatedByUserId}/profile"),
        };

        // One link per workout in the program: Program → Workouts.
        links.AddRange(p.Workouts.Select(w => w.WorkoutId).Distinct().Select(id => new Link("workout", $"{self}/workouts/{id}")));

        if (LoggedIn(user))
        {
            links.Add(new("save", $"/api/users/{Me(user)}/saved-programs/{p.Id}", "POST"));
            if (Me(user) != p.CreatedByUserId) links.Add(new("report", "/api/reports", "POST"));
        }

        if (LoggedIn(user) && user.CanModify(p.CreatedByUserId))
        {
            links.Add(new("update", self, "PUT"));
            links.Add(new("delete", self, "DELETE"));
            links.Add(new("add-workout", $"{self}/workouts", "POST"));
        }

        p.Links = links;
        return p;
    }

    public static UserProfile WithLinks(this UserProfile u, ClaimsPrincipal user)
    {
        var self = $"/api/users/{u.Id}";
        var links = new List<Link>
        {
            new("self", self),
            new("profile", $"{self}/profile"),
            new("programs", $"/api/programs?authorId={u.Id}"),
            new("workouts", $"/api/workouts?authorId={u.Id}"),
        };

        if (LoggedIn(user) && user.CanModify(u.Id))
        {
            links.Add(new("saved-programs", $"{self}/saved-programs"));
            links.Add(new("saved-workouts", $"{self}/saved-workouts"));
            links.Add(new("saved-exercises", $"{self}/saved-exercises"));
        }

        if (user.IsInRole(AppRoles.Admin) && Me(user) != u.Id)
        {
            links.Add(new("change-role", $"{self}/role", "PATCH"));
            links.Add(new("delete", self, "DELETE"));
        }

        u.Links = links;
        return u;
    }

    public static Category WithLinks(this Category c, ClaimsPrincipal user)
    {
        var self = $"/api/categories/{c.Id}";
        var name = Uri.EscapeDataString(c.Name);
        var links = new List<Link> { new("self", self), new("all-of-type", $"/api/categories?type={c.Type}") };

        // Where the category is used.
        if (c.Type == CategoryTypes.MuscleGroup)
        {
            links.Add(new("exercises", $"/api/exercises?muscleGroup={name}"));
            links.Add(new("workouts", $"/api/workouts?muscleGroup={name}"));
        }
        if (c.Type == CategoryTypes.Goal) links.Add(new("programs", $"/api/programs?goal={name}"));

        if (user.IsInRole(AppRoles.Admin))
        {
            links.Add(new("update", self, "PUT"));
            links.Add(new("delete", self, "DELETE"));
        }

        c.Links = links;
        return c;
    }

    public static ViolationReport WithLinks(this ViolationReport r, ClaimsPrincipal user)
    {
        var target = r.TargetType switch
        {
            "Program" => $"/api/programs/{r.TargetId}",
            "Workout" => $"/api/workouts/{r.TargetId}",
            "Exercise" => $"/api/exercises/{r.TargetId}",
            "User" => $"/api/users/{r.TargetId}",
            _ => null
        };

        var links = new List<Link>();
        if (target != null) links.Add(new("target", target));
        links.Add(new("reporter", $"/api/users/{r.ReportedByUserId}"));
        if (user.IsInRole(AppRoles.Admin)) links.Add(new("update-status", $"/api/reports/{r.Id}/status", "PATCH"));

        r.Links = links;
        return r;
    }

    /// <summary>
    /// Cuts one page out of <paramref name="all"/> and adds navigation links (self/first/prev/next/last)
    /// that keep the current filters, so the client can just follow "next".
    /// </summary>
    public static PagedResult<T> ToPage<T>(this IEnumerable<T> all, PageQuery query, HttpRequest request, string? createHref = null)
    {
        var list = all as IList<T> ?? all.ToList();
        var totalPages = Math.Max(1, (int)Math.Ceiling(list.Count / (double)query.PageSize));

        var result = new PagedResult<T>
        {
            Items = list.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = list.Count,
            TotalPages = totalPages
        };

        string PageUrl(int page)
        {
            var parameters = request.Query.ToDictionary(q => q.Key, q => (string?)q.Value.ToString(), StringComparer.OrdinalIgnoreCase);
            parameters["page"] = page.ToString();
            parameters["pageSize"] = query.PageSize.ToString();
            return QueryHelpers.AddQueryString(request.Path.Value ?? "/", parameters);
        }

        result.Links.Add(new("self", PageUrl(query.Page)));
        result.Links.Add(new("first", PageUrl(1)));
        if (query.Page > 1) result.Links.Add(new("prev", PageUrl(Math.Min(query.Page - 1, totalPages))));
        if (query.Page < totalPages) result.Links.Add(new("next", PageUrl(query.Page + 1)));
        result.Links.Add(new("last", PageUrl(totalPages)));
        if (createHref != null) result.Links.Add(new("create", createHref, "POST"));

        return result;
    }

    /// <summary>Applies ?sort=newest|rating|name.</summary>
    public static IEnumerable<T> SortBy<T>(this IEnumerable<T> items, string sort,
        Func<T, DateTime> created, Func<T, double> rating, Func<T, string> name) => sort switch
    {
        SortedPageQuery.Rating => items.OrderByDescending(rating).ThenByDescending(created),
        SortedPageQuery.Name => items.OrderBy(name, StringComparer.CurrentCultureIgnoreCase),
        _ => items.OrderByDescending(created)
    };
}
