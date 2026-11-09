using System.Collections;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SalePlius.Shared.Models;

namespace SalePlus.Api.Services;

/// <summary>
/// Runs after every controller action: if the response contains programs, workouts, exercises,
/// users or reports (alone, in a list, in a page or inside a profile), it adds their "_links"
/// for the current user. Controllers therefore never build links themselves.
///
/// Finally every link is made absolute (https://host/api/...), so it can be clicked straight
/// from Postman or a browser, and links to user profiles that no longer exist are dropped.
/// </summary>
public class HypermediaFilter : IAsyncResultFilter
{
    private static readonly HashSet<string> UserRels = new() { "author", "reporter" };

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult { Value: not null } result)
        {
            var linkLists = new List<List<Link>>();
            AddLinks(result.Value, context.HttpContext.User, linkLists);

            if (linkLists.Count > 0)
            {
                await DropLinksToMissingUsersAsync(linkLists, context.HttpContext);
                var origin = $"{context.HttpContext.Request.Scheme}://{context.HttpContext.Request.Host}";
                foreach (var link in linkLists.SelectMany(l => l).Where(l => l.Href.StartsWith('/')))
                {
                    link.Href = origin + link.Href;
                }
            }
        }

        await next();
    }

    /// <summary>Adds links to every resource in the response and collects all link lists for post-processing.</summary>
    private static void AddLinks(object value, ClaimsPrincipal user, List<List<Link>> found)
    {
        switch (value)
        {
            case TrainingProgram p: found.Add(p.WithLinks(user).Links!); break;
            case Workout w: found.Add(w.WithLinks(user).Links!); break;
            case Exercise e: found.Add(e.WithLinks(user).Links!); break;
            case UserProfile u: found.Add(u.WithLinks(user).Links!); break;
            case ViolationReport r: found.Add(r.WithLinks(user).Links!); break;
            case Category c: found.Add(c.WithLinks(user).Links!); break;
            case WorkoutExerciseItem entry:
                // Entry links are built by ProgramWorkoutsController (they need the parent workout).
                if (entry.Links != null) found.Add(entry.Links);
                break;
            case UserProfileDetails d:
                found.Add(d.Links);
                AddLinks(d.User, user, found);
                AddLinks(d.Programs, user, found);
                AddLinks(d.Workouts, user, found);
                break;
            case string:
                break;
            case IEnumerable list:
                foreach (var item in list)
                {
                    if (item != null) AddLinks(item, user, found);
                }
                break;
            default:
                // PagedResult<T>: its own navigation links plus its Items.
                if (value is IPageInfo page)
                {
                    found.Add(page.Links);
                    AddLinks(value.GetType().GetProperty(nameof(PagedResult<object>.Items))!.GetValue(value)!, user, found);
                }
                break;
        }
    }

    /// <summary>
    /// Some older content names an author whose account was since deleted; an "author" link to
    /// a profile that would answer 404 is worse than no link, so remove those.
    /// </summary>
    private static async Task DropLinksToMissingUsersAsync(List<List<Link>> linkLists, HttpContext http)
    {
        if (!linkLists.Any(l => l.Any(x => UserRels.Contains(x.Rel)))) return;

        var service = http.RequestServices.GetRequiredService<IFirestoreService>();
        var existing = (await service.GetAllUsersAsync()).Select(u => u.Id).ToHashSet();

        foreach (var links in linkLists)
        {
            links.RemoveAll(l => UserRels.Contains(l.Rel) && !existing.Contains(UserIdFrom(l.Href)));
        }
    }

    /// <summary>"/api/users/{id}" or "/api/users/{id}/profile" → id.</summary>
    private static string UserIdFrom(string href)
    {
        var parts = href.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var i = Array.IndexOf(parts, "users");
        return i >= 0 && i + 1 < parts.Length ? parts[i + 1] : string.Empty;
    }
}
