using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using ExcelLibrary.BinaryDrawingFormat;
using ExcelLibrary.SpreadSheet;

namespace ExcelLibrary.BinaryFileFormat
{
	public static class WorkbookEncoder
	{
		public static void Encode (Workbook workbook, Stream stream)
		{
			List<Record> records = EncodeWorkbook (workbook);

			var writer = new BinaryWriter (stream);
			foreach (Record record in records) {
				record.Write (writer);
			}
			writer.Close ();
		}

		static List<Record> EncodeWorkbook (Workbook workbook)
		{
			var sharedResource = new SharedResource (true);
			var book_records = new List<Record> ();
			var bof = new BOF ();
			bof.BIFFversion = 0x0600; //0600H = BIFF8
			bof.StreamType = StreamType.WorkbookGlobals;
			bof.BuildID = 3515;
			bof.BuildYear = 1996;
			bof.RequiredExcelVersion = 6;
			book_records.Add (bof);

			var codepage = new CODEPAGE ();
			codepage.CodePageIdentifier = (ushort)Encoding.Unicode.CodePage;
			book_records.Add (codepage);

			var window = new WINDOW1 ();
			window.WindowWidth = 16384;
			window.WindowHeight = 8192;
			window.SelecteWorksheets = 1;
			window.TabBarWidth = 600;
			window.OptionFlags = 56;
			book_records.Add (window);

			var dateMode = new DATEMODE ();

			switch (Environment.OSVersion.Platform) {
			case PlatformID.MacOSX:
				sharedResource.BaseDate = DateTime.Parse ("1904-01-01");
				dateMode.Mode = 1;
				break;
			case PlatformID.Unix:
				sharedResource.BaseDate = DateTime.Parse ("1899-12-30");
				dateMode.Mode = 0;
				break;
			default:
				sharedResource.BaseDate = DateTime.Parse ("1899-12-31");
				dateMode.Mode = 0;
				break;
			}

			book_records.Add (dateMode);

			var all_sheet_records = new List<List<Record>> ();
			foreach (Worksheet worksheet in workbook.Worksheets) {
				List<Record> sheet_records = WorkSheetEncoder.Encode (worksheet, sharedResource);
				Record.EncodeRecords (sheet_records);
				all_sheet_records.Add (sheet_records);
			}

			book_records.AddRange (sharedResource.FormatRecords.ToArray ());
			book_records.AddRange (sharedResource.ExtendedFormats.ToArray ());

			var boundSheets = new List<BOUNDSHEET> ();
			foreach (Worksheet worksheet in workbook.Worksheets) {
				var boundSheet = new BOUNDSHEET ();
				boundSheet.Visibility = 0; // 00H = Visible
				boundSheet.SheetType = (byte)SheetType.Worksheet;
				boundSheet.SheetName = worksheet.Name;
				boundSheet.StreamPosition = 0;
				boundSheets.Add (boundSheet);
				book_records.Add (boundSheet);
			}

			if (sharedResource.Images.Count > 0) {
				book_records.Add (EncodeImages (sharedResource.Images));
			}

			Record.EncodeRecords (book_records);
			int sstOffset = Record.CountDataLength (book_records);

			book_records.Add (sharedResource.SharedStringTable);
			book_records.Add (CreateEXTSST (sharedResource.SharedStringTable, sstOffset));

			var eof = new EOF ();
			book_records.Add (eof);

			Record.EncodeRecords (book_records);
			int dataLength = Record.CountDataLength (book_records);

			for (int i = 0; i < workbook.Worksheets.Count; i++) {
				boundSheets [i].StreamPosition = (uint)dataLength;
				boundSheets [i].Encode ();

				int sheet_length = Record.CountDataLength (all_sheet_records [i]);
				dataLength += sheet_length;
			}

			var all_records = new List<Record> ();
			all_records.AddRange (book_records);
			foreach (List<Record> sheet_records in all_sheet_records) {
				all_records.AddRange (sheet_records);
			}
			return all_records;
		}

		static EXTSST CreateEXTSST (SST sst, int sstOffset)
		{
			var extSST = new EXTSST ();
			extSST.NumStrings = 8;

			int counter = 0;
			int totalLength = sstOffset + 0x0C;
			int relativeLength = 0x0C;
			foreach (string text in sst.StringList) {
				int stringLength = Record.GetStringDataLength (text);
				if (relativeLength + stringLength > Record.MaxContentLength + 4) {
					totalLength += 4;
					relativeLength = 4;
				}
				if (counter == 0) {
					var stringOffset = new StringOffset ();
					stringOffset.AbsolutePosition = (uint)totalLength;
					stringOffset.RelativePosition = (ushort)relativeLength;
					extSST.Offsets.Add (stringOffset);
				}
				counter++;
				if (counter == extSST.NumStrings) {
					counter = 0;
				}
				totalLength += stringLength;
				relativeLength += stringLength;
			}

			return extSST;
		}

		static Record EncodeImages (IList<Image> images)
		{
			var drawingGroup = new MSODRAWINGGROUP ();
			var dggContainer = new MsofbtDggContainer ();
			drawingGroup.EscherRecords.Add (dggContainer);

			var dgg = new MsofbtDgg ();
			dgg.NumSavedDrawings = images.Count;
			dgg.NumSavedShapes = images.Count + 1;
			dgg.MaxShapeID = 1024 + dgg.NumSavedShapes;
			dgg.GroupIdClusters.Add (1, dgg.NumSavedShapes);
			dggContainer.EscherRecords.Add (dgg);

			var bstoreContainer = new MsofbtBstoreContainer ();
			bstoreContainer.Instance = (ushort)images.Count;
			foreach (Image image in images) {
				var blipStoreEntry = new MsofbtBSE ();
				blipStoreEntry.UID = Guid.NewGuid ();
				blipStoreEntry.Ref = 1;
				blipStoreEntry.Version = 2;
				blipStoreEntry.BlipRecord = CreateBlipRecord (image);
				blipStoreEntry.BlipRecord.Type = image.Format;
				blipStoreEntry.BlipRecord.ImageData = image.Data;
				blipStoreEntry.BlipRecord.UID = blipStoreEntry.UID;
				blipStoreEntry.BlipRecord.Marker = 255;
				blipStoreEntry.SetBlipType (image.Format);
				bstoreContainer.EscherRecords.Add (blipStoreEntry);
			}
			dggContainer.EscherRecords.Add (bstoreContainer);

			var defautProperties = new MsofbtOPT ();
			defautProperties.Add (PropertyIDs.FitTextToShape, 524296);
			defautProperties.Add (PropertyIDs.FillColor, 134217793);
			defautProperties.Add (PropertyIDs.LineColor, 134217792);
			dggContainer.EscherRecords.Add (defautProperties);

			var splitMenuColors = new MsofbtSplitMenuColors ();
			splitMenuColors.Instance = 4;
			splitMenuColors.Color1 = 134217741;
			splitMenuColors.Color2 = 134217740;
			splitMenuColors.Color3 = 134217751;
			splitMenuColors.Color4 = 268435703;
			dggContainer.EscherRecords.Add (splitMenuColors);

			return drawingGroup;
		}

		static MsofbtBlip CreateBlipRecord (Image image)
		{
			switch (image.Format) {
			case EscherRecordType.MsofbtBlipMetafileEMF:
				return new MsofbtBlipMetafileEMF ();
			case EscherRecordType.MsofbtBlipMetafileWMF:
				return new MsofbtBlipMetafileWMF ();
			case EscherRecordType.MsofbtBlipMetafilePICT:
				return new MsofbtBlipMetafilePICT ();
			case EscherRecordType.MsofbtBlipBitmapJPEG:
				return new MsofbtBlipBitmapJPEG ();
			case EscherRecordType.MsofbtBlipBitmapPNG:
				return new MsofbtBlipBitmapPNG ();
			case EscherRecordType.MsofbtBlipBitmapDIB:
				return new MsofbtBlipBitmapDIB ();
			default:
				throw new Exception ("Image format not supported.");
			}
		}
	}
}
