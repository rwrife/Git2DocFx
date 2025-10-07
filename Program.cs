using System.CommandLine;
using System.Diagnostics;
using DocFxClone;
using Newtonsoft.Json;

namespace Git2DocFx;

/// <summary>
/// Main program for the Git2DocFx CLI utility that integrates Git repository cloning
/// with DocFx build and serve operations.
/// </summary>
internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        // Create root command
        var rootCommand = new RootCommand("Git2DocFx - A CLI utility for running DocFx build and serve operations on Git repositories");

        // Create build command
        var buildCommand = CreateBuildCommand();
        rootCommand.AddCommand(buildCommand);

        // Create serve command
        var serveCommand = CreateServeCommand();
        rootCommand.AddCommand(serveCommand);

        // Execute
        return await rootCommand.InvokeAsync(args);
    }

    private static Command CreateBuildCommand()
    {
        var buildCommand = new Command("build", "Clone a Git repository and run DocFx build");
        
        var repoUrlArg = new Argument<string>("repo-url", "The git repository URL");
        var docfxPathArg = new Argument<string>("docfx-path", "Path to docfx.json within the repository");
        var branchOption = new Option<string?>("--branch", "Branch to clone (default: remote HEAD)");
        var outputOption = new Option<string?>("--output", "Output directory for cloned repository");
        var silentOption = new Option<bool>("--silent", "Suppress progress output");
        var keepTempOption = new Option<bool>("--keep-temp", "Keep temporary files after build");

        buildCommand.AddArgument(repoUrlArg);
        buildCommand.AddArgument(docfxPathArg);
        buildCommand.AddOption(branchOption);
        buildCommand.AddOption(outputOption);
        buildCommand.AddOption(silentOption);
        buildCommand.AddOption(keepTempOption);

        buildCommand.SetHandler(async (string repoUrl, string docfxPath, string? branch, string? output,  bool silent, bool keepTemp) =>
        {
            try
            {
                var tempDir = await CloneRepository(repoUrl, docfxPath, branch, output, silent);
                var docfxJsonPath = Path.Combine(output, docfxPath);
                var docfxDirectory = Path.GetDirectoryName(docfxJsonPath)!;

                if (!silent)
                {
                    Console.WriteLine($"Running DocFx build in: {docfxDirectory}");
                }

                await RunDocFxCommand("build", docfxJsonPath, silent);

                if (!keepTemp && string.IsNullOrEmpty(output))
                {
                    if (!silent)
                    {
                        Console.WriteLine("Cleaning up temporary files...");
                    }
                    Directory.Delete(tempDir, true);
                }
                else if (!silent)
                {
                    Console.WriteLine($"Repository files kept at: {tempDir}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.Exit(1);
            }
        }, repoUrlArg, docfxPathArg, branchOption, outputOption, silentOption, keepTempOption);

        return buildCommand;
    }

    private static Command CreateServeCommand()
    {
        var serveCommand = new Command("serve", "Clone a Git repository and run DocFx serve");
        
        var repoUrlArg = new Argument<string>("repo-url", "The git repository URL");
        var docfxPathArg = new Argument<string>("docfx-path", "Path to docfx.json within the repository");
        var branchOption = new Option<string?>("--branch", "Branch to clone (default: remote HEAD)");
        var outputOption = new Option<string?>("--output", "Output directory for cloned repository");
        var silentOption = new Option<bool>("--silent", "Suppress progress output");
        var portOption = new Option<int?>("--port", "Port for the DocFx serve command");

        serveCommand.AddArgument(repoUrlArg);
        serveCommand.AddArgument(docfxPathArg);
        serveCommand.AddOption(branchOption);
        serveCommand.AddOption(outputOption);
        serveCommand.AddOption(silentOption);
        serveCommand.AddOption(portOption);

        serveCommand.SetHandler(async (string repoUrl, string docfxPath, string? branch, string? output, bool silent, int? port) =>
        {
            try
            {
                var tempDir = await CloneRepository(repoUrl, docfxPath, branch, output, silent);
                var docfxJsonPath = Path.Combine(tempDir, docfxPath);
                var docfxDirectory = Path.GetDirectoryName(docfxJsonPath)!;

                if (!silent)
                {
                    Console.WriteLine($"Running DocFx serve in: {docfxDirectory}");
                }

                if (!silent)
                {
                    Console.WriteLine("Starting DocFx server...");
                    Console.WriteLine("Press Ctrl+C to stop the server and clean up files...");
                }

                // Handle Ctrl+C to clean up temporary files
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    if (string.IsNullOrEmpty(output))
                    {
                        if (!silent)
                        {
                            Console.WriteLine("\nCleaning up temporary files...");
                        }
                        try
                        {
                            Directory.Delete(tempDir, true);
                        }
                        catch
                        {
                            // Ignore cleanup errors
                        }
                    }
                    Environment.Exit(0);
                };

                await RunDocFxServeCommand(docfxJsonPath, port, silent);

                // Clean up if using temporary directory
                if (string.IsNullOrEmpty(output))
                {
                    if (!silent)
                    {
                        Console.WriteLine("Cleaning up temporary files...");
                    }
                    Directory.Delete(tempDir, true);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.Exit(1);
            }
        }, repoUrlArg, docfxPathArg, branchOption, outputOption, silentOption, portOption);

        return serveCommand;
    }

    private static async Task<string> CloneRepository(string repoUrl, string docfxPath, string? branch, string? output, bool silent)
    {
        string outputDir;
        
        if (!string.IsNullOrEmpty(output))
        {
            outputDir = Path.GetFullPath(output);
        }
        else
        {
            // Create temporary directory
            var repoName = Path.GetFileNameWithoutExtension(new Uri(repoUrl).AbsolutePath.TrimEnd('/'));
            outputDir = Path.Combine(Path.GetTempPath(), $"git2docfx_{repoName}_{Guid.NewGuid():N}");
        }

        if (!silent)
        {
            Console.WriteLine($"Cloning repository: {repoUrl}");
            Console.WriteLine($"DocFx config: {docfxPath}");
            Console.WriteLine($"Output directory: {outputDir}");
            if (!string.IsNullOrEmpty(branch))
            {
                Console.WriteLine($"Branch: {branch}");
            }
            Console.WriteLine();
        }

        IGitOperationCallback callback = silent ? new SilentGitOperationCallback() : new ConsoleGitOperationCallback();
        var gitUtility = new GitCloningUtility(outputDir, callback);
        var integration = new DocFxGitIntegration(gitUtility, callback);

        var result = await integration.CloneAndParseAsync(repoUrl, docfxPath, branch);

        if (!silent)
        {
            Console.WriteLine($"Successfully cloned and parsed DocFx project");
            Console.WriteLine($"Total files: {result.Files.Count}");
            Console.WriteLine($"Checked out files: {gitUtility.GetCheckedOutFiles().Count}");
            Console.WriteLine();
        }

        return outputDir;
    }

    private static async Task RunDocFxCommand(string command, string docfxJsonPath, bool silent)
    {
        var docfxDirectory = Path.GetDirectoryName(docfxJsonPath)!;
        var docfxJsonFile = Path.GetFileName(docfxJsonPath);

        var arguments = $"{command} \"{docfxJsonFile}\"";

        var processInfo = new ProcessStartInfo
        {
            FileName = "docfx",
            Arguments = arguments,
            WorkingDirectory = docfxDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = !silent,
            RedirectStandardError = true,
            CreateNoWindow = silent
        };

        using var process = new Process { StartInfo = processInfo };
        
        if (!silent)
        {
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    Console.WriteLine(e.Data);
                }
            };
        }

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                Console.Error.WriteLine(e.Data);
            }
        };

        process.Start();

        if (!silent)
        {
            process.BeginOutputReadLine();
        }
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"DocFx {command} command failed with exit code {process.ExitCode}");
        }
    }

    private static async Task RunDocFxServeCommand(string docfxJsonPath, int? port, bool silent)
    {
        var docfxDirectory = Path.GetDirectoryName(docfxJsonPath)!;
        var docfxJsonFile = Path.GetFileName(docfxJsonPath);

        var arguments = $"--serve \"{docfxJsonPath}\"";
        
        if (port.HasValue)
        {
            arguments += $" --port {port.Value}";
        }

        var processInfo = new ProcessStartInfo
        {
            FileName = "docfx",
            Arguments = arguments,
            WorkingDirectory = docfxDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = !silent,
            RedirectStandardError = true,
            CreateNoWindow = silent
        };

        using var process = new Process { StartInfo = processInfo };
        
        if (!silent)
        {
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    Console.WriteLine(e.Data);
                }
            };
        }

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                Console.Error.WriteLine(e.Data);
            }
        };

        process.Start();

        if (!silent)
        {
            process.BeginOutputReadLine();
        }
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"DocFx serve command failed with exit code {process.ExitCode}");
        }
    }
}