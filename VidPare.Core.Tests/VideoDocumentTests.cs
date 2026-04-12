using Xunit;
using VidPare.Core.Models;

namespace VidPare.Core.Tests;

public class VideoDocumentTests
{
    [Theory]
    [InlineData("video.mp4")]
    [InlineData("video.mov")]
    [InlineData("video.m4v")]
    [InlineData("VIDEO.MP4")]
    [InlineData("VIDEO.MOV")]
    [InlineData("path/to/clip.mp4")]
    public void CanOpen_SupportedExtension_ReturnsTrue(string path)
    {
        Assert.True(VideoDocument.CanOpen(path));
    }

    [Theory]
    [InlineData("video.avi")]
    [InlineData("video.mkv")]
    [InlineData("video.wmv")]
    [InlineData("video.txt")]
    [InlineData("video")]
    [InlineData("")]
    public void CanOpen_UnsupportedExtension_ReturnsFalse(string path)
    {
        Assert.False(VideoDocument.CanOpen(path));
    }

    [Fact]
    public void FormattedFileSize_Bytes()
    {
        var doc = new VideoDocument { FileSize = 512 };
        Assert.Equal("512 B", doc.FormattedFileSize);
    }

    [Fact]
    public void FormattedFileSize_Kilobytes()
    {
        var doc = new VideoDocument { FileSize = 2048 };
        Assert.Equal("2.0 KB", doc.FormattedFileSize);
    }

    [Fact]
    public void FormattedFileSize_Megabytes()
    {
        var doc = new VideoDocument { FileSize = 5 * 1024 * 1024 };
        Assert.Equal("5.0 MB", doc.FormattedFileSize);
    }

    [Fact]
    public void FormattedFileSize_Gigabytes()
    {
        var doc = new VideoDocument { FileSize = (long)(1.5 * 1024 * 1024 * 1024) };
        Assert.Equal("1.50 GB", doc.FormattedFileSize);
    }

    [Fact]
    public void FileName_ReturnsFileNameOnly()
    {
        var doc = new VideoDocument { FilePath = @"C:\Videos\clip.mp4" };
        Assert.Equal("clip.mp4", doc.FileName);
    }
}
