using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Examples
{
    public class PhysicalCharacteristics
    {
        // https://linked.art/model/object/physical/


        [LinkedArtExample("object/nightwatch/6")]
        public static HumanMadeObject Nightwatch_6_wh()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/6")
                .WithLabel("Night Watch by Rembrandt")
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork)
                .WithWidthDimension(453.5, MeasurementUnit.Centimeters)
                .WithHeightDimension(379.5, MeasurementUnit.Centimeters);

            return nightWatch;
        }


        [LinkedArtExample("object/nightwatch/7")]
        public static HumanMadeObject Nightwatch_7_Dimension_Statement()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/7")
                .WithLabel("Night Watch by Rembrandt")
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork);

            nightWatch.ReferredToBy = [
                new LinguisticObject()
                    .WithClassifiedAs(Aat.DimensionsStatement, Aat.Statement)
                    .WithContent("height 379.5 cm × width 453.5 cm × weight 337 kg")
            ];

            return nightWatch;
        }


        [LinkedArtExample("object/nightwatch/8")]
        public static HumanMadeObject Nightwatch_8_Display_Titles()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/8")
                .WithLabel("Night Watch by Rembrandt")
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork)
                .WithWidthDimension(453.5, MeasurementUnit.Centimeters, "453.5 cm wide")
                .WithHeightDimension(379.5, MeasurementUnit.Centimeters, "379.5 cm high");

            return nightWatch;
        }


        [LinkedArtExample("object/nightwatch/9")]
        public static HumanMadeObject Nightwatch_9_Measurement_Assigned()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/9")
                .WithLabel("Night Watch by Rembrandt")
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork)
                .WithHeightDimension(379.5, MeasurementUnit.Centimeters);

            var attrAssignment = new AttributeAssignment()
                .WithLabel("Measurement of the Night Watch");
            attrAssignment.CarriedOutBy = [
                new Group()
                    .WithId("https://linked.art/example/group/nightwatchteam")
                    .WithLabel("Operation Night Watch Team")
            ];
            attrAssignment.PartOf = [
                new Activity()
                    .WithId("https://linked.art/example/event/operationnightwatch")
                    .WithLabel("Operation Night Watch")
            ];

            nightWatch.Dimension![0].AssignedBy = [attrAssignment];

            return nightWatch;
        }


        [LinkedArtExample("object/spring/29")]
        public static HumanMadeObject Spring_29_Measurement_of_Features()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/spring/29")
                .WithLabel("Spring")
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork)
                .WithHeightDimension(74, MeasurementUnit.Centimeters)
                .WithWidthDimension(51.5, MeasurementUnit.Centimeters);

            var attrAssignment = new AttributeAssignment()
                .WithLabel("Unframed Measuring")
                .WithTechnique("https://linked.art/example/concept/measuring_unframed", "Unframed Measuring");

            spring.Dimension![0].AssignedBy = [attrAssignment];
            spring.Dimension![1].AssignedBy = [attrAssignment];

            return spring;
        }


        [LinkedArtExample("object/nightwatch/10")]
        public static HumanMadeObject Nightwatch_10_Color()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/10")
                .WithLabel("Night Watch by Rembrandt")
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork)
                .WithRgbColor("#B35A1F", Aat.Term("300127490", "Brown"), "brown");

            return nightWatch;
        }


        [LinkedArtExample("object/nightwatch/11")]
        public static HumanMadeObject Nightwatch_11_Shape()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/11")
                .WithLabel("Night Watch by Rembrandt")
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork)
                .WithShape(Aat.Oblong);

            return nightWatch;
        }


        [LinkedArtExample("object/nightwatch/12")]
        public static HumanMadeObject Nightwatch_12_Materials()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/12")
                .WithLabel("Night Watch by Rembrandt")
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork)
                .WithMadeOf("oil", "300015050")
                .WithMadeOf("canvas", "300014078");

            return nightWatch;
        }



        [LinkedArtExample("object/nightwatch/13")]
        public static HumanMadeObject Nightwatch_13_Materials_Statement()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/13")
                .WithLabel("Night Watch by Rembrandt")
                .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork);

            nightWatch.ReferredToBy = [
                new LinguisticObject()
                    .WithClassifiedAs(Aat.MaterialsStatement, Aat.Statement)
                    .WithContent("Oil on Canvas")
            ];

            return nightWatch;
        }


        [LinkedArtExample("object/nightwatch/support")]
        public static HumanMadeObject Nightwatch_Support()
        {
            var support = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/nightwatch/support")
                .WithLabel("Support of Night Watch")
                .WithClassifiedAs(Aat.Support, Aat.PartType)
                .WithMadeOf("canvas", "300014078");

            support.PartOf = [
                new HumanMadeObject()
                    .WithId("https://linked.art/example/object/nightwatch")
                    .WithLabel("Night Watch by Rembrandt")
            ];

            return support;
        }


        [LinkedArtExample("object/spring/back")]
        public static HumanMadeObject Spring_Back()
        {
            var springBack = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/spring/back")
                .WithLabel("Back of Spring by Manet")
                .WithClassifiedAs(Aat.Back, Aat.PartType);

            springBack.ReferredToBy = [
                new LinguisticObject()
                    .WithClassifiedAs(Aat.InscriptionStatement, Aat.Statement)
                    .WithContent("11505F")
            ];

            springBack.PartOf = [
                new HumanMadeObject()
                    .WithId("https://linked.art/example/object/spring")
                    .WithLabel("Jeanne (Spring) by Manet")
            ];

            return springBack;
        }


        [LinkedArtExample("object/chess/1")]
        public static HumanMadeObject Miniature_Chess_Parts()
        {
            var chess = new HumanMadeObject()
                .WithContext()
                .WithId("https://linked.art/example/object/chess/1")
                .WithLabel("Miniature Chess")
                .WithComponentCount(36);

            chess.IdentifiedBy = [
                new Name("Vessel with miniature chess set").AsPrimaryName()
            ];            

            return chess;
        }


    }
}
