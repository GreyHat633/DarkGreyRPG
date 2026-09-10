using System.Text;

namespace DarkGreyRPG.Studio.Core.Identity;

public static class DgrResourceId
{
    private const int MaxNamespaceLength = 32;
    private const int MaxLocalLength = 63;
    private const int MaxFullLength = 96;

    public static bool IsValidNamespace(string? value)
    {
        if (value is null || value.Length is < 1 or > MaxNamespaceLength || !IsAsciiAlphaNumeric(value[0]))
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            var character = value[index];
            if (!IsAsciiAlphaNumeric(character) && character is not ('_' or '-'))
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsFullId(string? value)
    {
        if (value is null || value.Length > MaxFullLength)
        {
            return false;
        }

        var separator = value.IndexOf(':');
        return separator > 0
            && separator == value.LastIndexOf(':')
            && IsValidNamespace(value[..separator])
            && IsValidLocal(value[(separator + 1)..]);
    }

    public static bool IsCompatibleId(string? value) => IsFullId(value) || IsValidBare(value);

    public static string Qualify(string namespaceValue, string local)
    {
        RequireNamespace(namespaceValue);
        RequireLocal(local);
        return $"{namespaceValue}:{local}";
    }

    public static string LocalId(string id)
    {
        RequireCompatible(id);
        var separator = id.IndexOf(':');
        return separator < 0 ? id : id[(separator + 1)..];
    }

    public static string Namespace(string id)
    {
        RequireCompatible(id);
        var separator = id.IndexOf(':');
        return separator < 0 ? string.Empty : id[..separator];
    }

    public static string RelativeJsonPath(string id)
    {
        RequireCompatible(id);
        var separator = id.IndexOf(':');
        if (separator < 0)
        {
            return id + ".json";
        }

        return $"x{Hex(id[..separator])}/x{Hex(id[(separator + 1)..])}.json";
    }

    public static string PackageFileName(string id)
    {
        RequireCompatible(id);
        var separator = id.IndexOf(':');
        if (separator < 0)
        {
            return id + ".dgrs";
        }

        return $"x{Hex(id[..separator])}_x{Hex(id[(separator + 1)..])}.dgrs";
    }

    private static bool IsValidLocal(string? value)
    {
        if (value is null || value.Length is < 1 or > MaxLocalLength || !IsAsciiAlphaNumeric(value[0]))
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            var character = value[index];
            if (!IsAsciiAlphaNumeric(character) && character is not ('_' or '.' or '-'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidBare(string? value)
    {
        if (value is null || value.Length == 0 || !IsAsciiLowerAlphaNumeric(value[0]))
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            var character = value[index];
            if (!IsAsciiLowerAlphaNumeric(character) && character is not ('_' or '.' or '-'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiAlphaNumeric(char value) => value is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9');

    private static bool IsAsciiLowerAlphaNumeric(char value) => value is (>= 'a' and <= 'z') or (>= '0' and <= '9');

    private static void RequireNamespace(string? value)
    {
        if (!IsValidNamespace(value))
        {
            throw new ArgumentException("Invalid DGR namespace.", nameof(value));
        }
    }

    private static void RequireLocal(string? value)
    {
        if (!IsValidLocal(value))
        {
            throw new ArgumentException("Invalid DGR local ID.", nameof(value));
        }
    }

    private static void RequireCompatible(string? value)
    {
        if (!IsCompatibleId(value))
        {
            throw new ArgumentException("Invalid DGR resource ID.", nameof(value));
        }
    }

    private static string Hex(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (var valueByte in bytes)
        {
            builder.Append(valueByte.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}
