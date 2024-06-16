using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Text;
using System.Text.Json;
using CommentsDensityTool;

var rootCommand = new RootCommand
{
    new Option<string>(
        "--dir",
        description: "The directory to scan"),
    new Option<string>(
        "--config",
        description: "The configuration file")
};

rootCommand.Description = "Comments Density Calculator";

rootCommand.Handler = CommandHandler.Create<string, string>((dir, config) =>
{
    if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(config))
    {
        Console.WriteLine("Usage: CommentsDensityCalculator --dir <directory-path> --config <config-file>");
        return;
    }

    if (!Directory.Exists(dir))
    {
        Console.WriteLine($"Directory not found: {dir}");
        return;
    }

    if (!File.Exists(config))
    {
        Console.WriteLine($"Config file not found: {config}");
        return;
    }

    try
    {
        var configContent = File.ReadAllText(config);
        var configuration = JsonSerializer.Deserialize<Config>(configContent, new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        if (configuration == null || configuration?.Languages == null || configuration.Languages.Count == 0)
        {
            Console.WriteLine("Invalid or empty configuration.");
            return;
        }

        double overallDensityScore = CalculateCommentsDensityForDirectory(dir, configuration);
        Console.WriteLine($"Overall Comments Density Score: {overallDensityScore:F2}%");

        // Check against density threshold
        foreach (var languageConfig in configuration.Languages)
        {
            if (languageConfig.DensityThreshold.HasValue && overallDensityScore > languageConfig.DensityThreshold.Value)
            {
                Console.WriteLine($"Comments density for {languageConfig.Name} exceeds configured threshold of {languageConfig.DensityThreshold.Value}%.");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred: {ex.Message}");
    }
});

await rootCommand.InvokeAsync(args);

static double CalculateCommentsDensityForDirectory(string directoryPath, Config config)
{
    var allFiles = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories);
    var files = allFiles.Where(file => config.Languages.Any(lang => lang.FileExtensions.Contains(Path.GetExtension(file).ToLower())))
                        .Where(file => !config.Languages.Any(lang => lang.IgnoreExtensions.Contains(Path.GetExtension(file).ToLower())))
                        .ToList();

    var totalLines = 0;
    var totalCommentLines = 0;
    var totalMethodSpecificCommentLines = 0;
    object lockObject = new object();

    Parallel.ForEach(files, (filePath) =>
    {
        var languageConfig = config.Languages.FirstOrDefault(lang => lang.FileExtensions.Contains(Path.GetExtension(filePath).ToLower()));
        if (languageConfig != null)
        {
            (int fileTotalLines, int fileCommentLines, int fileMethodSpecificCommentLines) = CalculateCommentsDensityForFile(filePath, languageConfig);

            lock (lockObject)
            {
                totalLines += fileTotalLines;
                totalCommentLines += fileCommentLines;
                totalMethodSpecificCommentLines += fileMethodSpecificCommentLines;
            }
        }
    });

    if (totalLines == 0)
    {
        return 0;
    }

    double overallDensityScore = (double)(totalCommentLines + totalMethodSpecificCommentLines) / totalLines * 100;

    return overallDensityScore;
}

static (int totalLines, int commentLines, int methodSpecificCommentLines) CalculateCommentsDensityForFile(string filePath, LanguageConfig languageConfig)
{
    string[] lines = File.ReadAllLines(filePath);
    int totalLines = lines.Length;
    int commentLines = 0;
    int methodSpecificCommentLines = 0;

    bool inMultiLineComment = false;

    foreach (var line in lines)
    {
        string trimmedLine = line.Trim();

        if (languageConfig.IgnorePatterns.Any(pattern => trimmedLine.StartsWith(pattern)))
        {
            continue;
        }

        if (!string.IsNullOrEmpty(languageConfig.SingleLineComment) && trimmedLine.StartsWith(languageConfig.SingleLineComment))
        {
            commentLines++;
        }
        else if (!string.IsNullOrEmpty(languageConfig.MultiLineCommentStart) && trimmedLine.StartsWith(languageConfig.MultiLineCommentStart))
        {
            inMultiLineComment = true;
            commentLines++;
        }
        else if (!string.IsNullOrEmpty(languageConfig.MultiLineCommentEnd) && trimmedLine.EndsWith(languageConfig.MultiLineCommentEnd))
        {
            inMultiLineComment = false;
            commentLines++;
        }
        else if (inMultiLineComment)
        {
            commentLines++;
        }

        // Check for method-specific comments
        if (languageConfig.MethodSpecificComments != null)
        {
            foreach (var methodComment in languageConfig.MethodSpecificComments)
            {
                if (trimmedLine.Contains(methodComment))
                {
                    methodSpecificCommentLines++;
                }
            }
        }
    }

    return (totalLines, commentLines, methodSpecificCommentLines);
}