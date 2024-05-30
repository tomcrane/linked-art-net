


using LinkedArtNet;
using LinkedArtNet.Vocabulary;

namespace Examples.NewDocExamples
{
    // https://linked.art/model/document/
    public class TextualDocuments
    {
        public static void Create()
        {
            HumanMadeObject_Carries_Linguistic();
            Koot_Textual_Content();
            Authorship_And_Publication();
            Koot_Chapter();
            Page_Count();
            Koot_About();
        }


        private static void HumanMadeObject_Carries_Linguistic()
        {
            var kootCopy = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/yul_10801219/1")
                .WithLabel("Yale's copy of Koot's Night Watch")
                .WithClassifiedAs(Getty.Book, Getty.TypeOfWork);

            kootCopy.IdentifiedBy = [ 
                new Name("Rembrandt's Night Watch. A Fascinating Story").AsPrimaryName(),
                new Identifier("mfhd:10801219").AsSystemAssignedNumber()
            ];

            kootCopy.ReferredToBy = [
                new LinguisticObject()
                    .WithContent("92 p. with illus.")
                    .WithClassifiedAs(Getty.PhysicalStatement, Getty.BriefText)
            ];

            kootCopy.Carries = [
                new LinguisticObject()
                    .WithId($"{Documentation.IdRoot}/text/koot_nightwatch")
                    .WithLabel("Content of Koot's Night Watch")
            ];

            Documentation.Save(kootCopy);
        }



        private static void Koot_Textual_Content()
        {
            var koot = new LinguisticObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/text/koot_nightwatch/1")
                .WithLabel("Content of Koot's Night Watch")
                .WithClassifiedAs(Getty.Monograph)
                .WithLanguage("300388277", "English");

            koot.IdentifiedBy = [
                new Name("Rembrandt's Night Watch. A Fascinating Story").AsPrimaryName(),
                new Identifier("75441784").AsSystemAssignedNumber()
            ];

            Documentation.Save(koot);
        }


        private static void Authorship_And_Publication()
        {
            var koot = new LinguisticObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/text/koot_nightwatch/2")
                .WithLabel("Content of Koot's Night Watch")
                .WithClassifiedAs(Getty.Monograph);

            koot.CreatedBy = new Activity(Types.Creation)
            {
                Label = "Koot's writing of the work",
                CarriedOutBy = [
                    new Person()
                        .WithId($"{Documentation.IdRoot}/person/koot")
                        .WithLabel("Ton Koot")
                ]
            };

            koot.UsedFor = [
                new Activity()
                {
                    Label = "MI's Publishing",
                    ClassifiedAs = [ Getty.Publishing ],
                    TimeSpan = LinkedArtTimeSpan.FromYear(1969),
                    CarriedOutBy = [ 
                        new Group()
                            .WithId($"{Documentation.IdRoot}/group/meulenhoff")
                            .WithLabel("Meulenhoff International")
                    ]
                }
            ];

            // remove the label from year to match example
            koot.UsedFor[0]!.TimeSpan!.Label = null;
            Documentation.Save(koot);
        }


        private static void Koot_Chapter()
        {
            var chapter = new LinguisticObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/text/koot_nightwatch_ch1/1")
                .WithLabel("Chapter 1 of Koot")
                .WithClassifiedAs(Getty.AatType("Chapter", "300311699"));

            chapter.IdentifiedBy = [new Name("Introduction").AsPrimaryName()];

            chapter.PartOf = [
                new LinguisticObject()
                    .WithId($"{Documentation.IdRoot}/text/koot_nightwatch")
                    .WithLabel("Koot's Night Watch")
            ];

            Documentation.Save(chapter);
        }


        private static void Page_Count()
        {
            var chapter = new LinguisticObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/text/koot_nightwatch_ch1/2")
                .WithLabel("Chapter 1 of Koot")
                .WithClassifiedAs(Getty.AatType("Chapter", "300311699"));

            chapter.IdentifiedBy = [new Name("Introduction").AsPrimaryName()];

            chapter.ReferredToBy = [
                new LinguisticObject()
                    .WithContent("5 - 10")
                    .WithClassifiedAs(Getty.PaginationStatement, Getty.BriefText)
            ];

            chapter.WithPageCount(10, "10 pages");

            Documentation.Save(chapter);
        }


        private static void Koot_About()
        {
            var koot = new LinguisticObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/text/koot_nightwatch/3")
                .WithLabel("Content of Koot's Night Watch")
                .WithClassifiedAs(Getty.Monograph);

            koot.About = [
                new HumanMadeObject()
                    .WithId($"{Documentation.IdRoot}/object/nightwatch")
                    .WithLabel("The Night Watch")
            ];

            Documentation.Save(koot);
        }
    }
}
