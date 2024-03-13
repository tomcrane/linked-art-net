using LinkedArtNet;

namespace Examples;

public interface ExamplePage
{
    public const string Base = "https://tomcrane.github.io/linked-art-net/LinkedArt/Examples/output/";

    List<HumanMadeObject> GetHumanMadeObjects();
}