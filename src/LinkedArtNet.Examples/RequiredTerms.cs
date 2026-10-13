using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Examples
{
    /// <summary>The usage examples on https://linked.art/model/vocab/ and https://linked.art/model/vocab/required/.</summary>
    public class RequiredTerms
    {
        [LinkedArtExample("object/spring/30")]
        public static HumanMadeObject Spring_Classified_As_Painting()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/spring/30")
                .WithLabel("Jeanne (Spring) by Manet")
                .WithClassifiedAs(Aat.Painting);

            return spring;
        }


        [LinkedArtExample("object/nightwatch/50")]
        public static HumanMadeObject NightWatch_Label()
        {
            var nightwatch = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/50")
                .WithLabel("Night Watch by Rembrandt");

            return nightwatch;
        }


        [LinkedArtExample("object/nightwatch/41")]
        public static HumanMadeObject NightWatch_Primary_Name_In_English()
        {
            var nightwatch = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/41")
                .WithLabel("Night Watch by Rembrandt");

            nightwatch.IdentifiedBy = [
                new Name("The Night Watch")
                    .AsPrimaryName()
                    .WithLanguage(Aat.English)
            ];

            return nightwatch;
        }


        [LinkedArtExample("object/nightwatch/42")]
        public static HumanMadeObject NightWatch_Statement_With_Display_Title()
        {
            var nightwatch = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/42")
                .WithLabel("Night Watch by Rembrandt");

            nightwatch.ReferredToBy = [
                new LinguisticObject()
                    .WithClassifiedAs(Aat.MaterialsStatement, Aat.Statement)
                    .WithContent("Oil on Canvas")
                    .WithName("Materials", n => n.WithClassifiedAs(Aat.DisplayName))
            ];

            return nightwatch;
        }


        [LinkedArtExample("object/nightwatch/44")]
        public static HumanMadeObject NightWatch_Brief_Text_Statement()
        {
            var nightwatch = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/44")
                .WithLabel("Night Watch by Rembrandt");

            nightwatch.ReferredToBy = [
                new LinguisticObject()
                    .WithClassifiedAs(Aat.MaterialsStatement, Aat.Statement)
                    .WithContent("Oil on Canvas")
            ];

            return nightwatch;
        }


        [LinkedArtExample("object/nightwatch/43")]
        public static HumanMadeObject NightWatch_TimeSpan_Display_Title()
        {
            var nightwatch = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/43")
                .WithLabel("Night Watch by Rembrandt");

            nightwatch.ProducedBy = new Production
            {
                TimeSpan = new LinkedArtTimeSpan
                {
                    IdentifiedBy = [
                        new Name()
                            .WithId("https://linked.art/example/Name/1642")
                            .WithClassifiedAs(Aat.DisplayName)
                    ],
                    BeginOfTheBegin = new LinkedArtDate(1642, 1, 1),
                    EndOfTheEnd = new LinkedArtDate(1842, 12, 31).EndOfDay()
                }
            };

            return nightwatch;
        }


        [LinkedArtExample("object/nightwatch/45")]
        public static HumanMadeObject NightWatch_Type_Of_Work()
        {
            var nightwatch = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/45")
                .WithLabel("Night Watch by Rembrandt")
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork);

            return nightwatch;
        }


        [LinkedArtExample("object/nightwatch/47")]
        public static HumanMadeObject NightWatch_Color()
        {
            var nightwatch = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/47")
                .WithLabel("Night Watch by Rembrandt");

            nightwatch.Dimension = [
                new Dimension
                {
                    Label = "brown",
                    Value = 11754015,
                    Unit = Aat.Rgb
                }.WithClassifiedAs(Aat.Color)
            ];

            return nightwatch;
        }


        [LinkedArtExample("object/nightwatch/48")]
        public static HumanMadeObject NightWatch_Shape()
        {
            var nightwatch = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/48")
                .WithLabel("Night Watch by Rembrandt")
                .WithClassifiedAs(Aat.Rectangular, Aat.Shape);

            return nightwatch;
        }


        [LinkedArtExample("object/nightwatch/49")]
        public static HumanMadeObject NightWatch_Collection_Item()
        {
            var nightwatch = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/49")
                .WithLabel("Night Watch by Rembrandt")
                .WithClassifiedAs(Aat.CollectionItem);

            return nightwatch;
        }


        [LinkedArtExample("person/rembrandt/41")]
        public static Person Rembrandt_Sorting_Name()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId("https://linked.art/example/person/rembrandt/41")
                .WithLabel("Rembrandt");

            rembrandt.IdentifiedBy = [
                new Name("Rembrandt van Rijn").AsPrimaryName(),
                new Name("van Rijn, Rembrandt Harmenszoon").WithClassifiedAs(Aat.SortName)
            ];

            return rembrandt;
        }


        [LinkedArtExample("person/rembrandt/42")]
        public static Person Rembrandt_Sort_Value()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId("https://linked.art/example/person/rembrandt/42")
                .WithLabel("Rembrandt");

            rembrandt.IdentifiedBy = [
                new Identifier("r0000011").WithClassifiedAs(Aat.SortValue)
            ];

            return rembrandt;
        }


        [LinkedArtExample("person/rembrandt/43")]
        public static Person Rembrandt_Occupation()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId("https://linked.art/example/person/rembrandt/43")
                .WithLabel("Rembrandt")
                .WithClassifiedAs(Aat.Artist, Aat.Occupation);

            return rembrandt;
        }


        [LinkedArtExample("person/rembrandt/44")]
        public static Person Rembrandt_Professional_Activities()
        {
            var rembrandt = new Person()
                .WithContext()
                .WithId("https://linked.art/example/person/rembrandt/44")
                .WithLabel("Rembrandt");

            rembrandt.CarriedOut = [
                new Activity().WithClassifiedAs(Aat.ProfessionalActivity)
            ];

            return rembrandt;
        }


        [LinkedArtExample("activity/exha/40")]
        public static Activity Exhibition_Activity()
        {
            var activity = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/activity/exha/40")
                .WithLabel("Manet and Modern Beauty (Getty)")
                .WithClassifiedAs(Aat.Term("300054766", "Exhibiting"));

            return activity;
        }


        [LinkedArtExample("provenance/manet_proust/40")]
        public static Activity Provenance_Activity()
        {
            var activity = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/provenance/manet_proust/40")
                .WithLabel("Purchase of Spring by Proust")
                .WithClassifiedAs(Aat.ProvenanceActivity);

            return activity;
        }


        [LinkedArtExample("provenance/coffin_promise/40")]
        public static Activity Promise_Activity()
        {
            var activity = new Activity()
                .WithContext()
                .WithId("https://linked.art/example/provenance/coffin_promise/40")
                .WithLabel("Promised Gift of Painting")
                .WithClassifiedAs(Aat.ProvenanceActivity);

            activity.Part = [
                new Activity()
                    .WithLabel("Promise of Gift")
                    .WithClassifiedAs(Aat.PromiseActivity)
            ];

            return activity;
        }


        [LinkedArtExample("text/koot_nightwatch/40")]
        public static LinguisticObject Publishing_Activity()
        {
            var text = new LinguisticObject()
                .WithContext()
                .WithId("https://linked.art/example/text/koot_nightwatch/40")
                .WithLabel("Content of Koot's Night Watch");

            text.UsedFor = [
                new Activity()
                    .WithLabel("MI's Publishing")
                    .WithClassifiedAs(Aat.PublicationActivity)
                    .WithCarriedOutBy(
                        new Group()
                            .WithId("https://linked.art/example/group/meulenhoff")
                            .WithLabel("Meulenhoff International"))
            ];

            return text;
        }


        [LinkedArtExample("visual/nightwatch/40")]
        public static VisualItem NightWatch_Style()
        {
            var appearance = new VisualItem()
                .WithContext()
                .WithId("https://linked.art/example/visual/nightwatch/40")
                .WithLabel("Appearance of Night Watch")
                .WithClassifiedAs(Aat.Term("300021147", "Baroque"), Aat.Style);

            return appearance;
        }
    }
}
