using System;
using System.IO;
using System.Linq;
using Kettu;
using LBPUnion.ProjectLighthouse.Logging;
using LBPUnion.ProjectLighthouse.Types.Settings;

namespace LBPUnion.ProjectLighthouse.Helpers;

public static class VersionHelper
{
    static VersionHelper()
    {
        try
        {
            CommitHash = readManifestFile("gitVersion.txt");
            Branch = readManifestFile("gitBranch.txt");

            string remotesFile = readManifestFile("gitRemotes.txt");

            string[] lines = remotesFile.Split('\n');

            // line[0] line[1]                                        line[2]
            // origin  git@github.com:LBPUnion/project-lighthouse.git (fetch)

            // linq is a serious and painful catastrophe but its useful so i'm gonna keep using it
            Remotes = lines.Select(line => line.Split("\t")[1]).ToArray();

            CommitsOutOfDate = readManifestFile("gitUnpushed.txt").Split('\n').Length;

            CanCheckForUpdates = true;
        }
        catch
        {
            Logger.Log
            (
                "Project Lighthouse was built incorrectly. Please make sure git is available when building. " +
                "Because of this, you will not be notified of updates.",
                LoggerLevelStartup.Instance
            );
            CommitHash = "invalid";
            Branch = "invalid";
            CanCheckForUpdates = false;
        }

        if (IsDirty)
        {
            Logger.Log
            (
                "This is a modified version of Project Lighthouse. " +
                "Please make sure you are properly disclosing the source code to any users who may be using this instance.",
                LoggerLevelStartup.Instance
            );
            CanCheckForUpdates = false;
        }
    }

    private static string readManifestFile(string fileName)
    {
        using Stream stream = typeof(Program).Assembly.GetManifestResourceStream($"{typeof(Program).Namespace}.{fileName}");
        using StreamReader reader = new(stream ?? throw new Exception("The assembly or manifest resource is null."));

        return reader.ReadToEnd().Trim();
    }

    public static string CommitHash { get; set; }
    public static string Branch { get; set; }
    public static string FullVersion => $"{ServerStatics.ServerName} {Branch}@{CommitHash} {Build}";
    public static bool IsDirty => CommitHash.EndsWith("-dirty") || CommitsOutOfDate != 1 || CommitHash == "invalid" || Branch == "invalid";
    public static int CommitsOutOfDate { get; set; }
    public static bool CanCheckForUpdates { get; set; }
    public static string[] Remotes { get; set; }

    public const string Build =
        #if DEBUG
            "Debug";
        #elif RELEASE
        "Release";
    #else
            "Unknown";
    #endif
}