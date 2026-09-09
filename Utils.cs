using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using System.Windows.Controls;
using Autodesk.AutoCAD.BoundaryRepresentation;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using Autodesk.AutoCAD.EditorInput;
using Jint.Runtime.Interop;
using Autodesk.AutoCAD.Customization;
using DocumentFormat.OpenXml.Vml.Office;
using Jint;
using DocumentFormat.OpenXml.Office2010.Excel;
using aacTable = Autodesk.AutoCAD.DatabaseServices.Table;
using aacCell = Autodesk.AutoCAD.DatabaseServices.Cell;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using DocumentFormat.OpenXml.Drawing.Charts;
using Jint.Runtime.Modules;
using System.IO;

namespace jcauto2025 {
	class Utils {
		public class TableCell {
			public object data;
			public bool left, right, top, bottom;
			public int colSpan;
			public int rowSpan;
			public bool center;
			public int decimals;
			public bool header;
			public TableCell(object data, bool header) {
				this.data = data;
				left = true;
				right = true;
				top = header;
				bottom = header;
				center = true;
				decimals = 2;
				this.header = header;
				colSpan = 1;
				rowSpan = 1;
			}
		}
		public class Table {
			public string title;
			public bool left, right, top, bottom;
			public bool center;
			public TableCell[][] cells;

			public Table(string title) {
				this.title = title;
				left = true;
				right = true;
				top = true;
				bottom = true;
				center = true;
				cells = new TableCell[0][];
			}
		}
		public interface ScriptAPI {
			public interface Object {
				ObjectId objectId { get; }
				string type { get; }
				string data { get; }
				string[] properties { get; }
				Point3d[] points { get; }
				object get(string key);
				//void set(string key, object value);
			}
			void log(string msg);
			public ScriptAPI.Object[] findBox(Point3d i, Point3d e);
			public void setBlockProperties(ObjectId id, Dictionary<string, object> props);
			public void setBlockContent(string handle, Point3d i, Point3d e, Point3d origin);
			public void tableSet(string handle, Table[] data);
		}
		public class ScriptAPIImplement: ScriptAPI {
			public class ObjectPoly : ScriptAPI.Object {
				public ObjectId objectId { get; }
				public string type { get; }
				public string data { get; }
				public Point3d[] points { get; }
				public string[] properties { get {
						return new string[0];
					}
				}
				public object get(string key) {
					return "";
				}
				//public void set(string key, object value) { }
				public ObjectPoly(ObjectId id, string type, string data, Point3d[] points) {
					objectId = id;
					this.type = type;
					this.data = data;
					this.points = points;
				}
			}
			public class ObjectBlock : ScriptAPI.Object {
				Point3d point;
				Dictionary<string, object> properties_data;
				public ObjectId objectId { get; }
				public string type { get; }
				public string data { get; }
				public Point3d[] points { get { return new Point3d[1] { point }; } }
				public string[] properties { get { return properties_data.Keys.ToArray(); } }
				public object get(string key) {
					if (properties_data.TryGetValue(key, out object v))
						return v;
					return null;
				}
				public ObjectBlock(ObjectId id, string type, string data, Point3d point, Dictionary<string, object> properties) {
					objectId = id;
					this.type = type;
					this.data = data;
					this.point = point;
					this.properties_data = properties;
				}
			}
			public Database ac_database;
			public Editor ac_editor;
			public void log(string msg) {
				ac_editor.WriteMessage("\n"+msg);
			}
			public ScriptAPI.Object[] findBox(Point3d init, Point3d eend) {
				Extents3d limits = new Extents3d(init, eend);
				Dictionary<ObjectId,string> cache_blockComment = new Dictionary<ObjectId, string>();
				List<ScriptAPI.Object> ret = new List<ScriptAPI.Object>();
				using (Transaction ac_transition = ac_database.TransactionManager.StartTransaction()) {
					foreach (ObjectId id in getModelSpace(ac_database, ac_transition)) {
						Entity? entity = ac_transition.GetObject(id, OpenMode.ForRead) as Entity;
						try {
							if (entity == null || !extentsContain(limits, entity.GeometricExtents)) continue;
						} catch { continue; }
						string data = "";
						if (entity is BlockReference bref) {
							if (entity.Hyperlinks == null || entity.Hyperlinks.Count == 0 || !entity.Hyperlinks[0].DisplayString.StartsWith('#')) {
								if (!bref.DynamicBlockTableRecord.IsNull) {
									ObjectId blockid = bref.DynamicBlockTableRecord;
									if (cache_blockComment.TryGetValue(blockid, out string v))
										data = v;
									else {
										BlockTableRecord btr = (BlockTableRecord)ac_transition.GetObject(blockid, OpenMode.ForRead);
										cache_blockComment[blockid] = btr.Comments;
										data = btr.Comments;
									}
								}
								if (data == "") continue;
							} else data = entity.Hyperlinks[0].DisplayString;
							Dictionary<string, object> props = new Dictionary<string, object>();
							if (bref.IsDynamicBlock) {
								foreach (DynamicBlockReferenceProperty prop in bref.DynamicBlockReferencePropertyCollection)
									props[prop.PropertyName] = prop.Value;
							}
							ret.Add(new ObjectBlock(id, bref.Name, data, bref.Position, props));
						}
						if (entity == null || entity.Hyperlinks == null || entity.Hyperlinks.Count == 0 || !entity.Hyperlinks[0].DisplayString.StartsWith('#')) continue;
						data = entity.Hyperlinks[0].DisplayString;
						if (entity is Line line) {
							ret.Add(new ObjectPoly(id, "?LINE", data, new Point3d[2] { line.StartPoint, line.EndPoint }));
						} else if (entity is Polyline pline) {
							List<Point3d> points = new List<Point3d>();
							int nv = pline.NumberOfVertices;
							for (int i = 0; i < nv; i++)
								points.Add(pline.GetPoint3dAt(i));
							ret.Add(new ObjectPoly(id, "?PLINE", data, points.ToArray()));
						} else if (entity is Polyline3d pline3d) {
							List<Point3d> points = new List<Point3d>();
							foreach (ObjectId vertexId in pline3d) {
								var vertice = ac_transition.GetObject(vertexId, OpenMode.ForRead) as PolylineVertex3d;
								if (vertice != null) {
									points.Add(vertice.Position);
								}
							}
							ret.Add(new ObjectPoly(id, "?PLINE3D", data, points.ToArray()));
						}
					}
				}
				return ret.ToArray();
			}
			public void setBlockProperties(ObjectId id, Dictionary<string, object> props) {
				using (Transaction ac_transition = ac_database.TransactionManager.StartTransaction()) {
					BlockReference? bref = ac_transition.GetObject(id, OpenMode.ForWrite, false, true) as BlockReference;
					if (bref == null) return;

					if (bref.IsDynamicBlock) {
						foreach (DynamicBlockReferenceProperty prop in bref.DynamicBlockReferencePropertyCollection) {
							if (prop.ReadOnly) continue;
							if (props.TryGetValue(prop.PropertyName, out object newvalue))
								prop.Value = newvalue;
						}
					}
					ac_transition.Commit();
				}
			}
			public void setBlockContent(string handle, Point3d init, Point3d eend, Point3d origin) {
				using (Transaction ac_transition = ac_database.TransactionManager.StartTransaction()) {
					ObjectId idObj = ac_database.GetObjectId(false, new Handle(Convert.ToInt64(handle, 16)), 0);
					if (!idObj.IsValid || idObj.IsNull || idObj.IsErased) {
						ac_editor.WriteMessage("\n Table handle " + handle + " is invalid");
						return;
					}
					BlockTableRecord blk = ac_transition.GetObject(idObj, OpenMode.ForWrite) as BlockTableRecord;
					if (blk == null) {
						ac_editor.WriteMessage("\n Table handle " + handle + " is not a Block definition");
						return;
					}

					//clear block
					foreach (ObjectId idAntigo in blk) {
						DBObject blk_obj = ac_transition.GetObject(idAntigo, OpenMode.ForWrite);
						if (blk_obj is Entity blk_ent)
							blk_ent.Erase(true);
					}
					//copy new data
					ObjectIdCollection res_ids = selectBox(ac_database, ac_transition, init, eend);
					IdMapping id_mapping = new IdMapping();
					ac_database.DeepCloneObjects(res_ids, blk.Id, id_mapping, false);

					Matrix3d move = Matrix3d.Displacement(Point3d.Origin - origin);

					foreach (IdPair par in id_mapping) {
						if (par.IsCloned) {
							Entity? new_entity = ac_transition.GetObject(par.Value, OpenMode.ForWrite) as Entity;
							if (new_entity != null) {
								new_entity.TransformBy(move);
							}
						}
					}
					ac_transition.Commit();
				}
			}
			public void tableSet(string handle, Table[] data) {
				using (Transaction ac_transition = ac_database.TransactionManager.StartTransaction()) {
					ObjectId idObj = ac_database.GetObjectId(false, new Handle(Convert.ToInt64(handle, 16)), 0);
					if (!idObj.IsValid || idObj.IsNull || idObj.IsErased)
					{
						ac_editor.WriteMessage("\n Table handle " + handle + " is invalid");
						return;
					}
                    aacTable? table = ac_transition.GetObject(idObj, OpenMode.ForWrite) as aacTable;
					if (table == null) {
						ac_editor.WriteMessage("\n Table handle " + handle + " is not a table ["+ idObj.ObjectClass.DxfName + "]");
						return;
					}
					if (table == null) return;
					int ic = 0, ec = 0, ir = 0, er = 0;
					for (int i = 0; i < data.Length; i++) {
						if (data[i].cells.Length > 0)
							ec = Math.Max(ec, data[i].cells[0].Length);
						er += ((data[i].title == null) ? 0 : 1) + data[i].cells.Length;
					}
					table.SetSize(er, ec);
					table.UnmergeCells(CellRange.Create(table, 0, 0, er - 1, ec - 1));
					aacCell c;
					for (int i = 0; i < data.Length; i++) {
						if ((data[i].title != null)) {
							table.MergeCells(CellRange.Create(table, ir, 0, ir, ec - 1));
							c = table.Cells[ir, 0];
							c.TextString = data[i].title;
							c.Borders.Bottom.IsVisible = data[i].bottom;
							c.Borders.Top.IsVisible = data[i].top;
							c.Borders.Left.IsVisible = data[i].left;
							c.Borders.Right.IsVisible = data[i].right;
							c.Style = "Title";
							ir++;
						}
						for (int iir = 0; iir < data[i].cells.Length; iir++) {
							for (ic = 0; ic < ec; ic++) {
								if (ic >= data[i].cells[iir].Length) {
									c = table.Cells[ir, 0];
									c.TextString = "";
									c.Borders.Bottom.IsVisible = false;
									c.Borders.Top.IsVisible = false;
									c.Borders.Left.IsVisible = false;
									c.Borders.Right.IsVisible = false;
									c.Style = "Data";
									continue;
								}
								TableCell cell = data[i].cells[iir][ic];
								if (cell.rowSpan > 0 || cell.colSpan > 0)
									table.MergeCells(CellRange.Create(table, ir, ic, ir + cell.rowSpan - 1, ic + cell.colSpan - 1));
								c = table.Cells[ir, 0];
								if (c.IsMerged != null && c.TopLeft.Column != ic && c.TopLeft.Row != ir) continue;
								c.TextString = cell.data.ToString();
								c.Borders.Bottom.IsVisible = cell.bottom;
								c.Borders.Top.IsVisible = cell.top;
								c.Borders.Left.IsVisible = cell.left;
								c.Borders.Right.IsVisible = cell.right;
								c.Style = cell.header ? "Header" : "Data";
							}
							ir++;
						}
					}
					ac_transition.Commit();
				}
			}
		}
		public class LCModuleLoader : IModuleLoader {
			public LCModuleLoader(){}
			public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest request) {
				string specifier = request.Specifier;
				// relative path
				if ((specifier.StartsWith("./") || specifier.StartsWith("../")) && referencingModuleLocation != null) {
					string? baseDir = Path.GetDirectoryName(referencingModuleLocation);
					if (baseDir != null) {
						string absolutePath = Path.GetFullPath(Path.Combine(baseDir, specifier));
						return new ResolvedSpecifier(request, absolutePath, new Uri(absolutePath), SpecifierType.RelativeOrAbsolute);
					}
				}
				if (LifeCycle.single != null) {
					foreach (var dir in LifeCycle.single.directories) {
						var testPath = Path.GetFullPath(Path.Combine(dir, specifier));
						if (File.Exists(testPath)) {
							return new ResolvedSpecifier(request, testPath, new Uri(testPath), SpecifierType.RelativeOrAbsolute);
						}
					}
				}
				return new ResolvedSpecifier(request, specifier, null, SpecifierType.Bare);
			}
			public Module LoadModule(Engine engine, ResolvedSpecifier resolved) {
				string filePath = resolved.Key;
				if (!File.Exists(filePath)) {
					throw new Jint.Runtime.JavaScriptException($"Could not find module at path: {filePath}");
				}
				string sourceCode = File.ReadAllText(filePath);
				return Jint.Runtime.Modules.ModuleFactory.BuildSourceTextModule(engine, resolved, sourceCode);
			}
		}
		public static Engine scriptAPILoad (Editor ed, Database db) {
			var engine = new Engine(options => {
				options.TimeoutInterval(TimeSpan.FromSeconds(120));
				options.Modules.ModuleLoader = new LCModuleLoader();
			});
			ScriptAPIImplement imp = new ScriptAPIImplement();
			imp.ac_editor = ed;
			imp.ac_database = db;
			ScriptAPI scriptAPI = imp;
			engine.SetValue("API", scriptAPI);
			engine.SetValue("Point3d", typeof(Point3d));
			engine.SetValue("Table", typeof(Table));
			engine.SetValue("TableCell", typeof(TableCell));
			return engine;
		}
		public static void scriptAPIRun(Engine eg, String src) {
			String id = "main"+new Random().NextInt64().ToString();
			eg.Modules.Add(id, src);
			eg.Modules.Import(id);
		}
		/*
		public class TagContext {
			public class ActionCount {
				public string tag;
				public Handle outputTable;
				public int col;
				public int row;
			}
			public class ActionCopy {
				public string block;
				public Handle outputTable;
				public int col;
				public int row;
				public List<string> properties;
			}
			public List<ActionCount> actionCounts = new List<ActionCount>();
			public List<ActionCopy> actionCopies = new List<ActionCopy>();
			public Point3d window_min, window_max;
			public Dictionary<string, int> count = new Dictionary<string, int>();
			public static Point3d decode_3dpoint(string x) {
				string[] v = x.Split(";");
				double[] r = new double[3];
				for (int i = 0; i < 3; i++)
					r[i] = Double.Parse(v[i].Trim());
				return new Point3d(r);
			}
			public void decode_minmax(string x) {
				string[] parts = x.Split("/");
				window_min = decode_3dpoint(parts[0]);
				window_max = decode_3dpoint(parts[1]);
			}
			public static void decode_cell(string x, out int col, out int row) {
				col = 0; row = 0;
				char[] cell = x.ToUpperInvariant().ToCharArray();
				for (int i = 0, e = cell.Length; i < e; i++) {
					if (cell[i] >= 48 && cell[i] < 58)
						row = row * 10 + (cell[i] - 48);
					if (cell[i] >= 65 && cell[i] < 91)
						col = col * 26 + (cell[i] - 65);
				}
			}
			public bool decode_action(string query, string output) {
				if (query.StartsWith("#")) {
					ActionCount ac = new ActionCount();
					ac.tag = query;
					string[] outf = output.Split('.');
					ac.outputTable = new Handle(Convert.ToInt64(outf[0].ToUpperInvariant(), 16));
					decode_cell(outf[1], out ac.col, out ac.row);
					actionCounts.Add(ac);
				} else if (query.StartsWith("!")) {
					ActionCopy ac = new ActionCopy();
					ac.block = query.Substring(1);
					string[] outf = output.Split(['.', '/']);
					ac.outputTable = new Handle(Convert.ToInt64(outf[0].ToUpperInvariant(), 16));
					decode_cell(outf[1], out ac.col, out ac.row);
					for (int i = 2, e = outf.Length; i < e; i++)
						ac.properties.Add(outf[i]);
					actionCopies.Add(ac);
				} else return false;
				return true;
			}
			public bool decode_table(ref int i, Autodesk.AutoCAD.DatabaseServices.Table table) {
				if (table.Columns.Count < 3) return false;
				if (table.Rows.Count <= i) return false;
				Autodesk.AutoCAD.DatabaseServices.Cell window = table.Cells[i, 0];
				int ei = window.BottomRow + 1;
				decode_minmax(window.Contents[0].Value as string);
				actionCounts.Clear();
				actionCopies.Clear();
				for (int e = table.Rows.Count; i < e; i++)
					decode_action(table.Cells[i, 1].Contents[0].Value as string, table.Cells[i, 2].Contents[0].Value as string);
				return true;
			}
		}*/
		private static void fixScale(ObjectContextCollection scaleCollection, string scaleName, double f1, double f2) {
			string[] parts = scaleName.Split('/');
			double scale1, scale2;
			try {
				scale1 = Double.Parse(parts[0]) * f1;
				scale2 = Double.Parse(parts[1]) * f2;
			} catch { return; }
			if (scaleCollection.HasContext(scaleName)) {
				AnnotationScale? crtScale = scaleCollection.GetContext(scaleName) as AnnotationScale;
				if (crtScale != null) {
					crtScale.PaperUnits = scale1;
					crtScale.DrawingUnits = scale2;
				}
			} else {
				AnnotationScale newScale = new AnnotationScale();
				newScale.Name = scaleName;
				newScale.PaperUnits = scale1;
				newScale.DrawingUnits = scale2;

				scaleCollection.AddContext(newScale);
			}
		}
		public static PromptSelectionResult? getSelection(string txt, Editor ed, bool single) {
			PromptSelectionResult result = ed.SelectImplied();
			if (result == null || result.Status != PromptStatus.OK || result.Value.Count == 0 || (single && result.Value.Count != 1)) {
				PromptSelectionOptions opcoes = new PromptSelectionOptions();
				opcoes.MessageForAdding = "\n"+txt;
				opcoes.SingleOnly = true;

				result = ed.GetSelection(opcoes);
				if (result.Status != PromptStatus.OK) {
					ed.WriteMessage("\n  Selection canceled");
					return null;
				}
			}
			return result;
		}
		public static void updateBlocks(Database destDb, Editor ed) {

			int count_textStyles = 0;
			int count_dimStyles = 0;
			int count_blocks = 0;
			int count_libraries = 0;

			foreach (LibraryFile lf in LifeCycle.single.libraries) {
				bool update = true;
				string? cur_date_str = Utils.kv_get(destDb, lf.kv);
				if (cur_date_str != null && DateTime.TryParseExact(cur_date_str, "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime curr_date)) {
					if (
						curr_date.Year == lf.lastchange.Year &&
						curr_date.Month == lf.lastchange.Month &&
						curr_date.Day == lf.lastchange.Day &&
						curr_date.Hour == lf.lastchange.Hour &&
						curr_date.Minute == lf.lastchange.Minute &&
						curr_date.Second == lf.lastchange.Second
					) update = false;
				}
				if (!update) continue;
				count_libraries++;
				count_textStyles += Utils.updateTextStyleTable(destDb, lf.db);
				count_dimStyles += Utils.updateDimStyleTable(destDb, lf.db);
				count_blocks += Utils.updateBlockTable(destDb, lf.db);
				Utils.kv_set(destDb, lf.kv, lf.lastchange.ToString("dd/MM/yyyy HH:mm:ss"));
			}
			ed.WriteMessage("\n UPDATES:");
			ed.WriteMessage("\n  LIBRARIES = " + count_libraries.ToString());
			ed.WriteMessage("\n  TEXT_STYLES = " + count_textStyles.ToString());
			ed.WriteMessage("\n  DIM_STYLES = " + count_dimStyles.ToString());
			ed.WriteMessage("\n  BLOCKS = " + count_blocks.ToString());
		}
		public static void generateScales(Database dst, List<string> scalesName) {
			using (ObjectIdCollection removeScaleIds = new ObjectIdCollection())
			using (Transaction trd = dst.TransactionManager.StartTransaction()) {
				double f1 = 1;
				double f2 = 1;
				switch (dst.Insunits) {
				case UnitsValue.Centimeters: f1 = 10; break;
				case UnitsValue.Decimeters: f1 = 100; break;
				case UnitsValue.Meters: f1 = 1000; break;
				case UnitsValue.Kilometers: f1 = 1000000; break;
				}

				ObjectContextManager contextManager = dst.ObjectContextManager;
				ObjectContextCollection scaleCollection = contextManager.GetContextCollection("ACDB_ANNOTATIONSCALES");
				foreach (ObjectContext oc in scaleCollection) {
					if (oc is AnnotationScale) {
						AnnotationScale scale = (AnnotationScale)oc;
						if (scale.Name == "1:1" || scale.Name == dst.Cannoscale.Name) continue;
						bool canAdd = true;
						foreach (string scaleStr in scalesName) {
							if (scale.Name == scaleStr) {
								canAdd = false;
								break;
							}
						}
						if (canAdd)
							removeScaleIds.Add(new ObjectId(scale.UniqueIdentifier));
					}
				}
				if (removeScaleIds.Count > 0) {
					dst.Purge(removeScaleIds);
					foreach (ObjectId scaleId in removeScaleIds) {
						ObjectContext? scale = scaleCollection.Cast<ObjectContext>()
							.FirstOrDefault(x => x.UniqueIdentifier == scaleId.OldIdPtr);
						if (scale != null)
							scaleCollection.RemoveContext(scale.Name);
					}
				}
				foreach (ObjectContext oc in scaleCollection) {
					if (oc is AnnotationScale) {
						AnnotationScale scale = (AnnotationScale)oc;
						if (scale.Name.IndexOf('/')<1) continue;
						bool canAdd = true;
						foreach (string scaleStr in scalesName) {
							if (scale.Name == scaleStr) {
								canAdd = false;
								break;
							}
						}
						scalesName.Add(scale.Name);
					}
				}
				foreach (string scaleName in scalesName)
					fixScale(scaleCollection, scaleName, f1, f2);
				trd.Commit();
			}
		}
		public static string kv_table = "jcauto2025";
		public static void kv_set(Database db, string key, string value) {
			using (Transaction tr = db.TransactionManager.StartTransaction()) {
				DBDictionary nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite);
				DBDictionary table;

				if (nod.Contains(kv_table))
					table = (DBDictionary)tr.GetObject(nod.GetAt(kv_table), OpenMode.ForWrite);
				else {
					table = new DBDictionary();
					nod.SetAt(kv_table, table);
					tr.AddNewlyCreatedDBObject(table, true);
				}

				Xrecord xRec = new Xrecord();
				xRec.Data = new ResultBuffer(new TypedValue((int)DxfCode.Text, value));
				table.SetAt(key, xRec);
				tr.AddNewlyCreatedDBObject(xRec, true);

				tr.Commit();
			}
		}
		public static string? kv_get(Database db, string key) {
			using (Transaction tr = db.TransactionManager.StartTransaction()) {
				DBDictionary nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
				if (!nod.Contains(kv_table)) return null;

				DBDictionary table = (DBDictionary)tr.GetObject(nod.GetAt(kv_table), OpenMode.ForRead);
				if (!table.Contains(key)) return null;

				foreach (TypedValue tv in ((Xrecord)tr.GetObject(table.GetAt(key), OpenMode.ForRead)).Data) {
					if (tv.TypeCode == (int)DxfCode.Text) {
						return tv.Value.ToString();
					}
				}
			}
			return null;
		}
		static public int updateBlockTable(Database dst, Database src) {
			return updateTable<BlockTable, BlockTableRecord>(dst, src);
		}
		static public int updateDimStyleTable(Database dst, Database src) {
			return updateTable<DimStyleTable, DimStyleTableRecord>(dst, src);
		}
		static public int updateTextStyleTable(Database dst, Database src) {
			return updateTable<TextStyleTable, TextStyleTableRecord>(dst, src);
		}
		static public int updateTable<Table, Record>(Database dst, Database src)
			where Table : SymbolTable
			where Record : SymbolTableRecord {
			using (ObjectIdCollection blockIdsToClone = new ObjectIdCollection()) {
				using (Transaction trd = dst.TransactionManager.StartTransaction())
				using (Transaction tro = src.TransactionManager.StartTransaction()) {
					ObjectId dstTableId = dst.BlockTableId, srcTableId = src.BlockTableId;
					if (typeof(BlockTable).IsAssignableFrom(typeof(Table))) {
						dstTableId = dst.BlockTableId;
						srcTableId = src.BlockTableId;
					} else if (typeof(DimStyleTable).IsAssignableFrom(typeof(Table))) {
						dstTableId = dst.DimStyleTableId;
						srcTableId = src.DimStyleTableId;
					} else if (typeof(TextStyleTable).IsAssignableFrom(typeof(Table))) {
						dstTableId = dst.TextStyleTableId;
						srcTableId = src.TextStyleTableId;
					}
					Table srcBT = (Table)tro.GetObject(srcTableId, OpenMode.ForRead);
					Table dstBT = (Table)trd.GetObject(dstTableId, OpenMode.ForRead);
					foreach (ObjectId btrId in srcBT) {
						Record btr = (Record)tro.GetObject(btrId, OpenMode.ForRead);
						if (btr is BlockTableRecord) {
							BlockTableRecord bbtr = btr as BlockTableRecord;
							if (bbtr.IsLayout || bbtr.IsAnonymous)
								continue;
						} else if (btr is DimStyleTableRecord) {
							DimStyleTableRecord dbtr = btr as DimStyleTableRecord;
							if (dbtr.IsDependent)
								continue;
						} else if (btr is TextStyleTableRecord) {
							TextStyleTableRecord tbtr = btr as TextStyleTableRecord;
							if (tbtr.Name.Equals("Standard", StringComparison.OrdinalIgnoreCase))
								continue;
						}
						if (dstBT.Has(btr.Name))
							blockIdsToClone.Add(btrId);
					}
					tro.Commit();
					if (blockIdsToClone.Count == 0) return 0;
					using (IdMapping mapping = new IdMapping()) {
						dst.WblockCloneObjects(
							blockIdsToClone,
							dstTableId,
							mapping,
							DuplicateRecordCloning.Replace,
							false
						);
					}
					trd.Commit();
				}
				return blockIdsToClone.Count;
			}
		}
		public static bool extentsContain(Extents3d larg, Extents3d obj) {
			return obj.MaxPoint.X > larg.MinPoint.X && obj.MinPoint.X < larg.MaxPoint.X &&
				obj.MaxPoint.Y > larg.MinPoint.Y && obj.MinPoint.Y < larg.MaxPoint.Y &&
				obj.MaxPoint.Z > larg.MinPoint.Z && obj.MinPoint.Z < larg.MaxPoint.Z;
		}
		public static BlockTableRecord getModelSpace(Database db, Transaction tr) {
			BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
			return tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
		}
		public static ObjectIdCollection selectBox(Database db, Transaction tr, Point3d i, Point3d e) {
			Extents3d limits = new Extents3d(i, e);
			ObjectIdCollection ret = new ObjectIdCollection();
			foreach (ObjectId id in getModelSpace(db, tr)) {
				Entity? ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
				if (ent != null) {
					try {
						Extents3d currExtents = ent.GeometricExtents;
						if (extentsContain(limits,currExtents))
							ret.Add(id);
					} catch {
						continue;
					}
				}
			}
			tr.Commit();
			return ret;
		}
	}
}
