namespace WinPicker;

internal static class IconLoader
{
    public static Icon LoadAppIcon()
    {
        try
        {
            var localIcon = Path.Combine(AppContext.BaseDirectory, "App.ico");
            if (File.Exists(localIcon))
                return new Icon(localIcon);

            var exeIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (exeIcon is not null)
                return exeIcon;
        }
        catch
        {
            // Fall back below.
        }

        return SystemIcons.Application;
    }
}
