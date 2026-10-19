using System.IO;
using CorePlayer.Engine;
using Xunit;

namespace CorePlayer.Tests;

public class SubtitleSubsystemTests
{
    [Fact]
    public void FindSubtitlesForMedia_DiscoversMatchingSrtAndAssFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            var movie = Path.Combine(tempDir, "Inception.2010.1080p.mkv");
            File.WriteAllText(movie, "");
            var sub1 = Path.Combine(tempDir, "Inception.2010.1080p.en.srt");
            File.WriteAllText(sub1, "");
            var sub2 = Path.Combine(tempDir, "Inception.2010.1080p.Spanish.ass");
            File.WriteAllText(sub2, "");
            var unrelated = Path.Combine(tempDir, "Interstellar.srt");
            File.WriteAllText(unrelated, "");

            var subSystem = new SubtitleSubsystem();
            var matches = subSystem.FindSubtitlesForMedia(movie);

            Assert.Equal(2, matches.Count);
            Assert.Contains(sub1, matches);
            Assert.Contains(sub2, matches);
            Assert.DoesNotContain(unrelated, matches);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
