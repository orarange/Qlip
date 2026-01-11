using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace QlipInstallerBuilder;

internal sealed class BuilderCore
{
    private readonly Action<string> _log;

    public string? QlipSrcDir { get; set; }

    public BuilderCore(Action<string>? log = null, bool logToConsole = false)
    {
        if (log != null)
        {
            _log = log;
        }
        else if (logToConsole)
        {
            _log = Console.WriteLine;
        }
        else
        {
            _log = _ => { };
        }
    }

    public async Task BuildAsync(string configuration, CancellationToken ct = default)
    {
        string installerRepoRoot = FindInstallerRepoRootOrThrow();
        string installerDir = Path.Combine(installerRepoRoot, "installer");
        string workDir = Path.Combine(installerDir, "work");
        string publishDir = Path.Combine(installerDir, "publish");
        string distDir = Path.Combine(installerDir, "dist");

        string qlipSrcRoot = FindQlipSrcRepoRootOrThrow(QlipSrcDir);

        _log($"Installer repo: {installerRepoRoot}");
        _log($"Qlip-src repo:  {qlipSrcRoot}");

        SafeDeleteDir(workDir);
        SafeDeleteDir(publishDir);
        Directory.CreateDirectory(workDir);
        Directory.CreateDirectory(publishDir);
        Directory.CreateDirectory(distDir);

        await PublishQlipAsync(qlipSrcRoot, publishDir, configuration, ct);
        await DownloadAndBundleFfmpegAsync(installerRepoRoot, workDir, publishDir, ct);

        string packageZipPath = Path.Combine(distDir, "Qlip_win-x64.zip");
        CreateZipPackage(publishDir, packageZipPath);
        _log("Package zip: " + packageZipPath);

        bool installerBuilt = await TryBuildInnoSetupAsync(installerDir, ct);
        if (!installerBuilt)
        {
            _log("Inno Setup (ISCC.exe) が見つかりません。セットアップEXEは未生成です。");
        }
    }

    private void CreateZipPackage(string publishDir, string outZipPath)
    {
        _log("Creating zip package...");

        try
        {
            if (File.Exists(outZipPath))
                File.Delete(outZipPath);
        }
        catch
        {
            // best-effort
        }

        ZipFile.CreateFromDirectory(publishDir, outZipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
    }

    public static string FindInstallerRepoRootOrThrow()
    {
        string? dir = AppContext.BaseDirectory;
        for (int i = 0; i < 12 && !string.IsNullOrWhiteSpace(dir); i++)
        {
            if (File.Exists(Path.Combine(dir, "installer", "Qlip.iss")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }

        // fallback: current working directory
        dir = Directory.GetCurrentDirectory();
        for (int i = 0; i < 12 && !string.IsNullOrWhiteSpace(dir); i++)
        {
            if (File.Exists(Path.Combine(dir, "installer", "Qlip.iss")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException("installer\\Qlip.iss が見つかりません。インストーラリポジトリ配下で実行してください。");
    }

    public static string FindQlipSrcRepoRootOrThrow(string? explicitDir)
    {
        if (!string.IsNullOrWhiteSpace(explicitDir))
        {
            string full = Path.GetFullPath(explicitDir);
            if (File.Exists(Path.Combine(full, "Qlip.csproj")))
                return full;
            if (File.Exists(Path.Combine(full, "Qlip.sln")) && File.Exists(Path.Combine(full, "Qlip.csproj")))
                return full;

            throw new InvalidOperationException($"指定されたQlip-srcのパスに Qlip.csproj が見つかりません: {full}");
        }

        string? env = Environment.GetEnvironmentVariable("QLIP_SRC_DIR");
        if (!string.IsNullOrWhiteSpace(env))
        {
            string full = Path.GetFullPath(env);
            if (File.Exists(Path.Combine(full, "Qlip.csproj")))
                return full;
        }

        // Common local layout: sibling repo folder "Qlip-src"
        string cwd = Directory.GetCurrentDirectory();
        for (int i = 0; i < 6; i++)
        {
            string candidate = Path.Combine(cwd, "Qlip-src");
            if (File.Exists(Path.Combine(candidate, "Qlip.csproj")))
                return candidate;

            string? parent = Directory.GetParent(cwd)?.FullName;
            if (string.IsNullOrWhiteSpace(parent))
                break;
            cwd = parent;
        }

        throw new InvalidOperationException("Qlip-src が見つかりません。GUIでフォルダ選択するか、環境変数 QLIP_SRC_DIR を設定してください。");
    }

    private async Task PublishQlipAsync(string qlipSrcRoot, string publishDir, string configuration, CancellationToken ct)
    {
        _log("Publishing Qlip (self-contained)...");

        string args = string.Join(' ', new[]
        {
            "publish",
            Quote(Path.Combine(qlipSrcRoot, "Qlip.csproj")),
            "-c", configuration,
            "-r", "win-x64",
            "-p:SelfContained=true",
            "-p:PublishTrimmed=false",
            "-o", Quote(publishDir),
        });

        await RunProcessStreamingAsync("dotnet", args, qlipSrcRoot, ct);
    }

    private async Task DownloadAndBundleFfmpegAsync(string repoRoot, string workDir, string publishDir, CancellationToken ct)
    {
        _log("Downloading FFmpeg (LGPL build)...");

        const string zipUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-lgpl.zip";
        string zipPath = Path.Combine(workDir, "ffmpeg-win64-lgpl.zip");
        string extractDir = Path.Combine(workDir, "ffmpeg-extract");

        SafeDeleteDir(extractDir);
        Directory.CreateDirectory(extractDir);

        await DownloadFileAsync(zipUrl, zipPath, ct);

        _log("Extracting zip...");
        ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);

        string? ffmpegExe = Directory.EnumerateFiles(extractDir, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault();
        if (ffmpegExe == null)
            throw new InvalidOperationException("ffmpeg.exe がzip内に見つかりません。");

        string ffmpegTargetDir = Path.Combine(publishDir, "ffmpeg");
        string ffmpegLicDir = Path.Combine(ffmpegTargetDir, "licenses");
        Directory.CreateDirectory(ffmpegTargetDir);
        Directory.CreateDirectory(ffmpegLicDir);

        File.Copy(ffmpegExe, Path.Combine(ffmpegTargetDir, "ffmpeg.exe"), overwrite: true);

        foreach (string lf in EnumerateLikelyLicenseFiles(extractDir))
        {
            try
            {
                File.Copy(lf, Path.Combine(ffmpegLicDir, Path.GetFileName(lf)), overwrite: true);
            }
            catch { }
        }

        // Ship notices next to the exe
        string rootNotices = Path.Combine(repoRoot, "THIRD-PARTY-NOTICES.txt");
        if (File.Exists(rootNotices))
            File.Copy(rootNotices, Path.Combine(publishDir, "THIRD-PARTY-NOTICES.txt"), overwrite: true);

        _log("FFmpeg bundled: " + Path.Combine(ffmpegTargetDir, "ffmpeg.exe"));
    }

    private static IEnumerable<string> EnumerateLikelyLicenseFiles(string root)
    {
        foreach (string p in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(p);
            if (name.StartsWith("LICENSE", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("COPYING", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("README", StringComparison.OrdinalIgnoreCase))
            {
                yield return p;
            }
        }
    }

    private async Task<bool> TryBuildInnoSetupAsync(string installerDir, CancellationToken ct)
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string[] possibleIscc = new[]
        {
            @"C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
            @"C:\Program Files\Inno Setup 6\ISCC.exe",
            Path.Combine(localAppData, "Programs", "Inno Setup 6", "ISCC.exe"),
        };

        string? iscc = possibleIscc.FirstOrDefault(File.Exists);
        if (iscc == null)
            return false;

        _log("Building installer with Inno Setup...");
        await RunProcessStreamingAsync(iscc, Quote(Path.Combine(installerDir, "Qlip.iss")), installerDir, ct);
        return true;
    }

    private async Task DownloadFileAsync(string url, string outPath, CancellationToken ct)
    {
        using var http = new HttpClient();
        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        await using var inStream = await resp.Content.ReadAsStreamAsync(ct);
        await using var outStream = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.Read);

        byte[] buffer = new byte[1024 * 128];
        while (true)
        {
            int n = await inStream.ReadAsync(buffer, ct);
            if (n <= 0)
                break;
            await outStream.WriteAsync(buffer.AsMemory(0, n), ct);
        }
    }

    private async Task RunProcessStreamingAsync(string fileName, string arguments, string workingDir, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var p = new Process { StartInfo = psi };
        p.Start();

        Task pumpOut = Task.Run(async () =>
        {
            while (!p.StandardOutput.EndOfStream)
            {
                string? line = await p.StandardOutput.ReadLineAsync(ct);
                if (line != null) _log(line);
            }
        }, ct);

        Task pumpErr = Task.Run(async () =>
        {
            while (!p.StandardError.EndOfStream)
            {
                string? line = await p.StandardError.ReadLineAsync(ct);
                if (line != null) _log(line);
            }
        }, ct);

        await Task.WhenAll(pumpOut, pumpErr, p.WaitForExitAsync(ct));

        if (p.ExitCode != 0)
            throw new InvalidOperationException($"Process failed: {fileName} (ExitCode={p.ExitCode})");
    }

    private static string Quote(string s) => s.Contains(' ') ? '"' + s + '"' : s;

    private static void SafeDeleteDir(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // best-effort
        }
    }
}
