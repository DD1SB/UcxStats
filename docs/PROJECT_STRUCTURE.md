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
├── packaging/             # standalone Windows installer inputs and payload policy
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
│   │   ├── AtomicJsonFile.cs
│   │   ├── DxccMapConfiguration.cs
│   │   ├── DxccMapPayload.cs
│   │   ├── MapPathWidths.cs
│   │   ├── MapSettings.cs
│   │   ├── MapSettingsStore.cs
│   │   ├── MapSettingsService.cs
│   │   └── MapPageBuilder.cs
│   └── UcxStats.Wpf/
│       ├── DxccMapSettingsWindow.xaml
│       ├── MapWindow.xaml
│       ├── MainWindow.xaml
│       └── MainWindow.xaml.cs
├── tests/
│   ├── UcxStats.Data.Tests/
│   ├── UcxStats.SmokeTest/
│   └── packaging/
├── third-party/           # preserved dependency license/notice texts
├── tools/
│   ├── dotnet_sdk.py
│   ├── generate-dxcc-catalog.sh
│   ├── generate-ucx-dxcc-mapping.sh
│   ├── publish-win-x64.sh
│   └── validate-windows-publish.py
├── Directory.Build.props
├── DONE.md
├── LICENSE
├── THIRD-PARTY-NOTICES.txt
├── TODO.md
├── README.md
├── global.json
└── UcxStats.sln
```
