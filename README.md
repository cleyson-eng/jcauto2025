# J. AutoCAD2025 Automation plugin
## Configuration file (%HOME%/jcauto25_config.txt) syntax
```
LIBRARY:folder[;folder[...]]
SCALES:1/75[;1/50[...]] default: 1/50;1/75;1/100
```

## Commands
- JCA_INFO: show loaded configuration.
- JSETUP_SCALES: setup/fix scales.
- JUPDATE: sync. cache/update all file dwg data from LIBRARY paths (for block auto update).
- JLIBRARY: update blocks from current file within all dwg files found in LIBRARY paths.
- JHANDLE: get information and the HANDLE of an entity.
- JRUN: run JS code (MTEXT/TEXT)

## JS syntax
- Importing: use relative (N/A on embeded code) or js file in LIBRARY paths
- VSCode library autocomplete (copy API.d.ts file):
```
// @ts-check
/// <reference path="./API.d.ts" />
```
JS execution is sandboxed, Look API.d.ts to know capabilities.