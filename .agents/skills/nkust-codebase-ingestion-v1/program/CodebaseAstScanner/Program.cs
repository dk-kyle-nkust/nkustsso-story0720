using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CodebaseAstScanner
{
    public class ClassInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string Inherits { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public List<MethodInfo> Methods { get; set; } = new();
    }

    public class MethodInfo
    {
        public string MethodName { get; set; } = string.Empty;
        public string ReturnType { get; set; } = string.Empty;
        public string Parameters { get; set; } = string.Empty;
        public bool IsAsync { get; set; }
        public string Summary { get; set; } = string.Empty;
    }

    public class Program
    {
        private static readonly string[] ForbiddenEndpoints = { "sybase.nkust.edu.tw", "140.133." };
        private static readonly string[] ForbiddenMutations = { "DELETE", "UPDATE", "INSERT", "DROP", "ALTER", "TRUNCATE" };

        public static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("==========================================================");
            Console.WriteLine("[AST SCANNER] NKUST-OOSA Codebase Static Structure Scanner");
            Console.WriteLine("==========================================================");

            string targetDir = args.Length > 0 ? args[0] : @"d:\projects_upgrade\nkust-oosa\nkust-oosa-step-8";
            string outputMd = args.Length > 1 ? args[1] : string.Empty;

            if (!Directory.Exists(targetDir))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: Target directory does not exist: {targetDir}");
                Console.ResetColor();
                return 1;
            }

            Console.WriteLine($"Scanning target directory: {targetDir}");

            // 1. Gather all C# files excluding bin/obj/.git/.agents
            var csFiles = Directory.GetFiles(targetDir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(@"\bin\") && !f.Contains(@"\obj\") && !f.Contains(@"\.git\") && !f.Contains(@"\.agents\"))
                .ToList();

            Console.WriteLine($"Found {csFiles.Count} active C# source files to analyze.");

            // 2. Perform Security Assertion & Structural AST Extraction
            var parsedClasses = new List<ClassInfo>();
            int securityViolationCount = 0;

            foreach (var file in csFiles)
            {
                string text = File.ReadAllText(file, Encoding.UTF8);

                // Security Check
                foreach (var ep in ForbiddenEndpoints)
                {
                    if (text.Contains(ep, StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var mut in ForbiddenMutations)
                        {
                            if (Regex.IsMatch(text, $@"\b{mut}\b", RegexOptions.IgnoreCase))
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"[SECURITY ALERT] Potential mutation detected in {file} towards {ep} ({mut})");
                                Console.ResetColor();
                                securityViolationCount++;
                            }
                        }
                    }
                }

                // Parse Classes and Methods
                var classes = ExtractClassesAndMethods(file, text);
                parsedClasses.AddRange(classes);
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Extracted {parsedClasses.Count} classes/interfaces across the codebase.");
            Console.ResetColor();

            // Highlight Key Services
            var sampleKeyServices = parsedClasses
                .Where(c => c.ClassName.EndsWith("Service") || c.ClassName.EndsWith("Controller"))
                .Take(10)
                .ToList();

            Console.WriteLine("\n[Sample Key Architecture Classes Extracted]");
            foreach (var cls in sampleKeyServices)
            {
                Console.WriteLine($"  - [{cls.Namespace}] {cls.ClassName} (Methods: {cls.Methods.Count})");
            }

            // Optional Markdown Output
            if (!string.IsNullOrWhiteSpace(outputMd))
            {
                var sb = new StringBuilder();
                sb.AppendLine("# NKUST-OOSA Codebase AST Architecture Report");
                sb.AppendLine($"\n- **Generated At**: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                sb.AppendLine($"- **Total C# Files**: {csFiles.Count}");
                sb.AppendLine($"- **Total Classes**: {parsedClasses.Count}");
                sb.AppendLine($"- **Security Violations**: {securityViolationCount}\n");

                sb.AppendLine("## Key Classes and Interfaces\n");
                sb.AppendLine("| Namespace | Class Name | Base / Interface | Methods | Summary |");
                sb.AppendLine("| :--- | :--- | :--- | :---: | :--- |");

                foreach (var c in parsedClasses)
                {
                    string safeSummary = string.IsNullOrWhiteSpace(c.Summary) ? "N/A" : c.Summary.Replace("\n", " ").Replace("|", "/");
                    sb.AppendLine($"| `{c.Namespace}` | `[{c.ClassName}]` | `{c.Inherits}` | {c.Methods.Count} | {safeSummary} |");
                }

                File.WriteAllText(outputMd, sb.ToString(), Encoding.UTF8);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\nReport exported successfully to: {outputMd}");
                Console.ResetColor();
            }

            Console.WriteLine("==========================================================");
            Console.WriteLine("[AST SCANNER] Completed successfully.");
            Console.WriteLine("==========================================================");
            return 0;
        }

        private static List<ClassInfo> ExtractClassesAndMethods(string filePath, string source)
        {
            var result = new List<ClassInfo>();

            // Extract Namespace
            var nsMatch = Regex.Match(source, @"namespace\s+([\w\.]+)");
            string ns = nsMatch.Success ? nsMatch.Groups[1].Value : "Global";

            // Match Class Declarations
            var classRegex = new Regex(@"(?:(?:public|internal|protected|private|static|sealed|abstract)\s+)*(?:class|interface|record)\s+(\w+)(?:\s*:\s*([\w\s,<>]+))?", RegexOptions.Compiled);
            var classMatches = classRegex.Matches(source);

            foreach (Match cm in classMatches)
            {
                string className = cm.Groups[1].Value;
                string inherits = cm.Groups[2].Success ? cm.Groups[2].Value.Trim() : "None";

                var cls = new ClassInfo
                {
                    FilePath = filePath,
                    Namespace = ns,
                    ClassName = className,
                    Inherits = inherits
                };

                // Match Public Methods inside Class scope
                var methodRegex = new Regex(@"(?:public|protected)\s+(?:async\s+)?([\w<>\[\]\?]+)\s+(\w+)\s*\(([^)]*)\)", RegexOptions.Compiled);
                var methodMatches = methodRegex.Matches(source);

                foreach (Match mm in methodMatches)
                {
                    string retType = mm.Groups[1].Value;
                    string methodName = mm.Groups[2].Value;
                    string paramList = mm.Groups[3].Value.Trim();

                    // Filter out class constructor matches or keywords
                    if (methodName == className || methodName == "if" || methodName == "while" || methodName == "switch")
                    {
                        continue;
                    }

                    cls.Methods.Add(new MethodInfo
                    {
                        MethodName = methodName,
                        ReturnType = retType,
                        Parameters = paramList,
                        IsAsync = retType.StartsWith("Task")
                    });
                }

                result.Add(cls);
            }

            return result;
        }
    }
}
