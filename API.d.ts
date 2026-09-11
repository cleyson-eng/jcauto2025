export {};
declare global {
	type int = number;
	type double = number;
	class cellText {
		left:boolean
		right:boolean
		top:boolean
		bottom:boolean
		center:boolean
	}
	class TableCell extends cellText {
		data:any
		colSpan:int
		rowSpan:int
		decimals:int
		header:boolean
		constructor (data:any, header:boolean);
	}
	class Table extends cellText {
		title:string;
		cells:TableCell[][];
		constructor (title:string);
	}
	class Point3d {
		constructor (x:double, y:double, z:double);
		X:double
		Y:double
		Z:double
		DistanceTo:(w:Point3d)=>double
	}
	namespace API {
		interface Object {
			readonly objectId:object
			readonly type:string
			readonly data:string
			readonly properties:string[]
			readonly points:Point3d[]
			get:(key:string)=>any;
		}
		const log:(msg:string)=>void;
		const findBox:(i:Point3d, e:Point3d)=>Object[];
		const setBlockProperties:(id:object, props:Record<string, any>)=>void;
		const setBlockContent:(handle:string, i:Point3d, e:Point3d, origin:Point3d)=>void;
		const tableSet:(handle:string, data:Table[])=>void;
		const updateLibraryBlocks:()=>void;//command: JLIBRARY
		const updateScales:()=>void;//command: JSETUP_SCALES
	}
}