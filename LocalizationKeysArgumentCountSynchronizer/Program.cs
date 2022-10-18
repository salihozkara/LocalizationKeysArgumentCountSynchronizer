using System.Text.RegularExpressions;
using Newtonsoft.Json;

void Exit()
{
    Console.WriteLine("Press any key to exit");
    Console.ReadKey();
    Environment.Exit(0);
}

Console.WriteLine("Enter the default culture file path:");
var defaultCultureFilePath = Console.ReadLine();

if (!File.Exists(defaultCultureFilePath))
{
    Console.WriteLine("File not found");
    Exit();
}

var resourcesPath = Path.GetDirectoryName(defaultCultureFilePath);

if (!AbpLocalizationInfo.TryDeserialize(File.ReadAllText(defaultCultureFilePath), out var localizationInfo))
{
    Console.WriteLine("Invalid localization file");
    Exit();
}
var defaultCulture = new AbpCulture
{
    Culture = Path.GetFileNameWithoutExtension(defaultCultureFilePath),
    FilePath = defaultCultureFilePath,
    LocalizationInfo = localizationInfo
};

var cultures = Directory.GetFiles(resourcesPath, "*.json")
    .Where(x => x != defaultCulture.FilePath)
    .Select(f => new AbpCulture
    {
        Culture = Path.GetFileNameWithoutExtension(f),
        FilePath = f
    })
    .Where(x=>
    {
        if (!AbpLocalizationInfo.TryDeserialize(File.ReadAllText(x.FilePath), out var argLocalizationInfo))
            return false;
        x.LocalizationInfo = argLocalizationInfo;
        return true;
    })
    .ToList();

var defaultCultureKeysAndArgCount = defaultCulture.LocalizationInfo.Texts
    .ToDictionary(k => k.Key, v => GetArgCount(v.Value));

var asynchronousResources = new List<AbpResourceInfo>();


foreach (var culture in cultures)
{
    var cultureKeysAndArgCount = culture.LocalizationInfo.Texts
        .ToDictionary(k => k.Key, v => GetArgCount(v.Value));

    var asynchronousResource = new AbpResource
    {
        ResourcePath = culture.FilePath,
        Texts = new List<AbpResourceText>()
    };

    foreach (var (key, value) in cultureKeysAndArgCount)
    {
        if (defaultCultureKeysAndArgCount.ContainsKey(key) && defaultCultureKeysAndArgCount[key] != value)
        {
            asynchronousResource.Texts.Add(new AbpResourceText
            {
                Reference = culture.LocalizationInfo.Texts[key],
                Target = defaultCulture.LocalizationInfo.Texts[key],
                LocalizationKey = key,
                ReferenceArgumentCount = value,
                TargetArgumentCount = defaultCultureKeysAndArgCount[key]
            });
        }
    }

    if (asynchronousResource.Texts.Any())
    {
        asynchronousResources.Add(new AbpResourceInfo
        {
            ReferenceCulture = culture.Culture,
            TargetCulture = defaultCulture.Culture,
            Resource = asynchronousResource
        });
    }
}

WriteToFile(JsonConvert.SerializeObject(asynchronousResources, Formatting.Indented));
Exit();

int GetArgCount(string text)
{
    var regex = new Regex(@"\{(\d+)\}");
    return regex.Matches(text).Count;
}

void WriteToFile(string content, string? filePath = null)
{
    filePath ??= Path.Combine(resourcesPath, "asynchronous-resources.json");
    File.WriteAllText(filePath, content);
}

public class AbpCulture
{
    public string Culture { get; set; }
    public string FilePath { get; set; }

    public AbpLocalizationInfo LocalizationInfo { get; set; }
}

public class AbpResourceInfo
{
    public string ReferenceCulture { get; set; }

    public string TargetCulture { get; set; }

    public AbpResource Resource { get; set; }
}

public class AbpResource
{
    public string ResourcePath { get; set; }

    public List<AbpResourceText> Texts { get; set; }
}

public class AbpResourceText
{
    public string LocalizationKey { get; set; }

    public string Reference { get; set; }

    public string Target { get; set; }

    public int ReferenceArgumentCount { get; set; }

    public int TargetArgumentCount { get; set; }
}

public class AbpLocalizationInfo
{
    public string Culture { get; set; }

    public Dictionary<string, string> Texts { get; set; }
    
    public static bool TryDeserialize(string json, out AbpLocalizationInfo? localizationInfo)
    {
        try
        {
            localizationInfo = JsonConvert.DeserializeObject<AbpLocalizationInfo>(json);
            return true;
        }
        catch (Exception)
        {
            localizationInfo = null;
            return false;
        }
    }
}