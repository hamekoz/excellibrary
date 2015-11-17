using System;
using System.Data;
using System.IO;
using ExcelLibrary.SpreadSheet;

namespace ExcelLibrary
{
	/// <summary>
	/// Provides simple way to convert Excel workbook into DataSet
	/// </summary>
	public static class DataSetHelper
	{
		/// <summary>
		/// Populate all data (all converted into String) in all worksheets
		/// from a given Excel workbook.
		/// </summary>
		/// <param name="filePath">File path of the Excel workbook</param>
		/// <returns>DataSet with all worksheet populate into DataTable</returns>
		public static DataSet CreateDataSet (String filePath)
		{
			var ds = new DataSet ();
			Workbook workbook = Workbook.Load (filePath);
			foreach (Worksheet ws in workbook.Worksheets) {
				DataTable dt = PopulateDataTable (ws);
				ds.Tables.Add (dt);
			}
			return ds;
		}

		/// <summary>
		/// Populate data (all converted into String) from a given Excel
		/// workbook and also work sheet name into a new instance of DataTable.
		/// Returns null if given work sheet is not found.
		/// </summary>
		/// <param name="filePath">File path of the Excel workbook</param>
		/// <param name="sheetName">Worksheet name in workbook</param>
		/// <returns>DataTable with populate data</returns>
		public static DataTable CreateDataTable (String filePath, String sheetName)
		{
			Workbook workbook = Workbook.Load (filePath);
			foreach (Worksheet ws in workbook.Worksheets) {
				if (ws.Name.Equals (sheetName))
					return PopulateDataTable (ws);
			}
			return null;
		}

		static DataTable PopulateDataTable (Worksheet ws)
		{
			CellCollection Cells = ws.Cells;

			// Creates DataTable from a Worksheet
			// All values will be treated as Strings
			var dt = new DataTable (ws.Name);

			// Extract columns
			for (int i = 0; i <= Cells.LastColIndex; i++)
				dt.Columns.Add (Cells [0, i].StringValue, typeof(String));

			// Extract data
			for (int currentRowIndex = 1; currentRowIndex <= Cells.LastRowIndex; currentRowIndex++) {
				DataRow dr = dt.NewRow ();
				for (int currentColumnIndex = 0; currentColumnIndex <= Cells.LastColIndex; currentColumnIndex++)
					dr [currentColumnIndex] = Cells [currentRowIndex, currentColumnIndex].StringValue;
				dt.Rows.Add (dr);
			}

			return dt;
		}

		/// <summary>
		/// Populate all data from the given DataSet into a new Excel workbook
		/// </summary>
		/// <param name="filePath">File path to new Excel workbook to be created</param>
		/// <param name="dataset">Source DataSet</param>
		public static void CreateWorkbook (String filePath, DataSet dataset)
		{
			if (dataset.Tables.Count == 0)
				throw new ArgumentException ("DataSet needs to have at least one DataTable", "dataset");

			var workbook = new Workbook ();
			foreach (DataTable dt in dataset.Tables) {
				var worksheet = new Worksheet (dt.TableName);
				for (int i = 0; i < dt.Columns.Count; i++) {
					// Add column header
					worksheet.Cells [0, i] = new Cell (dt.Columns [i].ColumnName);

					// Populate row data
					for (int j = 0; j < dt.Rows.Count; j++) {
						if (dt.Columns [i].DataType == typeof(DateTime) && dt.Rows [j] [i].GetType () != typeof(DBNull)) {
							var value = (DateTime)dt.Rows [j] [i];
							if (value.Date == DateTime.MinValue.Date && value > DateTime.MinValue) {
								worksheet.Cells [j + 1, i] = new Cell (dt.Rows [j] [i], CellFormat.Time);
							} else {
								if (value.Hour == 0 && value.Minute == 0 && value.Second == 0 && value.Millisecond == 0) {
									worksheet.Cells [j + 1, i] = new Cell (dt.Rows [j] [i], CellFormat.Date);
								} else {
									worksheet.Cells [j + 1, i] = new Cell (dt.Rows [j] [i], CellFormat.DateTime);
								}
							}
						} else {
							worksheet.Cells [j + 1, i] = new Cell (dt.Rows [j] [i]);
						}
					}
				}
				workbook.Worksheets.Add (worksheet);
			}
			workbook.Save (filePath);
		}

		public static void CreateWorkbook (Stream stream, DataSet dataset)
		{
			if (dataset.Tables.Count == 0)
				throw new ArgumentException ("DataSet needs to have at least one DataTable", "dataset");

			var workbook = new Workbook ();
			foreach (DataTable dt in dataset.Tables) {
				var worksheet = new Worksheet (dt.TableName);
				for (int i = 0; i < dt.Columns.Count; i++) {
					// Add column header
					worksheet.Cells [0, i] = new Cell (dt.Columns [i].ColumnName);

					// Populate row data
					for (int j = 0; j < dt.Rows.Count; j++)
						worksheet.Cells [j + 1, i] = new Cell (dt.Rows [j] [i]);
				}
				workbook.Worksheets.Add (worksheet);
			}
			workbook.SaveToStream (stream);
		}
	}
}
