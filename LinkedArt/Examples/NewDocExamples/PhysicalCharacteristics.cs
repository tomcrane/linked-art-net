using LinkedArtNet;
using LinkedArtNet.Vocabulary;

namespace Examples.NewDocExamples
{
    public class PhysicalCharacteristics
    {
        // https://linked.art/model/object/physical/

        public static void Create()
        {
            Nightwatch_6_wh();
            Nightwatch_7_Dimension_Statement();
            Nightwatch_8_Display_Titles();
            Nightwatch_9_Measurement_Assigned();
            Spring_29_Measurement_of_Features();
            Nightwatch_10_Color();
            Nightwatch_11_Shape();
            Nightwatch_12_Materials();
            Nightwatch_13_Materials_Statement();
            Nightwatch_Support();
            Spring_Back();
            Miniature_Chess_Parts();
        }


        private static void Nightwatch_6_wh()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/nightwatch/6")
                .WithLabel("Night Watch by Rembrandt")
                .WithClassifiedAs(Getty.Painting, Getty.TypeOfWork)
                .WithWidthDimension(453.5, MeasurementUnit.Centimetres)
                .WithHeightDimension(379.5, MeasurementUnit.Centimetres);

            Documentation.Save(nightWatch);
        }


        private static void Nightwatch_7_Dimension_Statement()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/nightwatch/7")
                .WithLabel("Night Watch by Rembrandt")
                .WithClassifiedAs(Getty.Painting, Getty.TypeOfWork);

            nightWatch.ReferredToBy = [
                new LinguisticObject()
                    .WithClassifiedAs(Getty.DimensionStatement, Getty.BriefText)
                    .WithContent("height 379.5 cm × width 453.5 cm × weight 337 kg")
            ];

            Documentation.Save(nightWatch);
        }


        private static void Nightwatch_8_Display_Titles()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/nightwatch/8")
                .WithLabel("Night Watch by Rembrandt")
                .WithClassifiedAs(Getty.Painting, Getty.TypeOfWork)
                .WithWidthDimension(453.5, MeasurementUnit.Centimetres, "453.5 cm wide")
                .WithHeightDimension(379.5, MeasurementUnit.Centimetres, "379.5 cm high");

            Documentation.Save(nightWatch);
        }


        private static void Nightwatch_9_Measurement_Assigned()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/nightwatch/9")
                .WithLabel("Night Watch by Rembrandt")
                .WithClassifiedAs(Getty.Painting, Getty.TypeOfWork)
                .WithHeightDimension(379.5, MeasurementUnit.Centimetres);

            var attrAssignment = new Activity(Types.AttributeAssignment)
                .WithLabel("Measurement of the Night Watch");
            attrAssignment.CarriedOutBy = [
                new Group()
                    .WithId($"{Documentation.IdRoot}/group/nightwatchteam")
                    .WithLabel("Operation Night Watch Team")
            ];
            attrAssignment.PartOf = [
                new Activity()
                    .WithId($"{Documentation.IdRoot}/event/operationnightwatch")
                    .WithLabel("Operation Night Watch")
            ];

            nightWatch.Dimension![0].AssignedBy = [attrAssignment];

            Documentation.Save(nightWatch);
        }


        private static void Spring_29_Measurement_of_Features()
        {
            var spring = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/spring/29")
                .WithLabel("Spring")
                .WithClassifiedAs(Getty.Painting, Getty.TypeOfWork)
                .WithHeightDimension(74, MeasurementUnit.Centimetres)
                .WithWidthDimension(51.5, MeasurementUnit.Centimetres);

            var attrAssignment = new Activity(Types.AttributeAssignment)
                .WithLabel("Unframed Measuring")
                .WithTechnique($"{Documentation.IdRoot}/concept/measuring_unframed", "Unframed Measuring");

            spring.Dimension![0].AssignedBy = [attrAssignment];
            spring.Dimension![1].AssignedBy = [attrAssignment];

            Documentation.Save(spring);
        }


        private static void Nightwatch_10_Color()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/nightwatch/10")
                .WithLabel("Night Watch by Rembrandt")
                .WithClassifiedAs(Getty.Painting, Getty.TypeOfWork)
                .WithRgbColor("#B35A1F", Getty.AatType("Brown", "300127490"), "brown");

            Documentation.Save(nightWatch);
        }


        private static void Nightwatch_11_Shape()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/nightwatch/11")
                .WithLabel("Night Watch by Rembrandt")
                .WithClassifiedAs(Getty.Painting, Getty.TypeOfWork)
                .WithShape(Getty.AatType("Oblong", "300311843"));

            Documentation.Save(nightWatch);
        }


        private static void Nightwatch_12_Materials()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/nightwatch/12")
                .WithLabel("Night Watch by Rembrandt")
                .WithClassifiedAs(Getty.Painting, Getty.TypeOfWork)
                .WithMadeOf("oil", "300015050")
                .WithMadeOf("canvas", "300014078");

            Documentation.Save(nightWatch);
        }



        private static void Nightwatch_13_Materials_Statement()
        {
            var nightWatch = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/nightwatch/13")
                .WithLabel("Night Watch by Rembrandt")
                .WithClassifiedAs(Getty.Painting, Getty.TypeOfWork);

            nightWatch.ReferredToBy = [
                new LinguisticObject()
                    .WithClassifiedAs(Getty.MaterialStatement, Getty.BriefText)
                    .WithContent("Oil on Canvas")
            ];

            Documentation.Save(nightWatch);
        }


        private static void Nightwatch_Support()
        {
            var support = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/nightwatch/support")
                .WithLabel("Support of Night Watch")
                .WithClassifiedAs(Getty.Support, Getty.PartType)
                .WithMadeOf("canvas", "300014078");

            support.PartOf = [
                new HumanMadeObject()
                    .WithId($"{Documentation.IdRoot}/object/nightwatch")
                    .WithLabel("Night Watch by Rembrandt")
            ];

            Documentation.Save(support);
        }


        private static void Spring_Back()
        {
            var springBack = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/spring/back")
                .WithLabel("Back of Spring by Manet")
                .WithClassifiedAs(Getty.BackPart, Getty.PartType);

            springBack.ReferredToBy = [
                new LinguisticObject()
                    .WithClassifiedAs(Getty.Inscription, Getty.BriefText)
                    .WithContent("11505F")
            ];

            springBack.PartOf = [
                new HumanMadeObject()
                    .WithId($"{Documentation.IdRoot}/object/spring")
                    .WithLabel("Jeanne (Spring) by Manet")
            ];

            Documentation.Save(springBack);
        }


        private static void Miniature_Chess_Parts()
        {
            var chess = new HumanMadeObject()
                .WithContext()
                .WithId($"{Documentation.IdRoot}/object/chess/1")
                .WithLabel("Miniature Chess")
                .WithCount(36);

            chess.IdentifiedBy = [
                new Name("Vessel with miniature chess set").AsPrimaryName()
            ];            

            Documentation.Save(chess);
        }


    }
}
