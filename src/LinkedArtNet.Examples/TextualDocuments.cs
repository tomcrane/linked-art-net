


using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Examples
{
    // https://linked.art/model/document/
    public class TextualDocuments
    {

        [LinkedArtExample("object/yul_10801219/1")]
        public static HumanMadeObject HumanMadeObject_Carries_Linguistic()
        {
            var kootCopy = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/yul_10801219/1")
                .WithLabel("Yale's copy of Koot's Night Watch")
                .WithClassifiedAs(Aat.Book, Aat.TypeOfWork);

            kootCopy.IdentifiedBy = [ 
                new Name("Rembrandt's Night Watch. A Fascinating Story").AsPrimaryName(),
                new Identifier("mfhd:10801219").AsSystemAssignedNumber()
            ];

            kootCopy.ReferredToBy = [
                new LinguisticObject()
                    .WithContent("92 p. with illus.")
                    .WithClassifiedAs(Aat.PhysicalStatement, Aat.Statement)
            ];

            kootCopy.Carries = [
                new LinguisticObject()
                    .WithId("https://linked.art/example/text/koot_nightwatch")
                    .WithLabel("Content of Koot's Night Watch")
            ];

            return kootCopy;
        }



        [LinkedArtExample("text/koot_nightwatch/1")]
        [SiteIssue("The site publishes two examples as text/koot_nightwatch/1; the JSON at that URL is the 'abstract work' example with part_of, not this textual-content example.")]
        public static LinguisticObject Koot_Textual_Content()
        {
            var koot = new LinguisticObject()
                .WithContext()
                .WithId("https://linked.art/example/text/koot_nightwatch/1")
                .WithLabel("Content of Koot's Night Watch")
                .WithClassifiedAs(Aat.Monograph, Aat.TypeOfWork)
                .WithLanguage("300388277", "English", "en");

            koot.IdentifiedBy = [
                new Name("Rembrandt's Night Watch. A Fascinating Story").AsPrimaryName(),
                new Identifier("75441784").AsSystemAssignedNumber()
            ];

            return koot;
        }


        [LinkedArtExample("text/koot_nightwatch/2")]
        public static LinguisticObject Authorship_And_Publication()
        {
            var koot = new LinguisticObject()
                .WithContext()
                .WithId("https://linked.art/example/text/koot_nightwatch/2")
                .WithLabel("Content of Koot's Night Watch")
                .WithClassifiedAs(Aat.Monograph, Aat.TypeOfWork);

            koot.CreatedBy = new Creation()
            {
                Label = "Koot's writing of the work",
                CarriedOutBy = [
                    new Person()
                        .WithId("https://linked.art/example/person/koot")
                        .WithLabel("Ton Koot")
                ]
            };

            koot.UsedFor = [
                new Activity()
                {
                    Label = "MI's Publishing",
                    ClassifiedAs = [ Aat.PublicationActivity ],
                    TimeSpan = LinkedArtTimeSpan.FromYear(1969),
                    CarriedOutBy = [ 
                        new Group()
                            .WithId("https://linked.art/example/group/meulenhoff")
                            .WithLabel("Meulenhoff International")
                    ]
                }
            ];

            return koot;
        }


        [LinkedArtExample("text/koot_nightwatch_ch1/1")]
        public static LinguisticObject Koot_Chapter()
        {
            var chapter = new LinguisticObject()
                .WithContext()
                .WithId("https://linked.art/example/text/koot_nightwatch_ch1/1")
                .WithLabel("Chapter 1 of Koot")
                .WithClassifiedAs(Aat.Chapter, Aat.TypeOfWork);

            chapter.IdentifiedBy = [new Name("Introduction").AsPrimaryName()];

            chapter.PartOf = [
                new LinguisticObject()
                    .WithId("https://linked.art/example/text/koot_nightwatch")
                    .WithLabel("Koot's Night Watch")
            ];

            return chapter;
        }


        [LinkedArtExample("text/koot_nightwatch_ch1/2")]
        public static LinguisticObject Page_Count()
        {
            var chapter = new LinguisticObject()
                .WithContext()
                .WithId("https://linked.art/example/text/koot_nightwatch_ch1/2")
                .WithLabel("Chapter 1 of Koot")
                .WithClassifiedAs(Aat.Chapter, Aat.TypeOfWork);

            chapter.IdentifiedBy = [new Name("Introduction").AsPrimaryName()];

            chapter.ReferredToBy = [
                new LinguisticObject()
                    .WithContent("5 - 15")
                    .WithClassifiedAs(Aat.PaginationStatement, Aat.Statement)
            ];

            chapter.WithPageCount(10, "10 pages");

            return chapter;
        }


        [LinkedArtExample("text/koot_nightwatch/3")]
        public static LinguisticObject Koot_About()
        {
            var koot = new LinguisticObject()
                .WithContext()
                .WithId("https://linked.art/example/text/koot_nightwatch/3")
                .WithLabel("Content of Koot's Night Watch")
                .WithClassifiedAs(Aat.Monograph, Aat.TypeOfWork);

            koot.About = [
                new HumanMadeObject()
                    .WithId("https://linked.art/example/object/nightwatch")
                    .WithLabel("The Night Watch")
            ];

            return koot;
        }

        [LinkedArtExample("text/koot_nightwatch/1")]
        public static LinguisticObject Abstract_Work()
        {
            var koot = new LinguisticObject()
                .WithContext()
                .WithId("https://linked.art/example/text/koot_nightwatch/1")
                .WithLabel("Content of Koot's Night Watch");

            koot.IdentifiedBy = [
                new Name("Rembrant's Night Watch. A Fascinating Story").AsPrimaryName()
                // Note typo for now
            ];

            koot.PartOf = [
                new PropositionalObject()
                    .WithId("https://linked.art/example/concept/koot_idea")
                    .WithLabel("Koot's Conceptualization")
            ];

            return koot;
        }
    }
}
