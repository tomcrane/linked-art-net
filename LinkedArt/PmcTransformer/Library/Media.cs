using LinkedArtNet;
using LinkedArtNet.Vocabulary;

namespace PmcTransformer.Library
{
    public class Media
    {
        public static (LinkedArtObject?, LinkedArtObject?) FromRecordValue(string value)
        {
            if (value == "Image files")
            {
                return (null, null);
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
            Journal = Getty.AatType("Journal", "300215390");  // IGNORE THE RECORD
            CDRom = Getty.AatType("CD-ROM", "300196583");
            Website = Getty.AatType("Web site", "300264578");
            Manuscript = Getty.AatType("Manuscript", "300265483");
            DVD = Getty.AatType("DVD", "300264677");
            Microfilm = Getty.AatType("Microfilm", "300028598");
            CD = Getty.AatType("CD", "300028673");

            // Mediums from class field:
            Pamphlet = Getty.AatType("Pamphlet", "300220572");
            Large = Getty.AatType("Large", "300379501");
            Report = Getty.AatType("Report", "300027267");

            //               to be applied to:   (the LinguisticObject, the HumanMadeObjects)
            MediaDict[InformationFiles.Label!] = (Text, InformationFiles);
            MediaDict[Text.Label!] = (Text, Getty.Book) ;
            MediaDict[ExhibitionCatalogue.Label!] = (ExhibitionCatalogue, Getty.Book);
            MediaDict[AuctionCatalogue.Label!] = (AuctionCatalogue, Getty.Book);
            MediaDict[Journal.Label!] = (Journal, null);
            MediaDict[CDRom.Label!] = (null, CDRom);
            MediaDict[Website.Label!] = (Website, Website);
            MediaDict[Manuscript.Label!] = (Text, Manuscript);
            MediaDict[DVD.Label!] = (null, DVD);
            MediaDict[Microfilm.Label!] = (null, Microfilm);
            MediaDict[CD.Label!] = (Getty.Sound, CD);

            MediaDict[Pamphlet.Label!] = (Text, Pamphlet);
            MediaDict[Large.Label!] = (Text, Large);
            MediaDict[Report.Label!] = (Text, Report);

        }

        //                                 for the LinguisticObject, for the HumanMadeObjects
        private static readonly Dictionary<string, (LinkedArtObject?, LinkedArtObject?)> MediaDict = [];

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

        public static LinkedArtObject Pamphlet;
        public static LinkedArtObject Large;
        public static LinkedArtObject Report;


    }
}
