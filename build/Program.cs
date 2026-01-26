#pragma warning disable CS1591
// ReSharper disable ClassNeverInstantiated.Global
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Cake.Common;
using Cake.Common.IO;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Build;
using Cake.Common.Tools.DotNet.MSBuild;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;
using Cake.Git;
using Cake.Json;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;

return new CakeHost()
       .UseContext<BuildContext>()
       .Run(args);

public class BuildContext : FrostingContext
{
    public enum ProjectBuildType
    {
        Release,
        Development,
        BleedingEdge,
        Starlight
    }

    internal readonly DistributionTarget[] Distributions =
    [
        new("Unity.IL2CPP", "android-arm64"),
        new("Unity.IL2CPP", "android-arm")
    ];


    public BuildContext(ICakeContext ctx)
        : base(ctx)
    {
        RootDirectory = ctx.Environment.WorkingDirectory.GetParent();
        OutputDirectory = RootDirectory.Combine("bin");
        DistributionDirectory = OutputDirectory.Combine("dist");
        var props = Project.FromFile(RootDirectory.CombineWithFilePath("Directory.Build.props").FullPath,
                                     new ProjectOptions());
        VersionPrefix = props.GetPropertyValue("VersionPrefix");
        CurrentCommit = ctx.GitLogTip(RootDirectory);

        BuildType = ctx.Argument("build-type", ProjectBuildType.Development);
        BuildId = ctx.Argument("build-id", -1);
        LastBuildCommit = ctx.Argument("last-build-commit", "");
    }

    public ProjectBuildType BuildType { get; }
    public int BuildId { get; }
    public string LastBuildCommit { get; }

    public DirectoryPath RootDirectory { get; }
    public DirectoryPath OutputDirectory { get; }
    public DirectoryPath DistributionDirectory { get; }

    public string VersionPrefix { get; }
    public GitCommit CurrentCommit { get; }

    public string VersionSuffix => BuildType switch
    {
        ProjectBuildType.Release      => "",
        ProjectBuildType.Development  => "dev",
        ProjectBuildType.BleedingEdge => $"be.{BuildId}",
        ProjectBuildType.Starlight    => $"starlight.{BuildId}",
        var _                         => throw new ArgumentOutOfRangeException()
    };

    public string BuildPackageVersion =>
        VersionPrefix + BuildType switch
        {
            ProjectBuildType.Release => "",
            var _                    => $"-{VersionSuffix}+{this.GitShortenSha(RootDirectory, CurrentCommit)}",
        };
}

[TaskName("Clean")]
public sealed class CleanTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext ctx)
    {
        ctx.CreateDirectory(ctx.OutputDirectory);
        ctx.CleanDirectory(ctx.OutputDirectory,
                           f => !f.Path.FullPath.Contains(".dep_cache"));

        ctx.Log.Information("Cleaning up old build objects");
        ctx.CleanDirectories(ctx.RootDirectory.Combine("**/BepInEx.*/**/bin").FullPath);
        ctx.CleanDirectories(ctx.RootDirectory.Combine("**/BepInEx.*/**/obj").FullPath);
    }
}

[TaskName("Compile")]
[IsDependentOn(typeof(CleanTask))]
public sealed class CompileTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext ctx)
    {
        var buildSettings = new DotNetBuildSettings
        {
            Configuration = "Release"
        };
        if (ctx.BuildType != BuildContext.ProjectBuildType.Release)
        {
            buildSettings.MSBuildSettings = new DotNetMSBuildSettings
            {
                VersionSuffix = ctx.VersionSuffix,
                Properties =
                {
                    ["SourceRevisionId"] = [ctx.CurrentCommit.Sha],
                    ["RepositoryBranch"] = [ctx.GitBranchCurrent(ctx.RootDirectory).FriendlyName]
                }
            };
        }

        ctx.DotNetBuild(ctx.RootDirectory.FullPath, buildSettings);
    }
}

[TaskName("MakeDist")]
[IsDependentOn(typeof(CompileTask))]
public sealed class MakeDistTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext ctx)
    {
        ctx.CreateDirectory(ctx.DistributionDirectory);
        ctx.CleanDirectory(ctx.DistributionDirectory);

        var latestTag = ctx.Git("describe --tags --abbrev=0");
        var changelog = new StringBuilder()
                        .AppendLine(
                                    $"{ctx.Git($"rev-list --count {latestTag}..HEAD")} changes since {latestTag}")
                        .AppendLine()
                        .AppendLine("Changelog (excluding merge commits):")
                        .AppendLine(ctx.Git(
                                            $"--no-pager log --no-merges --pretty=\"format:* (%h) [%an] %s\" {latestTag}..HEAD",
                                            Environment.NewLine))
                        .ToString();


        foreach (var dist in ctx.Distributions)
        {
            ctx.Log.Information($"Creating distribution {dist.Target}");
            var targetDir = ctx.DistributionDirectory.Combine(dist.Target);
            ctx.CreateDirectory(targetDir);
            ctx.CleanDirectory(targetDir);

            var bepInExDir = targetDir.Combine("BepInEx");
            var bepInExCoreDir = bepInExDir.Combine("core");
            ctx.CreateDirectory(bepInExDir);
            ctx.CreateDirectory(bepInExCoreDir);
            ctx.CreateDirectory(bepInExDir.Combine("plugins"));
            ctx.CreateDirectory(bepInExDir.Combine("patchers"));

            File.WriteAllText(targetDir.CombineWithFilePath("changelog.txt").FullPath, changelog);

            var sourceDirectory = ctx.OutputDirectory.Combine(dist.DistributionIdentifier);
            if (dist.FrameworkTarget != null)
                sourceDirectory = sourceDirectory.Combine(dist.FrameworkTarget);

            foreach (var filePath in ctx.GetFiles(sourceDirectory.Combine("*.*").FullPath))
                ctx.CopyFileToDirectory(filePath, bepInExCoreDir);
        }
    }
}

[TaskName("Publish")]
[IsDependentOn(typeof(MakeDistTask))]
public sealed class PublishTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext ctx)
    {
        ctx.Log.Information("Packing BepInEx");

        foreach (var dist in ctx.Distributions)
        {
            var targetZipName = $"BepInEx-{dist.Target}-{ctx.BuildPackageVersion}.zip";
            ctx.Log.Information($"Packing {targetZipName}");
            ctx.Zip(ctx.DistributionDirectory.Combine(dist.Target),
                    ctx.DistributionDirectory
                       .CombineWithFilePath(targetZipName));
        }


        var changeLog = "";
        if (!string.IsNullOrWhiteSpace(ctx.LastBuildCommit))
        {
            var changeLogContents =
                ctx.Git($"--no-pager log --no-merges --pretty=\"format:<li>(<code>%h</code>) [%an] %s</li>\" {ctx.LastBuildCommit}..HEAD",
                        "\n");
            changeLog = $"<ul>{changeLogContents}</ul>";
        }

        ctx.SerializeJsonToPrettyFile(ctx.DistributionDirectory.CombineWithFilePath("info.json"),
                                      new Dictionary<string, object>
                                      {
                                          ["id"] = ctx.BuildId.ToString(),
                                          ["date"] = DateTime.Now.ToString("o"),
                                          ["changelog"] = changeLog,
                                          ["hash"] = ctx.CurrentCommit.Sha,
                                          ["short_hash"] = ctx.GitShortenSha(ctx.RootDirectory, ctx.CurrentCommit),
                                          ["artifacts"] = ctx.Distributions.Select(d => new Dictionary<string, string>
                                          {
                                              ["file"] = $"BepInEx-{d.Target}-{ctx.BuildPackageVersion}.zip",
                                              ["description"] =
                                                  $"BepInEx {d.Engine} ({d.Runtime}{(d.FrameworkTarget == null ? "" : " " + d.FrameworkTarget)}) for {d.ClearOsName} ({d.Arch}) games"
                                          }).ToArray()
                                      });
    }
}

[TaskName("Default")]
[IsDependentOn(typeof(CompileTask))]
public class DefaultTask : FrostingTask;
