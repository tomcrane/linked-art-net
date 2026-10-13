namespace LinkedArtNet.Tests;

public class SmokeTests
{
    [Fact]
    public void Library_Loads()
    {
        Assert.Equal("TimeSpan", new LinkedArtTimeSpan().Type);
    }
}
