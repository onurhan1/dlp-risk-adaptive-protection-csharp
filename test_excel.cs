using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;

class Program
{
    static void Main()
    {
        var package = ZipFile.OpenRead("excel/VGY_2026_Kalıcı_İstisna_Listesi.xlsx");
        var strings = ReadSharedStrings(package);
        Console.WriteLine($"Found {strings.Count} shared strings.");
        // print first 5 strings
        for(int i=0; i<Math.Min(5, strings.Count); i++) Console.WriteLine($"{i}: {strings[i]}");
        Console.WriteLine($"29: {(strings.Count > 29 ? strings[29] : "N/A")}");
        
        var sheetData = ReadSheetData(package, "sheet1.xml");
        Console.WriteLine($"Found {sheetData.Count} rows.");
        if (sheetData.Count > 1) {
            Console.WriteLine("Row 2:");
            foreach(var c in sheetData[1]) {
                Console.WriteLine($" Col: {c.col}, Val: {c.value}, Type: {c.type}");
                if (c.type == "s" && int.TryParse(c.value, out int ssIndex) && ssIndex < strings.Count) {
                    Console.WriteLine($"   Resolved: {strings[ssIndex]}");
                }
            }
        }
    }
    
    private static List<string> ReadSharedStrings(ZipArchive package)
    {
        var strings = new List<string>();
        var entry = package.GetEntry("xl/sharedStrings.xml");
        if (entry == null) return strings;
        using var reader = new StreamReader(entry.Open());
        var xml = reader.ReadToEnd();
        var siMatches = Regex.Matches(xml, @"<si.*?>(.*?)</si>", RegexOptions.Singleline);
        foreach (Match match in siMatches)
        {
            var siContent = match.Groups[1].Value;
            var tMatches = Regex.Matches(siContent, @"<t[^>]*>(.*?)</t>");
            var str = "";
            foreach (Match t in tMatches) str += t.Groups[1].Value;
            strings.Add(System.Net.WebUtility.HtmlDecode(str));
        }
        return strings;
    }

    private static List<List<(int col, string value, string type)>> ReadSheetData(ZipArchive package, string sheetName)
    {
        var rows = new List<List<(int col, string value, string type)>>();
        var entry = package.GetEntry("xl/worksheets/sheet1.xml");
        if (entry == null) return rows;
        using var reader = new StreamReader(entry.Open());
        var xml = reader.ReadToEnd();
        var rowTag = "<row";
        int pos = 0;
        while ((pos = xml.IndexOf(rowTag, pos, StringComparison.Ordinal)) >= 0)
        {
            var rowEnd = xml.IndexOf("</row>", pos, StringComparison.Ordinal);
            if (rowEnd < 0) break;
            var rowXml = xml.Substring(pos, rowEnd - pos + 6);
            var cells = new List<(int col, string value, string type)>();
            var cTag = "<c ";
            int cPos = 0;
            while ((cPos = rowXml.IndexOf(cTag, cPos, StringComparison.Ordinal)) >= 0)
            {
                var cEnd = rowXml.IndexOf("</c>", cPos, StringComparison.Ordinal);
                var selfClose = rowXml.IndexOf("/>", cPos, StringComparison.Ordinal);
                int cellEnd;
                if (cEnd >= 0 && (selfClose < 0 || cEnd < selfClose)) cellEnd = cEnd + 4;
                else if (selfClose >= 0) cellEnd = selfClose + 2;
                else break;
                var cellXml = rowXml.Substring(cPos, cellEnd - cPos);
                var rMatch = Regex.Match(cellXml, @"r=""([A-Z]+)\d+""");
                var tMatch = Regex.Match(cellXml, @"t=""(\w+)""");
                string cellValue = "";
                bool hasValue = false;
                var vMatch = Regex.Match(cellXml, @"<v>(.*?)</v>");
                if (vMatch.Success) { cellValue = vMatch.Groups[1].Value; hasValue = true; }
                else { var isMatch = Regex.Match(cellXml, @"<t[^>]*>(.*?)</t>"); if (isMatch.Success) { cellValue = isMatch.Groups[1].Value; hasValue = true; } }
                if (rMatch.Success && hasValue)
                {
                    var colLetter = rMatch.Groups[1].Value;
                    int colIndex = 0;
                    for (int i = 0; i < colLetter.Length; i++) { colIndex *= 26; colIndex += (colLetter[i] - 'A' + 1); }
                    colIndex -= 1;
                    cells.Add((colIndex, System.Net.WebUtility.HtmlDecode(cellValue), tMatch.Success ? tMatch.Groups[1].Value : null));
                }
                cPos = cellEnd;
            }
            rows.Add(cells);
            pos = rowEnd + 6;
        }
        return rows;
    }
}
