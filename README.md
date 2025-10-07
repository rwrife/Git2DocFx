# Git2DocFx

A C# CLI utility for running DocFx build and serve operations on Git repositories. This tool leverages the DocFxClone utility to intelligently clone repositories with sparse checkout and run DocFx commands.

## Features

- **Smart Repository Cloning**: Uses sparse checkout to only download necessary files
- **DocFx Integration**: Runs `docfx --build` and `docfx --serve` commands seamlessly
- **Auto-build for Serve**: The serve command automatically builds documentation before serving
- **Branch Support**: Optionally specify a Git branch to clone
- **Temporary File Management**: Automatically cleans up temporary files after operations
- **Flexible Output**: Choose custom output directories or use temporary locations

## Prerequisites

- .NET 8.0 or later
- DocFx CLI tool installed and available in PATH
- Git installed and available in PATH

## Installation

1. Clone this repository
2. Build the project:
   ```bash
   dotnet build
   ```

## Usage

### Build Command

Clone a Git repository and run DocFx build:

```bash
git2docfx build <repo-url> <docfx-path> [options]
```

**Arguments:**
- `repo-url`: The Git repository URL (e.g., https://github.com/user/repo.git)
- `docfx-path`: Path to docfx.json within the repository (e.g., docs/docfx.json)

**Options:**
- `--branch <branch>`: Branch to clone (default: remote HEAD)
- `--output <directory>`: Output directory for cloned repository
- `--docfx-output <directory>`: Output directory for DocFx build files
- `--silent`: Suppress progress output
- `--keep-temp`: Keep temporary files after build

**Example:**
```bash
git2docfx build https://github.com/dotnet/docfx.git docfx.json --branch main
```

### Serve Command

Clone a Git repository, build the documentation, and run DocFx serve:

```bash
git2docfx serve <repo-url> <docfx-path> [options]
```

**Arguments:**
- `repo-url`: The Git repository URL
- `docfx-path`: Path to docfx.json within the repository

**Options:**
- `--branch <branch>`: Branch to clone (default: remote HEAD)
- `--output <directory>`: Output directory for cloned repository
- `--docfx-output <directory>`: Output directory for DocFx build files
- `--silent`: Suppress progress output
- `--port <port>`: Port for the DocFx serve command

**Note:** The serve command automatically runs `docfx build` before starting the server.

**Example:**
```bash
git2docfx serve https://github.com/dotnet/docfx.git docfx.json --port 8080
```

## How It Works

1. **Repository Analysis**: The tool uses the DocFxClone utility to parse the docfx.json file and determine which files are needed
2. **Sparse Checkout**: Only the necessary files are downloaded from the Git repository
3. **DocFx Execution**: The appropriate DocFx command is executed in the cloned repository directory
4. **Cleanup**: Temporary files are automatically cleaned up (unless `--keep-temp` is used or `--output` is specified)

## Command Examples

### Build a documentation site from a specific branch:
```bash
git2docfx build https://github.com/myorg/docs.git docs/docfx.json --branch develop --output ./my-docs --docfx-output ./docs-output
```

### Serve documentation locally on a custom port:
```bash
git2docfx serve https://github.com/myorg/docs.git docs/docfx.json --port 9000 --docfx-output ./docs-output
```

### Silent build (minimal output):
```bash
git2docfx build https://github.com/myorg/docs.git docs/docfx.json --silent
```

### Understanding the Output Parameters

- `--output`: Specifies where to clone the Git repository (if not specified, uses a temporary directory)
- `--docfx-output`: Specifies where DocFx should place the generated documentation files (if not specified, uses DocFx's default output location)

## Integration with DocFxClone

This utility leverages the DocFxClone project to provide intelligent repository cloning. The DocFxClone utility:

- Performs sparse Git checkouts to minimize download time
- Analyzes DocFx configuration files to determine dependencies
- Checks out files on-demand as they are referenced
- Provides progress callbacks and detailed logging

## License

This project follows the same license as the parent repository.
