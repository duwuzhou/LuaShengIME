using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Android.Content;
using Android.Util;
using IME.Shared.InputEngine;

namespace IME.Features.UserLexicon
{
    /// <summary>
    /// User lexicon sync service that exports/imports the user dictionary ZIP.
    /// </summary>
    public class SyncService
    {
        private const string Tag = "SyncService";
        private static readonly string[] ExportPatterns = { "*.userdb", "*.txt", "*.yaml", "*.custom.yaml" };

        private readonly Context _context;

        public SyncService(Context context)
        {
            _context = context;
        }

        public bool SyncRimeUserData()
        {
            try
            {
                Log.Info(Tag, "Start Rime user data sync...");
                bool result = RimeNativeBindings.SyncUserData();
                Log.Info(Tag, result ? "Rime sync succeeded" : "Rime sync failed");
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(Tag, $"Rime sync failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ExportUserDictAsync(string outputPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    string userDataDir = RimeUserDataPaths.GetUserDataDir(_context);
                    if (string.IsNullOrEmpty(userDataDir) || !Directory.Exists(userDataDir))
                    {
                        Log.Error(Tag, "User data directory does not exist.");
                        return false;
                    }

                    Log.Info(Tag, $"Exporting user dictionary to: {outputPath}");

                    string outputDir = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(outputDir))
                    {
                        Directory.CreateDirectory(outputDir);
                    }

                    if (File.Exists(outputPath))
                    {
                        File.Delete(outputPath);
                    }

                    using (var zipStream = new FileStream(outputPath, FileMode.Create))
                    using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                    {
                        foreach (string pattern in ExportPatterns)
                        {
                            string[] files = Directory.GetFiles(userDataDir, pattern, SearchOption.AllDirectories);
                            foreach (string file in files)
                            {
                                string relativePath = Path.GetRelativePath(userDataDir, file);
                                var entry = archive.CreateEntry(relativePath, CompressionLevel.Optimal);
                                using (var entryStream = entry.Open())
                                using (var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read))
                                {
                                    fileStream.CopyTo(entryStream);
                                }
                                Log.Info(Tag, $"Exported {relativePath}");
                            }
                        }

                        string syncDir = RimeUserDataPaths.GetSyncDir(_context);
                        if (!string.IsNullOrEmpty(syncDir) && Directory.Exists(syncDir))
                        {
                            string[] syncFiles = Directory.GetFiles(syncDir, "*.*", SearchOption.AllDirectories);
                            foreach (string file in syncFiles)
                            {
                                string relativePath = "sync/" + Path.GetRelativePath(syncDir, file);
                                var entry = archive.CreateEntry(relativePath, CompressionLevel.Optimal);
                                using (var entryStream = entry.Open())
                                using (var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read))
                                {
                                    fileStream.CopyTo(entryStream);
                                }
                            }
                        }
                    }

                    Log.Info(Tag, "User dictionary export succeeded");
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Error(Tag, $"Export failed: {ex.Message}");
                    Log.Error(Tag, $"StackTrace: {ex.StackTrace}");
                    return false;
                }
            });
        }

        public async Task<bool> ImportUserDictAsync(string inputPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(inputPath))
                    {
                        Log.Error(Tag, $"Import file not found: {inputPath}");
                        return false;
                    }

                    string userDataDir = RimeUserDataPaths.GetUserDataDir(_context);
                    if (string.IsNullOrEmpty(userDataDir))
                    {
                        Log.Error(Tag, "User data directory is not available.");
                        return false;
                    }

                    Directory.CreateDirectory(userDataDir);

                    Log.Info(Tag, $"Importing user dictionary from {inputPath}...");

                    using (var zipStream = new FileStream(inputPath, FileMode.Open, FileAccess.Read))
                    using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            if (string.IsNullOrEmpty(entry.Name))
                                continue;

                            string destPath;
                            string expectedRoot;
                            if (entry.FullName.StartsWith("sync/"))
                            {
                                string syncDir = RimeUserDataPaths.GetSyncDir(_context);
                                if (string.IsNullOrEmpty(syncDir))
                                    continue;
                                Directory.CreateDirectory(syncDir);
                                destPath = Path.Combine(syncDir, entry.FullName.Substring(5));
                                expectedRoot = Path.GetFullPath(syncDir);
                            }
                            else
                            {
                                destPath = Path.Combine(userDataDir, entry.FullName);
                                expectedRoot = Path.GetFullPath(userDataDir);
                            }

                            string fullDestPath = Path.GetFullPath(destPath);
                            if (!fullDestPath.StartsWith(expectedRoot, StringComparison.Ordinal))
                            {
                                Log.Warn(Tag, $"Skipping entry with path traversal: {entry.FullName}");
                                continue;
                            }

                            string destDir = Path.GetDirectoryName(fullDestPath);
                            if (!string.IsNullOrEmpty(destDir))
                            {
                                Directory.CreateDirectory(destDir);
                            }

                            using (var entryStream = entry.Open())
                            using (var fileStream = new FileStream(fullDestPath, FileMode.Create))
                            {
                                entryStream.CopyTo(fileStream);
                            }
                            Log.Info(Tag, $"Imported {entry.FullName}");
                        }
                    }

                    Log.Info(Tag, "User dictionary import succeeded");
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Error(Tag, $"Import failed: {ex.Message}");
                    Log.Error(Tag, $"StackTrace: {ex.StackTrace}");
                    return false;
                }
            });
        }

        public string GetDefaultExportPath()
        {
            string downloadDir = Android.OS.Environment.GetExternalStoragePublicDirectory(
                Android.OS.Environment.DirectoryDownloads)?.AbsolutePath;

            if (string.IsNullOrEmpty(downloadDir))
            {
                downloadDir = Path.Combine(_context.FilesDir.AbsolutePath, "export");
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            return Path.Combine(downloadDir, $"ime_userdict_{timestamp}.zip");
        }
    }
}
