using LinkedArtNet;
using LinkedArtNet.Vocabulary;

namespace Examples.NewDocExamples
{
    public class PhysicalCharacteristics
    {
        public static void Create()
        {
            Nightwatch_6_wh();
            Nightwatch_7_Dimension_Statement();
            Nightwatch_8_Display_Titles();
            Nightwatch_9_Measurement_Assigned();
            Spring_29_Measurement_of_Features();
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
                new LinkedArtObject(Types.LinguisticObject)
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
                new LinkedArtObject(Types.Group)
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

    }
}
