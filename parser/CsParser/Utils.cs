namespace CsParser;

public class Utils
{
    public static string GetShortName(string fullName)
    {
        return fullName.Contains('.') ? fullName.Substring(fullName.LastIndexOf('.') + 1) : fullName;
    }
}
