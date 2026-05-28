using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SalesPortal.Web.Services.Orders
{
    public sealed class BulkOrderWorkbook
    {
        public IReadOnlyList<BulkOrderHeaderRecord> Headers { get; init; } = Array.Empty<BulkOrderHeaderRecord>();
        public IReadOnlyList<BulkOrderLineRecord> Lines { get; init; } = Array.Empty<BulkOrderLineRecord>();
    }

    public sealed class BulkOrderHeaderRecord
    {
        public int RowNumber { get; init; }
        public string IdOrden { get; init; } = string.Empty;
        public string Direccion { get; init; } = string.Empty;
        public string Comentario { get; init; } = string.Empty;
    }

    public sealed class BulkOrderLineRecord
    {
        public int RowNumber { get; init; }
        public string IdOrden { get; init; } = string.Empty;
        public int Linea { get; init; }
        public string Articulo { get; init; } = string.Empty;
        public decimal Cantidad { get; init; }
    }

    public static partial class BulkOrderWorkbookService
    {
        private static readonly XNamespace SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        public static byte[] CreateTemplate()
        {
            using var stream = new MemoryStream();

            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                AddEntry(archive, "[Content_Types].xml", """
                    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                    <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                      <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                      <Default Extension="xml" ContentType="application/xml"/>
                      <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                      <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                      <Override PartName="/xl/worksheets/sheet2.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                      <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
                    </Types>
                    """);

                AddEntry(archive, "_rels/.rels", """
                    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                    <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                      <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                    </Relationships>
                    """);

                AddEntry(archive, "xl/_rels/workbook.xml.rels", """
                    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                    <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                      <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                      <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet2.xml"/>
                      <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
                    </Relationships>
                    """);

                AddEntry(archive, "xl/workbook.xml", """
                    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                    <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                      <sheets>
                        <sheet name="Cabecera" sheetId="1" r:id="rId1"/>
                        <sheet name="Detalle" sheetId="2" r:id="rId2"/>
                      </sheets>
                    </workbook>
                    """);

                AddEntry(archive, "xl/styles.xml", """
                    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                    <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                      <fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts>
                      <fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills>
                      <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
                      <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
                      <cellXfs count="2"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/></cellXfs>
                    </styleSheet>
                    """);

                AddEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheetXml(new[] { "IdOrden", "Direccion", "Comentario" }, new[]
                {
                    new[] { "ORD-001", "DIRECCION_ENVIO", "Comentario opcional" }
                }));

                AddEntry(archive, "xl/worksheets/sheet2.xml", BuildWorksheetXml(new[] { "IdOrden", "Linea", "Articulo(sku)", "Cantidad" }, new[]
                {
                    new[] { "ORD-001", "1", "SKU-001", "2" },
                    new[] { "ORD-001", "2", "SKU-002", "1" }
                }));
            }

            return stream.ToArray();
        }

        public static BulkOrderWorkbook Read(Stream stream)
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var sharedStrings = ReadSharedStrings(archive);
            var headers = ReadSheet(archive, "xl/worksheets/sheet1.xml", sharedStrings);
            var lines = ReadSheet(archive, "xl/worksheets/sheet2.xml", sharedStrings);

            ValidateHeaderRow(headers, new[] { "IdOrden", "Direccion", "Comentario" }, "Cabecera");
            ValidateHeaderRow(lines, new[] { "IdOrden", "Linea", "Articulo(sku)", "Cantidad" }, "Detalle");

            return new BulkOrderWorkbook
            {
                Headers = headers.Skip(1)
                    .Where(row => row.Values.Any(value => !string.IsNullOrWhiteSpace(value)))
                    .Select(row => new BulkOrderHeaderRecord
                    {
                        RowNumber = row.RowNumber,
                        IdOrden = row.Values.ElementAtOrDefault(0)?.Trim() ?? string.Empty,
                        Direccion = row.Values.ElementAtOrDefault(1)?.Trim() ?? string.Empty,
                        Comentario = row.Values.ElementAtOrDefault(2)?.Trim() ?? string.Empty
                    })
                    .ToList(),
                Lines = lines.Skip(1)
                    .Where(row => row.Values.Any(value => !string.IsNullOrWhiteSpace(value)))
                    .Select(row => new BulkOrderLineRecord
                    {
                        RowNumber = row.RowNumber,
                        IdOrden = row.Values.ElementAtOrDefault(0)?.Trim() ?? string.Empty,
                        Linea = ParseInt(row.Values.ElementAtOrDefault(1)),
                        Articulo = row.Values.ElementAtOrDefault(2)?.Trim() ?? string.Empty,
                        Cantidad = ParseDecimal(row.Values.ElementAtOrDefault(3))
                    })
                    .ToList()
            };
        }

        private static string BuildWorksheetXml(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
        {
            var builder = new StringBuilder();
            builder.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            builder.AppendLine("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            builder.AppendLine("<sheetData>");
            AppendRow(builder, 1, headers, isHeader: true);

            for (var index = 0; index < rows.Count; index++)
            {
                AppendRow(builder, index + 2, rows[index], isHeader: false);
            }

            builder.AppendLine("</sheetData>");
            builder.AppendLine("</worksheet>");
            return builder.ToString();
        }

        private static void AppendRow(StringBuilder builder, int rowIndex, IReadOnlyList<string> values, bool isHeader)
        {
            builder.Append(CultureInfo.InvariantCulture, $"<row r=\"{rowIndex}\">");

            for (var columnIndex = 0; columnIndex < values.Count; columnIndex++)
            {
                var reference = $"{ColumnName(columnIndex + 1)}{rowIndex}";
                var style = isHeader ? " s=\"1\"" : string.Empty;
                builder.Append(CultureInfo.InvariantCulture, $"<c r=\"{reference}\" t=\"inlineStr\"{style}><is><t>{SecurityElement.Escape(values[columnIndex])}</t></is></c>");
            }

            builder.AppendLine("</row>");
        }

        private static IReadOnlyList<WorkbookRow> ReadSheet(ZipArchive archive, string path, IReadOnlyList<string> sharedStrings)
        {
            var entry = archive.GetEntry(path) ?? throw new InvalidDataException("El archivo no contiene las dos pestañas requeridas.");

            using var entryStream = entry.Open();
            var document = XDocument.Load(entryStream);
            var rows = new List<WorkbookRow>();

            foreach (var rowElement in document.Descendants(SpreadsheetNamespace + "row"))
            {
                var rowNumber = int.TryParse(rowElement.Attribute("r")?.Value, out var parsedRowNumber) ? parsedRowNumber : rows.Count + 1;
                var values = new SortedDictionary<int, string>();

                foreach (var cellElement in rowElement.Elements(SpreadsheetNamespace + "c"))
                {
                    var reference = cellElement.Attribute("r")?.Value ?? string.Empty;
                    var columnIndex = TryGetColumnIndex(reference, out var parsedColumnIndex) ? parsedColumnIndex : values.Count + 1;
                    values[columnIndex] = ReadCellValue(cellElement, sharedStrings);
                }

                rows.Add(new WorkbookRow(rowNumber, values.Count == 0 ? Array.Empty<string>() : Enumerable.Range(1, values.Keys.Max()).Select(index => values.TryGetValue(index, out var value) ? value : string.Empty).ToList()));
            }

            return rows;
        }

        private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
        {
            var entry = archive.GetEntry("xl/sharedStrings.xml");

            if (entry is null)
                return Array.Empty<string>();

            using var entryStream = entry.Open();
            var document = XDocument.Load(entryStream);

            return document.Descendants(SpreadsheetNamespace + "si")
                .Select(item => string.Concat(item.Descendants(SpreadsheetNamespace + "t").Select(text => text.Value)))
                .ToList();
        }

        private static string ReadCellValue(XElement cellElement, IReadOnlyList<string> sharedStrings)
        {
            var cellType = cellElement.Attribute("t")?.Value;

            if (cellType == "inlineStr")
                return string.Concat(cellElement.Descendants(SpreadsheetNamespace + "t").Select(text => text.Value));

            var rawValue = cellElement.Element(SpreadsheetNamespace + "v")?.Value ?? string.Empty;

            if (cellType == "s" && int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sharedStringIndex))
                return sharedStringIndex >= 0 && sharedStringIndex < sharedStrings.Count ? sharedStrings[sharedStringIndex] : string.Empty;

            return rawValue;
        }

        private static void ValidateHeaderRow(IReadOnlyList<WorkbookRow> rows, IReadOnlyList<string> expectedHeaders, string sheetName)
        {
            var header = rows.FirstOrDefault()?.Values ?? Array.Empty<string>();

            for (var index = 0; index < expectedHeaders.Count; index++)
            {
                var actual = NormalizeHeader(header.ElementAtOrDefault(index));
                var expected = NormalizeHeader(expectedHeaders[index]);

                if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"La pestaña {sheetName} debe tener las columnas: {string.Join(", ", expectedHeaders)}.");
                }
            }
        }

        private static string NormalizeHeader(string? value) => (value ?? string.Empty).Trim().Replace(" ", string.Empty, StringComparison.Ordinal);

        private static int ParseInt(string? value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;

        private static decimal ParseDecimal(string? value)
        {
            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariantResult))
                return invariantResult;

            return decimal.TryParse(value, NumberStyles.Number, CultureInfo.GetCultureInfo("es-DO"), out var localResult) ? localResult : 0;
        }

        private static void AddEntry(ZipArchive archive, string path, string content)
        {
            var entry = archive.CreateEntry(path, CompressionLevel.Fastest);

            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write(content.Trim());
        }

        private static string ColumnName(int columnIndex)
        {
            var dividend = columnIndex;
            var columnName = string.Empty;

            while (dividend > 0)
            {
                var modulo = (dividend - 1) % 26;
                columnName = Convert.ToChar('A' + modulo) + columnName;
                dividend = (dividend - modulo) / 26;
            }

            return columnName;
        }

        private static bool TryGetColumnIndex(string reference, out int columnIndex)
        {
            var match = CellReferenceRegex().Match(reference);

            if (!match.Success)
            {
                columnIndex = 0;
                return false;
            }

            columnIndex = 0;

            foreach (var character in match.Groups[1].Value.ToUpperInvariant())
            {
                columnIndex *= 26;
                columnIndex += character - 'A' + 1;
            }

            return true;
        }

        [GeneratedRegex("^([A-Z]+)", RegexOptions.IgnoreCase)]
        private static partial Regex CellReferenceRegex();

        private sealed record WorkbookRow(int RowNumber, IReadOnlyList<string> Values);
    }
}
