

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using System.Globalization;
using Autodesk.AutoCAD.Internal.DatabaseServices;
using Jint;
using static jcauto2025.Utils;

[assembly: CommandClass(typeof(jcauto2025.CCommands))]
[assembly: ExtensionApplication(typeof(jcauto2025.LifeCycle))]

namespace jcauto2025
{
    public class CCommands {
		[CommandMethod("JCA_INFO")]
		public void JCA_INFO() {
			Document doc = AcadApp.DocumentManager.MdiActiveDocument;
			if (doc == null) return;
			Editor ed = doc.Editor;
			using (DocumentLock dlock = doc.LockDocument()) {
				ed.WriteMessage("\n LIBRARIES:");
				foreach (LibraryFile lib in LifeCycle.single.libraries) {
					ed.WriteMessage("\n  " + lib.path);
				}
				ed.WriteMessage("\n SCALES:");
				foreach (string scale in LifeCycle.single.scales) {
					ed.WriteMessage("\n  " + scale);
				}
			}
		}
		[CommandMethod("JSETUP_SCALES")]
		public void JSETUP_SCALES() {
			Document doc = AcadApp.DocumentManager.MdiActiveDocument;
			if (doc == null) return;

			Database destDb = doc.Database;
			Editor ed = doc.Editor;
			using (DocumentLock dlock = doc.LockDocument()) {
				Utils.generateScales(destDb, LifeCycle.single.scales);
			}
			doc.Editor.Regen();
		}
		[CommandMethod("JUPDATE")]
		public void JUPDATE() {
			Document doc = AcadApp.DocumentManager.MdiActiveDocument;
			if (doc == null) return;

			Database destDb = doc.Database;
			Editor ed = doc.Editor;

			using (DocumentLock dlock = doc.LockDocument()) {
				foreach (LibraryFile lf in LifeCycle.single.libraries)
					lf.dbLoad();
			}
		}
		[CommandMethod("JLIBRARY")]
		public void JLIBRARY() {
			Document doc = AcadApp.DocumentManager.MdiActiveDocument;
			if (doc == null) return;

			Database destDb = doc.Database;
			Editor ed = doc.Editor;

			using (DocumentLock dlock = doc.LockDocument()) {
				Utils.updateBlocks(destDb, ed);
			}
			doc.Editor.Regen();
		}
		[CommandMethod("JRUN")]
		public void JRUN() {
			Document doc = AcadApp.DocumentManager.MdiActiveDocument;
			if (doc == null) return;

			Database db = doc.Database;
			Editor ed = doc.Editor;

			PromptSelectionResult? result = Utils.getSelection("Select JS to run", ed, true);
			if (result == null) return;

			using (DocumentLock dlock = doc.LockDocument()) {
				string script = string.Empty;
				using (Transaction tr = db.TransactionManager.StartTransaction()) {
					SelectedObject r = result.Value[0];
					Entity entity = tr.GetObject(result.Value[0].ObjectId, OpenMode.ForRead) as Entity;

					if (entity is MText mtext)
						script = mtext.Text;
					else if (entity is DBText dbtext)
						script = dbtext.TextString;
					if (script == string.Empty) return;
				}

				var engine = scriptAPILoad(ed, db);
				engine.Execute(script);
			}
			doc.Editor.Regen();
		}
		[CommandMethod("JHANDLE")]
		public void JHANDLE() {
			Document doc = AcadApp.DocumentManager.MdiActiveDocument;
			if (doc == null) return;

			Database db = doc.Database;
			Editor ed = doc.Editor;

			PromptSelectionResult? result = Utils.getSelection("Select object to get information", ed, true);
			if (result == null) return;

			using (DocumentLock dlock = doc.LockDocument())
			using (Transaction tr = db.TransactionManager.StartTransaction()) {
				foreach (SelectedObject obj in result.Value) {
					if (obj != null) {
						Entity entity = (Entity)tr.GetObject(obj.ObjectId, OpenMode.ForRead);

						string objectType = entity.Id.ObjectClass.DxfName;

						ed.WriteMessage($"\n[{objectType}] {entity.Handle.Value.ToString("X")} (Layer: {entity.Layer})");

						if (entity is BlockReference bref) {
							ed.WriteMessage($"\n is a block, dynamic: " + (bref.IsDynamicBlock?"Y":"N"));
							if (bref.IsDynamicBlock) {
								ed.WriteMessage($"\n props: " + bref.DynamicBlockReferencePropertyCollection.Count.ToString());
								foreach (DynamicBlockReferenceProperty prop in bref.DynamicBlockReferencePropertyCollection)
									ed.WriteMessage($"\n  {prop.PropertyName} = {prop.Value}");
							}
							ObjectId blockDefId = bref.DynamicBlockTableRecord;
							BlockTableRecord btr = (BlockTableRecord)tr.GetObject(blockDefId, OpenMode.ForRead);
							ed.WriteMessage($"\n block def {btr.Handle.Value.ToString("X")} description: {btr.Comments}");
						}
					}
				}
				tr.Commit();
			}
		}
	}
}
