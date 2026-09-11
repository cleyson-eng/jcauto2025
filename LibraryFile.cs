using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using DocumentFormat.OpenXml.InkML;
using System.IO;
using System.Dynamic;


namespace jcauto2025 {

	public class LibraryFile {
		public bool directFile { get; private set; }
		public LibraryFile (string path, bool directFile = false) {
			this.path = path;
			kv = Path.GetFileNameWithoutExtension(path);
			this.directFile = directFile;
		}
		public string kv { get; private set; }
		public string path;
		public DateTime lastchange { get; private set; }
		public Database? db { get; private set; } = null;
		public void dbLoad() {
			dbUnload();
			this.lastchange = new FileInfo(path).LastWriteTimeUtc;
			db = new Database(false, true);
			try {
				db.ReadDwgFile(path, System.IO.FileShare.Read, true, "");
			} finally { }
		}
		public void dbUnload() {
			if (db == null) return;
			try {
				db.Dispose();
				db = null;
			} finally { }
		}
		~LibraryFile() {
			dbUnload();
		}
	}
}
