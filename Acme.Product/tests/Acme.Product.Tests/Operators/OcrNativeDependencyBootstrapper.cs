// OcrNativeDependencyBootstrapper.cs
// OCR 单元测试原生依赖加载引导器

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using PaddleOCRSharp;

namespace Acme.Product.Tests.Operators;

internal static class OcrNativeDependencyBootstrapper
{
    private static readonly object InitLock = new();
    private static bool _isInitialized;
    private static bool _nativeDependenciesPreloaded;
    private static IReadOnlyList<string> _nativeSearchDirectories = Array.Empty<string>();

    private static readonly HashSet<string> KnownNativeLibraries = new(StringComparer.OrdinalIgnoreCase)
    {
        "paddleocr",
        "paddle_inference",
        "opencv_world470",
        "common",
        "iomp5md",
        "mkldnn",
        "mklml",
        "yaml-cpp",
        "tbb12",
        "tbbmalloc",
        "tbbmalloc_proxy",
        "msvcp140",
        "msvcp140_1",
        "msvcp140_2",
        "vcruntime140",
        "vcruntime140_1",
        "concrt140",
        "vcomp140",
        "vcamp140",
        "vccorlib140",
        "onnxruntime",
        "opencvsharpextern"
    };

    private static readonly string[] NativePreloadOrder =
    {
        "vcruntime140",
        "vcruntime140_1",
        "msvcp140",
        "msvcp140_1",
        "msvcp140_2",
        "concrt140",
        "vcomp140",
        "vcamp140",
        "vccorlib140",
        "libiomp5md",
        "mklml",
        "mkldnn",
        "tbb12",
        "tbbmalloc",
        "tbbmalloc_proxy",
        "yaml-cpp",
        "common",
        "opencv_world470",
        "paddle_inference"
    };

    public static void Initialize()
    {
        lock (InitLock)
        {
            if (_isInitialized)
            {
                return;
            }

            var baseDirectory = AppContext.BaseDirectory;
            Environment.CurrentDirectory = baseDirectory;

            EnsureInferenceDirectory(baseDirectory);
            _nativeSearchDirectories = BuildNativeSearchDirectories(baseDirectory);

            try
            {
                NativeLibrary.SetDllImportResolver(typeof(PaddleOCREngine).Assembly, ResolveNativeLibrary);
            }
            catch (InvalidOperationException)
            {
                // 同一 Assembly 的 resolver 只能设置一次，已设置则沿用现有配置。
            }

            _isInitialized = true;
        }
    }

    private static IntPtr ResolveNativeLibrary(string libraryName, Assembly _, DllImportSearchPath? __)
    {
        if (string.IsNullOrWhiteSpace(libraryName))
        {
            return IntPtr.Zero;
        }

        if (Path.IsPathRooted(libraryName) && NativeLibrary.TryLoad(libraryName, out var directHandle))
        {
            return directHandle;
        }

        var normalizedName = NormalizeLibraryName(libraryName);
        if (!KnownNativeLibraries.Contains(normalizedName))
        {
            return IntPtr.Zero;
        }

        if (normalizedName.Equals("paddleocr", StringComparison.OrdinalIgnoreCase))
        {
            PreloadNativeDependencies();
        }

        if (TryLoadFromSearchDirectories(EnsureDllSuffix(libraryName), out var handle))
        {
            return handle;
        }

        return IntPtr.Zero;
    }

    private static void PreloadNativeDependencies()
    {
        lock (InitLock)
        {
            if (_nativeDependenciesPreloaded)
            {
                return;
            }

            foreach (var dependencyName in NativePreloadOrder)
            {
                TryLoadFromSearchDirectories(EnsureDllSuffix(dependencyName), out _);
            }

            _nativeDependenciesPreloaded = true;
        }
    }

    private static IReadOnlyList<string> BuildNativeSearchDirectories(string baseDirectory)
    {
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            baseDirectory,
            Path.Combine(baseDirectory, "runtimes", "win-x64", "native"),
            Path.Combine(baseDirectory, "runtimes", "win", "native")
        };

        foreach (var paddleDir in FindDirectoriesContainingFile(baseDirectory, "PaddleOCR.dll"))
        {
            directories.Add(paddleDir);
        }

        foreach (var nugetRoot in GetNuGetPackageRootCandidates())
        {
            AddLatestPackageSubDirectory(directories, nugetRoot, "paddleocrsharp", Path.Combine("build", "PaddleOCRLib"));
            AddLatestPackageSubDirectory(directories, nugetRoot, "paddle.runtime.win_x64", Path.Combine("build", "win_x64"));
        }

        return directories.Where(Directory.Exists).ToArray();
    }

    private static IEnumerable<string> FindDirectoriesContainingFile(string rootDirectory, string fileName)
    {
        if (!Directory.Exists(rootDirectory))
        {
            yield break;
        }

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(rootDirectory, fileName, SearchOption.AllDirectories);
        }
        catch
        {
            yield break;
        }

        foreach (var filePath in files)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                yield return directory;
            }
        }
    }

    private static void AddLatestPackageSubDirectory(
        ISet<string> directories,
        string nugetRoot,
        string packageId,
        string relativeSubDirectory)
    {
        var packageRoot = Path.Combine(nugetRoot, packageId);
        if (!Directory.Exists(packageRoot))
        {
            return;
        }

        var latestVersionDirectory = Directory
            .EnumerateDirectories(packageRoot)
            .OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (latestVersionDirectory is null)
        {
            return;
        }

        var candidate = Path.Combine(latestVersionDirectory, relativeSubDirectory);
        if (Directory.Exists(candidate))
        {
            directories.Add(candidate);
        }
    }

    private static bool TryLoadFromSearchDirectories(string libraryFileName, out IntPtr handle)
    {
        foreach (var directory in _nativeSearchDirectories)
        {
            var candidatePath = Path.Combine(directory, libraryFileName);
            if (!File.Exists(candidatePath))
            {
                continue;
            }

            if (NativeLibrary.TryLoad(candidatePath, out handle))
            {
                return true;
            }
        }

        handle = IntPtr.Zero;
        return false;
    }

    private static string NormalizeLibraryName(string libraryName)
    {
        var fileName = Path.GetFileNameWithoutExtension(libraryName);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = libraryName;
        }

        if (fileName.StartsWith("lib", StringComparison.OrdinalIgnoreCase) && fileName.Length > 3)
        {
            fileName = fileName[3..];
        }

        return fileName.ToLowerInvariant();
    }

    private static string EnsureDllSuffix(string libraryName)
    {
        return libraryName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? libraryName
            : $"{libraryName}.dll";
    }

    private static void EnsureInferenceDirectory(string baseDirectory)
    {
        var targetInferenceDirectory = Path.Combine(baseDirectory, "inference");
        if (ContainsInferenceFiles(targetInferenceDirectory))
        {
            return;
        }

        var sourceInferenceDirectory = FindInferenceSourceDirectory();
        if (sourceInferenceDirectory is null)
        {
            throw new DirectoryNotFoundException(
                "OCR 推理模型目录(inference)不存在，且未找到可用的 PaddleOCRSharp 包内模型资源。");
        }

        CopyDirectoryRecursively(sourceInferenceDirectory, targetInferenceDirectory);
    }

    private static string? FindInferenceSourceDirectory()
    {
        foreach (var nugetRoot in GetNuGetPackageRootCandidates())
        {
            var packageRoot = Path.Combine(nugetRoot, "paddleocrsharp");
            if (!Directory.Exists(packageRoot))
            {
                continue;
            }

            var versionDirectories = Directory
                .EnumerateDirectories(packageRoot)
                .OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase);

            foreach (var versionDirectory in versionDirectories)
            {
                var candidate = Path.Combine(versionDirectory, "build", "PaddleOCRLib", "inference");
                if (ContainsInferenceFiles(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> GetNuGetPackageRootCandidates()
    {
        var nugetPackages = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrWhiteSpace(nugetPackages))
        {
            yield return nugetPackages;
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            yield return Path.Combine(userProfile, ".nuget", "packages");
        }
    }

    private static bool ContainsInferenceFiles(string inferenceDirectory)
    {
        return Directory.Exists(inferenceDirectory) &&
               File.Exists(Path.Combine(inferenceDirectory, "PaddleOCR.config.json")) &&
               File.Exists(Path.Combine(inferenceDirectory, "ppocr_keys.txt"));
    }

    private static void CopyDirectoryRecursively(string sourceDirectory, string targetDirectory)
    {
        var sourceFullPath = Path.GetFullPath(sourceDirectory);
        var targetFullPath = Path.GetFullPath(targetDirectory);

        if (sourceFullPath.Equals(targetFullPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Directory.CreateDirectory(targetFullPath);

        foreach (var directory in Directory.GetDirectories(sourceFullPath, "*", SearchOption.AllDirectories))
        {
            var targetSubDirectory = directory.Replace(sourceFullPath, targetFullPath, StringComparison.OrdinalIgnoreCase);
            Directory.CreateDirectory(targetSubDirectory);
        }

        foreach (var file in Directory.GetFiles(sourceFullPath, "*", SearchOption.AllDirectories))
        {
            var targetFile = file.Replace(sourceFullPath, targetFullPath, StringComparison.OrdinalIgnoreCase);
            File.Copy(file, targetFile, overwrite: true);
        }
    }
}
