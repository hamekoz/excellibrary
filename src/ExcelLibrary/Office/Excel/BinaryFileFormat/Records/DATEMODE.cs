using System;
using System.IO;

namespace ExcelLibrary.BinaryFileFormat
{
	public partial class DATEMODE : Record
	{
		public DATEMODE (Record record) : base (record)
		{
		}

		public DATEMODE ()
		{
			Type = RecordType.DATEMODE;
		}

		/// <summary>
		/// 0 = Base date is 1899-Dec-31; 1 = Base date is 1904-Jan-01
		/// </summary>
		public Int16 Mode;

		public override void Decode ()
		{
			var stream = new MemoryStream (Data);
			var reader = new BinaryReader (stream);
			Mode = reader.ReadInt16 ();
		}

		public override void Encode ()
		{
			var stream = new MemoryStream ();
			var writer = new BinaryWriter (stream);
			writer.Write (Mode);
			Data = stream.ToArray ();
			Size = (UInt16)Data.Length;
			base.Encode ();
		}
	}
}
