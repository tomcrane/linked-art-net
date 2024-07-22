using System.Xml.Linq;

namespace LocLoader
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var madsNames = @"C:\Users\TomCrane\Dropbox\digirati\PMC\linked.art\Authority PG Dumps\names.madsrdf.xml\";

            int counter = 0;
            foreach(var line in File.ReadLines(madsNames + "names.madsrdf.xml"))
            {
                XDocument doc = XDocument.Parse(line);
                if(counter++ % 1000 == 0)
                {
                    doc.Save(madsNames + "out/" + counter + ".xml");
                }
            }
        }
    }
}
