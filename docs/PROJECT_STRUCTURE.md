# Project structure

```text
UcxStats/
├── .vscode/
│   ├── launch.json
│   └── tasks.json
├── docs/
│   ├── ARCHITECTURE.md
│   ├── DATABASE.md
│   ├── PROJECT_STRUCTURE.md
│   └── UCX_FORMAT.md
├── src/
│   ├── UcxStats.Core/
│   │   └── Ucx/
│   │       ├── UcxFileReader.cs
│   │       ├── UcxRecordParser.cs
│   │       ├── UcxQsoRecord.cs
│   │       ├── UcxSourceRecord.cs
│   │       ├── UcxRecordValidator.cs
│   │       └── ...
│   ├── UcxStats.Data/
│   │   ├── Confirmation/
│   │   ├── Database/
│   │   │   ├── IQsoRepository.cs
│   │   │   ├── QsoScope.cs
│   │   │   ├── QsoImportRecord.cs
│   │   │   └── SqliteQsoRepository.cs
│   │   └── Dxcc/
│   │       ├── Adif317DxccCatalog.Generated.cs
│   │       ├── UcxLog850CountryDxccMappings.Generated.cs
│   │       ├── DxccEntityCatalog.cs
│   │       ├── DxccGeoJsonLoader.cs
│   │       ├── DxccIdentityResolver.cs
│   │       ├── UcxCountryDxccMapping.cs
│   │       └── DxccStatusModels.cs
│   ├── UcxStats.Geo/
│   │   └── Maidenhead/
│   ├── UcxStats.Map/
│   │   ├── DxccMapConfiguration.cs
│   │   ├── DxccMapPayload.cs
│   │   └── MapPageBuilder.cs
│   └── UcxStats.Wpf/
│       ├── DxccMapSettingsWindow.xaml
│       ├── MapWindow.xaml
│       ├── MainWindow.xaml
│       └── MainWindow.xaml.cs
├── tests/
│   ├── UcxStats.Data.Tests/
│   └── UcxStats.SmokeTest/
├── tools/
│   ├── generate-dxcc-catalog.sh
│   └── generate-ucx-dxcc-mapping.sh
├── DONE.md
├── TODO.md
├── README.md
├── global.json
└── UcxStats.sln
```
