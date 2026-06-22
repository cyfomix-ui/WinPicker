using System.Reflection;

namespace WinPicker;

internal static class BrandingImageLoader
{
    public static Image? LoadImage(string fileName)
    {
        try
        {
            // First try embedded resources. This allows a true single-file EXE publish.
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly
                .GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(resourceName))
            {
                using var resourceStream = assembly.GetManifestResourceStream(resourceName);
                if (resourceStream is not null)
                    return Image.FromStream(resourceStream);
            }

            // Fallback for Debug runs from Visual Studio.
            var path = Path.Combine(AppContext.BaseDirectory, fileName);
            if (!File.Exists(path))
                path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", fileName);

            if (!File.Exists(path))
                return null;

            var bytes = File.ReadAllBytes(path);
            using var stream = new MemoryStream(bytes);
            return Image.FromStream(stream);
        }
        catch
        {
            return null;
        }
    }
}
