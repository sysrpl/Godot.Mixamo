// Comment this out to use Godot's built-in FileDialog control instead of
// the OS-native "Open File" picker.
// #define USE_NATIVE_FILE_DIALOG

using Godot;
using System;
using System.Collections.Generic;

// Prompts the user to pick a file from disk, transparently using either the
// OS-native file picker or Godot's own FileDialog control depending on the
// USE_NATIVE_FILE_DIALOG define above. The set of selectable files is
// controlled by FileTypeFilter.
public class OpenFileDialog
{
    public enum FileTypeFilter
    {
        AllFiles,
        FbxFiles,
        TextFiles,
        ImageFiles,
    }

    // Comma-separated extension pattern + description per filter, matching
    // the format expected by both FileDialog.AddFilter and
    // DisplayServer.FileDialogShow. AllFiles is intentionally absent - no
    // entry means no restriction.
    private static readonly Dictionary<FileTypeFilter, (string Pattern, string Description)> Filters = new()
    {
        { FileTypeFilter.FbxFiles, ("*.fbx", "FBX Files") },
        { FileTypeFilter.TextFiles, ("*.txt,*.md,*.json,*.log", "Text Files") },
        { FileTypeFilter.ImageFiles, ("*.png,*.jpg,*.jpeg,*.bmp,*.webp", "Image Files") },
    };

    public event Action<string> FileSelected;

    private readonly string _title;
    private readonly FileTypeFilter _fileType;
#if !USE_NATIVE_FILE_DIALOG
    private readonly FileDialog _dialog;
#endif

    // parent is the Node the fallback FileDialog gets added to (unused in native mode).
    public OpenFileDialog(Node parent, FileTypeFilter fileType = FileTypeFilter.AllFiles, string title = "Open File", Theme theme = null)
    {
        _title = title;
        _fileType = fileType;

#if !USE_NATIVE_FILE_DIALOG
        _dialog = new FileDialog
        {
            Title = title,
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem
        };
        if (Filters.TryGetValue(fileType, out var filter))
            _dialog.AddFilter(filter.Pattern, filter.Description);
        _dialog.FileSelected += path => FileSelected?.Invoke(path);
        if (theme != null)
            _dialog.Theme = theme;
        parent.AddChild(_dialog);
#endif
    }

    public void Show()
    {
#if USE_NATIVE_FILE_DIALOG
        var filters = Filters.TryGetValue(_fileType, out var filter)
            ? new[] { $"{filter.Pattern};{filter.Description}" }
            : Array.Empty<string>();

        DisplayServer.FileDialogShow(
            _title,
            "",
            "",
            false,
            DisplayServer.FileDialogMode.OpenFile,
            filters,
            Callable.From<bool, string[], int>(OnNativeResult));
#else
        _dialog.PopupCentered(new Vector2I(720, 480));
#endif
    }

#if USE_NATIVE_FILE_DIALOG
    private void OnNativeResult(bool status, string[] selectedPaths, int selectedFilterIndex)
    {
        if (!status || selectedPaths.Length == 0)
            return;

        FileSelected?.Invoke(selectedPaths[0]);
    }
#endif
}
