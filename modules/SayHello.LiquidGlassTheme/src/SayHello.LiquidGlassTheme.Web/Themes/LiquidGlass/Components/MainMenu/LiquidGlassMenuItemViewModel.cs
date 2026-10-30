using Volo.Abp.UI.Navigation;

namespace SayHello.LiquidGlassTheme.Components.MainMenu;

public class LiquidGlassMenuItemViewModel
{
    public required ApplicationMenuItem Item { get; init; }

    public string? Url { get; init; }

    public bool IsActive { get; init; }

    public bool IsTopLevel { get; init; }

    public IReadOnlyList<LiquidGlassMenuItemViewModel> Children { get; init; } = [];

    public static LiquidGlassMenuItemViewModel Create(
        ApplicationMenuItem item,
        string? selectedName,
        string currentPath,
        Func<string, string?> resolveUrl,
        bool isTopLevel = true)
    {
        var children = item.Items.OrderBy(child => child.Order)
            .Select(child => Create(child, selectedName, currentPath, resolveUrl, false)).ToArray();
        var url = string.IsNullOrEmpty(item.Url) ? null : resolveUrl(item.Url);
        var path = url?.Split('?', '#')[0].TrimEnd('/');
        var isSelected = !string.IsNullOrEmpty(selectedName)
            ? string.Equals(item.Name, selectedName, StringComparison.Ordinal)
            : path is not null && url!.StartsWith('/') &&
              (string.Equals(currentPath.TrimEnd('/'), path, StringComparison.OrdinalIgnoreCase) ||
               path.Length > 0 && currentPath.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase));

        return new LiquidGlassMenuItemViewModel
        {
            Item = item,
            Url = url,
            IsTopLevel = isTopLevel,
            IsActive = !item.IsDisabled && (isSelected || children.Any(child => child.IsActive)),
            Children = children
        };
    }
}
