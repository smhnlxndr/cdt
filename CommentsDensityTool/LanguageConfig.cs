namespace CommentsDensityTool;

public class LanguageConfig
{
    public required string Name { get; set; }
    public List<string> FileExtensions { get; set; } = [];
    public List<string> IgnoreExtensions { get; set; } = [];
    public required string SingleLineComment { get; set; }
    public string? MultiLineCommentStart { get; set; }
    public string? MultiLineCommentEnd { get; set; }
    public List<string> IgnorePatterns { get; set; } = [];

    public double? DensityThreshold { get; set; }
    
    public List<string> MethodSpecificComments { get; set; } = [];
}