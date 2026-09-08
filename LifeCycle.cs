using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.Runtime;
using System.IO;

namespace jcauto2025 {
    /*
        * config_file:
        * LIBRARY:folder[;folder[...]]
        * SCALES:1/75[;1/50[...]] default: 1/50;1/75;1/100
        */
	public class LifeCycle : IExtensionApplication {
        public static LifeCycle? single;
        public List<LibraryFile> libraries;
        public List<string> scales;
		public List<string> directories;
		public void Initialize() {
            single = this;
            libraries = new List<LibraryFile>();
            scales = new List<string>();
            directories = new List<string>();

			string config_file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "jcauto25_config.txt");
			if (File.Exists(config_file)) {
                foreach (string line in File.ReadAllLines(config_file)) {
                    int div = line.IndexOf(':');
                    if (div < 1) continue;
                    string name = line.Substring(0, div).ToLowerInvariant();
                    string[] values = line.Substring(div + 1).Split(';');
                    switch (name) {
                    case "library":
                        foreach (string value in values) {
                            if (File.Exists(value)) {
                                libraries.Add(new LibraryFile(value));
                            } else if (Directory.Exists(value)) {
                                directories.Add(value);
                                foreach (string file in Directory.GetFiles(value)) {
                                    if (file.EndsWith(".dwg"))
                                        libraries.Add(new LibraryFile(file));
                                }
                            }
                        }
                        break;
                    case "scales":
                        scales.AddRange(values);
                        break;
                    }
                }
            } else {
                scales.Add("1/50");
				scales.Add("1/75");
				scales.Add("1/100");
			}
        }
        public void Terminate() {
            scales.Clear();
            libraries.Clear();
        }
    }
}
