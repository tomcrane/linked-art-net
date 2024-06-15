using LinkedArtNet;
using LinkedArtNet.Vocabulary;

namespace PmcTransformer.Helpers
{
    public class Media
    {
        public static LinkedArtObject? FromRecordValue(string value)
        {
            if(value == "Image files")
            {
                return null;
            }
            return MediaDict[value];
        }

        static Media()
        {
            // Labels are exactly those in the PMC data, uses this to look up
            InformationFiles = Getty.AatType("Information files", "300028881");
            Text = Getty.AatType("Text", "300263751");
            ExhibitionCatalogue = Getty.AatType("Exhibition catalogue", "300026096");
            AuctionCatalogue = Getty.AatType("Auction catalogue", "300026068");
            Journal = Getty.AatType("Journal", "300215390");
            CDRom = Getty.AatType("CD-ROM", "300196583");
            Website = Getty.AatType("Web site", "300264578");
            Manuscript = Getty.AatType("Manuscript", "300265483");
            DVD = Getty.AatType("DVD", "300264677");
            Microfilm = Getty.AatType("Microfilm", "300028598");
            CD = Getty.AatType("CD", "300028673");

            MediaDict[InformationFiles.Label!] = InformationFiles;
            MediaDict[Text.Label!] = Text;
            MediaDict[ExhibitionCatalogue.Label!] = ExhibitionCatalogue;
            MediaDict[AuctionCatalogue.Label!] = AuctionCatalogue;
            MediaDict[Journal.Label!] = Journal;
            MediaDict[CDRom.Label!] = CDRom;
            MediaDict[Website.Label!] = Website;
            MediaDict[Manuscript.Label!] = Manuscript;
            MediaDict[DVD.Label!] = DVD;
            MediaDict[Microfilm.Label!] = Microfilm;
            MediaDict[CD.Label!] = CD;
        }

        private static readonly Dictionary<string, LinkedArtObject> MediaDict = [];

        public static LinkedArtObject InformationFiles;
        public static LinkedArtObject Text;
        public static LinkedArtObject ExhibitionCatalogue;
        public static LinkedArtObject AuctionCatalogue;
        public static LinkedArtObject Journal;
        public static LinkedArtObject CDRom;
        public static LinkedArtObject Website;
        public static LinkedArtObject Manuscript;
        public static LinkedArtObject DVD;
        public static LinkedArtObject Microfilm;
        public static LinkedArtObject CD;

    }
}
