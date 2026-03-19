using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Xunit;

namespace VidPare.App.UITests;

/// <summary>
/// End-to-end UI test: drags a real MP4 file onto the VidPare window and asserts
/// that the editor state appears with the correct file info.
///
/// Requirements:
///   - VidPare.App must be built (Release x64) before running these tests.
///   - A display must be available (not headless).
/// </summary>
public class DragDropLoadTests : IDisposable
{
    private static readonly string AppExe = FindAppExe();

    private static readonly string SampleMp4 = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "TestFixtures", "sample.mp4"));

    private readonly FlaUI.Core.Application _app;
    private readonly UIA3Automation _automation;

    public DragDropLoadTests()
    {
        _app = FlaUI.Core.Application.Launch(AppExe);
        _automation = new UIA3Automation();
    }

    [Fact]
    public void DragDrop_Mp4File_LoadsVideoAndShowsEditor()
    {
        var window = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(15));
        Assert.NotNull(window);

        // Confirm empty state is visible before any interaction
        var emptyState = window.FindFirstDescendant(cf => cf.ByAutomationId("EmptyState"));
        Assert.NotNull(emptyState);

        // Compute a drop point at the centre of the empty-state panel
        var rect = emptyState.BoundingRectangle;
        var dropPoint = new System.Drawing.Point(
            rect.X + rect.Width / 2,
            rect.Y + rect.Height / 2);

        // Perform the OLE file drag-drop
        FileDragDropSimulator.Drop(SampleMp4, dropPoint);

        // Poll for the editor state to become visible (video loading is async)
        AutomationElement? editorState = null;
        var deadline = DateTime.Now.AddSeconds(15);
        while (DateTime.Now < deadline)
        {
            editorState = window.FindFirstDescendant(cf => cf.ByAutomationId("EditorState"));
            if (editorState != null) break;
            Thread.Sleep(200);
        }

        Assert.NotNull(editorState);

        // Verify the file info bar shows the loaded filename
        var fileInfo = window.FindFirstDescendant(cf => cf.ByAutomationId("FileInfoText"));
        Assert.NotNull(fileInfo);
        Assert.Contains("sample.mp4", fileInfo.Name ?? string.Empty);
    }

    public void Dispose()
    {
        _automation.Dispose();
        try { _app.Close(); } catch { /* best-effort */ }
    }

    private static string FindAppExe()
    {
        // Walk up from the test output dir to the solution root, then into VidPare.App's output.
        // Layout: VidPare.App.UITests/bin/<config>/net8.0-.../win-x64/  (5 levels up = solution root)
        var baseDir = AppContext.BaseDirectory;
        foreach (var config in new[] { "Release", "Debug" })
        {
            var candidate = Path.GetFullPath(Path.Combine(
                baseDir, "..", "..", "..", "..", "..",
                "VidPare.App", "bin", "x64", config,
                "net8.0-windows10.0.22000.0", "win-x64", "VidPare.App.exe"));

            if (File.Exists(candidate)) return candidate;
        }

        throw new FileNotFoundException(
            "VidPare.App.exe not found. Build the solution (Release x64) before running UI tests.\n" +
            $"Searched from: {baseDir}");
    }
}
