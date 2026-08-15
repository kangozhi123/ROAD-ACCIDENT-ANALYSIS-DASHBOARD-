namespace RoadSafety.Web.ViewModels;

/// <summary>
/// The header every page renders through <c>_PageHeader</c>, so titles,
/// breadcrumbs and the primary action sit in the same place on every screen.
/// </summary>
/// <param name="Title">Page name, shown as the h1.</param>
/// <param name="Subtitle">One line of context under the title.</param>
/// <param name="Crumbs">Trail above the title. Omit on top-level pages.</param>
/// <param name="ActionText">Label for the primary action. Omit for no action.</param>
/// <param name="ActionModalTarget">CSS selector of a dialog to open, e.g. "#addOfficer".</param>
/// <param name="ActionPage">Razor page to link to instead of opening a dialog.</param>
public record PageHeader(
string Title,
string? Subtitle = null,
IReadOnlyList<Crumb>? Crumbs = null,
string? ActionText = null,
string? ActionModalTarget = null,
string? ActionPage = null);
