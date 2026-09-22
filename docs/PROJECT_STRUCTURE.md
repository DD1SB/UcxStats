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
│   │   └── Database/
│   │       ├── IQsoRepository.cs
│   │       ├── QsoImportRecord.cs
│   │       └── SqliteQsoRepository.cs
│   ├── UcxStats.Geo/
│   │   └── Maidenhead/
│   └── UcxStats.Wpf/
│       ├── MainWindow.xaml
│       └── MainWindow.xaml.cs
├── tests/
│   └── UcxStats.SmokeTest/
├── DONE.md
├── TODO.md
├── README.md
├── global.json
└── UcxStats.sln
```
