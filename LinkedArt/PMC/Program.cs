
using NanoXLSX;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

var root = "C:\\Users\\TomCrane\\Dropbox\\digirati\\PMC\\linked.art\\2024-03-18";
var archive = root + "\\2024-03-11_archive";
var library = root + "\\2024-03-11_library";
var photo_archive = root + "\\2024-03-14_photo-archive";

var xArchive = XDocument.Load(archive + "\\2024-03-11_archive-descriptions.xml");
var xAuthorities = XDocument.Load(archive + "\\2024-03-11_archive-authorities.xml");

// var xLido = XDocument.Load(photo_archive + "\\2024-03-14_PMCPA_LIDO.xml");
// xLido.Save(photo_archive + "\\2024-03-14_PMCPA_LIDO_PRETTY.xml");

//var xPhotoEAD = XDocument.Load(photo_archive + "\\2024-03-12_PMCPA_EAD.xml");

// xPhotoEAD.Save(photo_archive + "\\2024-03-12_PMCPA_EAD_PRETTY.xml");

//var eadWb = Workbook.Load(photo_archive + "\\2024-03-14_PMCPA_constituents.xlsx");

// var sheet = GetSheet(eadWb.CurrentWorksheet);

Console.WriteLine();

List<Dictionary<string, string>>? GetSheet(Worksheet worksheet)
{
    string? currentCol = null;
    int currentRow = -1;
    var rows = new List<Dictionary<string, string>>();
    Dictionary<string, string>? rowDict = null;

    foreach (var cell in worksheet.Cells)
    {
        string col = "";
        string rowS = "";
        foreach (char c in cell.Key)
        {
            if (char.IsDigit(c))
            {
                rowS += c;
            }
            else
            {
                col += c;
            }
        }
        int row = Convert.ToInt32(rowS);
        if(row != currentRow)
        {
            rowDict = new Dictionary<string, string>();
            rows.Add(rowDict);
        }
        //rowDict[col] = cell.Value.;

        Console.WriteLine($"{col}{row}: {cell.Value.Value}");
    }
    return null;

}