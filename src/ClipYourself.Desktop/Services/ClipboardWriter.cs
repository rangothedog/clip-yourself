using System.Collections.Specialized;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using ClipYourself.Core.Models;

namespace ClipYourself.Desktop.Services;

/// <summary>Puts a stored clip back onto the Windows clipboard.</summary>
public static class ClipboardWriter
{
    public static bool TryWrite(ClipItem clip)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                WriteCore(clip);
                return true;
            }
            catch (COMException) { Thread.Sleep(80); }
            catch (ExternalException) { Thread.Sleep(80); }
            catch
            {
                return false;
            }
        }
        return false;
    }

    private static void WriteCore(ClipItem clip)
    {
        switch (clip.Kind)
        {
            case ClipKind.Text:
                Clipboard.SetDataObject(clip.Text ?? string.Empty, true);
                break;

            case ClipKind.Image:
                if (clip.ImagePath == null || !File.Exists(clip.ImagePath))
                    throw new FileNotFoundException("Image blob missing", clip.ImagePath);
                Clipboard.SetImage(LoadBitmap(clip.ImagePath));
                break;

            case ClipKind.Audio:
                var audioPaths = FirstExisting(clip.AudioPath, clip.FilePaths);
                var data = new DataObject();
                var files = new StringCollection();
                files.AddRange(audioPaths);
                data.SetFileDropList(files);
                if (Path.GetExtension(audioPaths[0]).Equals(".wav", StringComparison.OrdinalIgnoreCase))
                    data.SetAudio(File.ReadAllBytes(audioPaths[0]));
                Clipboard.SetDataObject(data, true);
                break;

            case ClipKind.Files:
                var existing = clip.FilePaths.Where(File.Exists).ToArray();
                if (existing.Length == 0) throw new FileNotFoundException("Original files no longer exist.");
                SetFileDrop(existing);
                break;
        }
    }

    private static string[] FirstExisting(string? primary, List<string> fallbacks)
    {
        if (primary != null && File.Exists(primary)) return new[] { primary };
        var alt = fallbacks.FirstOrDefault(File.Exists);
        if (alt == null) throw new FileNotFoundException("Audio file no longer exists.");
        return new[] { alt };
    }

    private static void SetFileDrop(string[] paths)
    {
        var collection = new StringCollection();
        collection.AddRange(paths);
        Clipboard.SetFileDropList(collection);
    }

    private static BitmapImage LoadBitmap(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
