using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Cryptography;
using System.Text.Json;
using BepInEx.Logging;

namespace BepInEx.Unity.IL2CPP;

internal static class RepositoryPluginValidator
{
    private static readonly Dictionary<string, string> ResolvableAssemblies =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, RepositoryAllowlistEntry> ExpectedDirectories =
        new(StringComparer.Ordinal);
    private static string StagingRoot;

    internal static void Initialize(string modsRoot)
    {
        Initialize(modsRoot, StarlightInterop.GetRepositoryModAllowlist(),
            Path.Combine(Paths.CachePath, "starlight-repository"));
    }

    internal static void Initialize(string modsRoot, string serializedAllowlist, string stagingRoot)
    {
        ResolvableAssemblies.Clear();
        ExpectedDirectories.Clear();
        StagingRoot = Path.GetFullPath(stagingRoot);
        if (Directory.Exists(StagingRoot))
            Directory.Delete(StagingRoot, true);
        Directory.CreateDirectory(StagingRoot);

        var root = Path.GetFullPath(modsRoot);
        var allowlist = JsonSerializer.Deserialize<RepositoryAllowlist>(serializedAllowlist)
                        ?? throw new SecurityException("Native repository mod allowlist is invalid");

        foreach (var entry in allowlist.mods)
        {
            if (!IsSafeRelativeDirectory(entry.directory))
                throw new SecurityException(
                    $"Native repository allowlist contains an invalid directory: '{entry.directory}'");

            var directory = Path.GetFullPath(Path.Combine(root,
                entry.directory.Replace('/', Path.DirectorySeparatorChar)));
            var expectedPrefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!directory.StartsWith(expectedPrefix, StringComparison.Ordinal))
                throw new SecurityException(
                    $"Native repository allowlist directory escapes starlight_mods: '{entry.directory}'");
            if (!ExpectedDirectories.TryAdd(directory, entry))
                throw new SecurityException(
                    $"Native repository allowlist contains a duplicate directory: '{entry.directory}'");
        }
    }

    internal static RepositoryValidationResult ValidateDirectory(string directory)
    {
        var root = Path.GetFullPath(directory);
        if (!ExpectedDirectories.TryGetValue(root, out var entry))
            throw new SecurityException(
                $"Repository directory is absent from the native launch allowlist: '{root}'");

        var primaryPath = ResolveLeafFile(root, entry.fileName);
        var primaryBytes = ReadVerifiedBytes(primaryPath, entry.sha256);

        var allowedSourceFiles = new HashSet<string>(StringComparer.Ordinal) { primaryPath };
        var verifiedFiles = new List<(string FileName, byte[] Contents)>
        {
            (entry.fileName, primaryBytes)
        };

        foreach (var dependency in ReadDeclaredDependencies(primaryBytes))
        {
            var dependencyPath = ResolveLeafFile(root, dependency.FileName);
            var dependencyBytes = ReadVerifiedBytes(dependencyPath, dependency.Sha256);
            if (!allowedSourceFiles.Add(dependencyPath))
                throw new SecurityException($"Duplicate dependency declaration for '{dependency.FileName}'");

            verifiedFiles.Add((dependency.FileName, dependencyBytes));
        }

        foreach (var dll in Directory.GetFiles(root, "*.dll", SearchOption.AllDirectories))
        {
            var fullPath = Path.GetFullPath(dll);
            if (allowedSourceFiles.Contains(fullPath))
                continue;

            File.Delete(fullPath);
            Logger.Log(LogLevel.Warning,
                       $"Deleted undeclared repository DLL '{Path.GetRelativePath(root, fullPath)}'");
        }

        var loadDirectory = Path.GetFullPath(Path.Combine(StagingRoot,
            entry.directory.Replace('/', Path.DirectorySeparatorChar)));
        var stagingPrefix = StagingRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!loadDirectory.StartsWith(stagingPrefix, StringComparison.Ordinal))
            throw new SecurityException($"Repository staging directory escapes its root: '{entry.directory}'");
        Directory.CreateDirectory(loadDirectory);

        var allowedStagedFiles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var verifiedFile in verifiedFiles)
        {
            var stagedPath = Path.GetFullPath(Path.Combine(loadDirectory, verifiedFile.FileName));
            File.WriteAllBytes(stagedPath, verifiedFile.Contents);
            allowedStagedFiles.Add(stagedPath);
            RegisterResolvableAssembly(stagedPath);
        }

        return new RepositoryValidationResult(loadDirectory, allowedStagedFiles);
    }

    internal static bool TryResolve(AssemblyName assemblyName, out Assembly assembly)
    {
        assembly = null;
        if (assemblyName?.Name == null || !ResolvableAssemblies.TryGetValue(assemblyName.Name, out var path))
            return false;

        assembly = Assembly.LoadFrom(path);
        return true;
    }

    private static IEnumerable<DeclaredDependency> ReadDeclaredDependencies(byte[] primaryBytes)
    {
        // Load the already verified bytes so the runtime cannot probe the source DLL's directory
        // and resolve an undeclared companion before validation has removed it.
        var assembly = Assembly.Load(primaryBytes);
        var manifestType = assembly.GetType("Starlight.Dependencies", false, false);
        if (manifestType == null)
            return Array.Empty<DeclaredDependency>();

        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        object value = manifestType.GetField("Files", flags)?.GetValue(null)
                    ?? manifestType.GetProperty("Files", flags)?.GetValue(null);
        if (value == null)
            throw new SecurityException("Starlight.Dependencies must expose a static Files array");
        if (value is not string[][] entries)
            throw new SecurityException("Starlight.Dependencies.Files must be a string[][] array");

        return entries.Select((entry, index) =>
        {
            if (entry is not { Length: 2 })
                throw new SecurityException(
                    $"Starlight.Dependencies.Files[{index}] must contain exactly a filename and SHA-256 hash");
            return new DeclaredDependency(entry[0], entry[1]);
        }).ToArray();
    }

    private static string ResolveLeafFile(string root, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)
         || fileName != Path.GetFileName(fileName)
         || fileName.IndexOfAny(new[] { '/', '\\', '\0' }) >= 0
         || !fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            throw new SecurityException($"Invalid repository DLL filename: '{fileName}'");

        var path = Path.GetFullPath(Path.Combine(root, fileName));
        var expectedPrefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(expectedPrefix, StringComparison.Ordinal))
            throw new SecurityException($"Repository DLL escapes its version directory: '{fileName}'");
        if (!File.Exists(path))
            throw new SecurityException($"Declared repository DLL is missing: '{fileName}'");

        return path;
    }

    private static bool IsSafeRelativeDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || directory.Contains('\\') || directory.Contains('\0'))
            return false;

        var components = directory.Split('/');
        return components.Length == 2 && components.All(component =>
            !string.IsNullOrWhiteSpace(component) && component != "." && component != "..");
    }

    private static byte[] ReadVerifiedBytes(string path, string expectedHash)
    {
        if (expectedHash == null || expectedHash.Length != 64 ||
            expectedHash.Any(character => !Uri.IsHexDigit(character)))
            throw new SecurityException($"Invalid SHA-256 declaration for '{Path.GetFileName(path)}'");

        var contents = File.ReadAllBytes(path);
        var actualHash = Convert.ToHexString(SHA256.HashData(contents));
        if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new SecurityException($"SHA-256 mismatch for '{Path.GetFileName(path)}'");

        return contents;
    }

    private static void RegisterResolvableAssembly(string path)
    {
        var assemblyName = AssemblyName.GetAssemblyName(path).Name
                           ?? throw new SecurityException($"DLL has no assembly name: '{path}'");
        if (ResolvableAssemblies.TryGetValue(assemblyName, out var existingPath)
         && !existingPath.Equals(path, StringComparison.Ordinal))
            throw new SecurityException(
                $"Multiple repository DLLs use the assembly name '{assemblyName}'");

        ResolvableAssemblies[assemblyName] = path;
    }

    private sealed class RepositoryAllowlistEntry
    {
        public string directory { get; set; }
        public string fileName { get; set; }
        public string sha256 { get; set; }
    }

    private sealed class RepositoryAllowlist
    {
        public RepositoryAllowlistEntry[] mods { get; set; } = Array.Empty<RepositoryAllowlistEntry>();
    }

    private readonly record struct DeclaredDependency(string FileName, string Sha256);
    internal readonly record struct RepositoryValidationResult(
        string LoadDirectory,
        IReadOnlySet<string> AllowedFiles);
}
