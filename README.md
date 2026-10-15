# EQS Tool v5

See [CHANGELOG.md](CHANGELOG.md) for what changed from v3 and why.

A command-line tool that processes **NICEIC electrical certificate PDFs**. It reads the
certificate details, checks the property against a master property list, then renames the
file and copies it to the right destination. Anything that doesn't check out is moved to a
folder that says why.

Supported certificate types:

| Type | Certificate |
|---|---|
| `EICR` | Electrical Installation Condition Report |
| `EIC` | Electrical Installation Certificate |
| `MW` | Minor Electrical Installation Works Certificate |
| `VIS` | Domestic Visual Condition Report |
| `PARTP` | Building Regulations (Part P) eNotification |
| `DFHN` | Commissioning of a Fire Detection and Fire Alarm System |

---

## How it works

For every PDF in `Certificates/EH` and `Certificates/RR`:

1. **Identify the certificate type** from the title on page 1.
2. **Read the fields** (job reference, UPRN, certificate number, date, address, postcode,
   engineer, supervisor, occupier) from boxes on page 1 defined in `App.config`.
3. **EICR only: read the result.** NICEIC strikes through the word that doesn't apply in
   *"Satisfactory/Unsatisfactory"*. The tool finds that word and the `/` in it, then looks
   for a drawn line through it:
   - line right of the `/` → "Unsatisfactory" struck out → **SAT**
   - line left of the `/` → "Satisfactory" struck out → **UNSAT**

   No line positions are hard-coded, so small layout changes don't break it.
4. **Find the property.** UPRN-based certificates are looked up by UPRN. DFHN and Part P
   carry no UPRN, so they are matched by address, and only a **single unambiguous match**
   is accepted.
5. **Check the address** (except DFHN/Part P). House numbers and postcode must match
   exactly, and the full address must be within `ScoreThreshold` character edits.
6. **Final checks.** Draft certificates are skipped. Empty Homes certificates (occupier
   `VOID` / `EMPTY`) are only accepted from the `EH` folder.
7. **Rename and file**, e.g. `123456_DEICR_13-10-26.pdf`, copied to the department's
   destination (or `PROCESSED` when `AutoMoveFiles` is off), optionally emailed via Outlook,
   then the original is removed.

There is a 2 second pause between files so copies to network (CIFS) shares can complete.

### Where rejected files go

Each department folder gets these sub-folders automatically, and every move is logged with
the reason:

| Folder | Why a file ends up here |
|---|---|
| `ERROR` | Unreadable PDF, unknown document, EICR result not found, no date, no certificate number |
| `UPRN_ERROR` | UPRN missing or not found in `PROPERTIES.xlsx` (or an ambiguous address for DFHN/Part P) |
| `ADDRESS_CHECK_FAILED` | Certificate address doesn't match the property list |
| `WRONG_DEPARTMENT` | Empty Homes certificate (`VOID`/`EMPTY` occupier) outside the `EH` folder |
| `SKIPPED` | Draft certificate |
| `DUPLICATE` | A file with the new name already exists at the destination |
| `EMAIL_FAILED` | Copied to the destination, but the email couldn't be sent |
| `PROCESSED` | Renamed copies, when `AutoMoveFiles` is `no` |

If the destination share is unavailable, the file is left in place and retried on the next run.

---

## Folder layout

```
<BasePath>/
├── PROPERTIES.xlsx          property list (columns: "Property Reference", "Property Address")
├── Certificates/
│   ├── EH/                  drop certificates here...
│   └── RR/                  ...or here
└── _logs/                   one log file per run
```

`BasePath` defaults to the current working folder. When that is inside `bin/` (F5 in
VS Code or Visual Studio), the project folder (`EQS_Tool/`) is used instead. Set
`BasePath` in `App.config` to point anywhere else.

The tool stops with a clear message if `PROPERTIES.xlsx` is missing or doesn't have the two
required columns.

---

## Configuration (`App.config`)

| Setting | Meaning |
|---|---|
| `BasePath` | Folder holding `Certificates`, `_logs` and `PROPERTIES.xlsx` (blank = auto) |
| `ScoreThreshold` | Maximum character edits allowed between certificate and property-list address |
| `AutoMoveFiles` | `yes` = copy to destination shares; `no` = renamed copy to `PROCESSED` only |
| `TGPEHFilePath`, `TGPRRFilePath`, `TGPUNSATFilePath` | Destination folders for EH, RR and unsatisfactory EICRs |
| `AutoEmail…` | Email certificates after copying (Windows + Outlook only) |
| `EHEmailAddress`, `RREmailAddress` | Comma-separated recipients |
| `NamingFormat` | File name pattern, default `UPRN_TYPE_DD-MM-YY` |
| `<TYPE>_<FIELD>` | Field boxes, see below |
| `PrintTextCoordinates`, `DrawRectsToPDF`, `PrintDataToConsole` | Developer tools for remapping fields (default `no`) |

### Field boxes

Each field is read from a rectangle on page 1:

```xml
<add key="EICR_UPRN" value="579,432,650,446"/>   <!-- left,bottom,right,top -->
```

Coordinates are PDF points with the origin at the **bottom-left** of the page. Fields:
`JOB`, `UPRN`, `CERT`, `DATE`, `ADD1`, `ADD2`, `PC`, `ENG`, `SUP`, `OCC`. Use
`0,0,0,0` for fields a certificate type doesn't have.

### When NICEIC changes the layout

1. Set `PrintTextCoordinates` to `yes` and run against one sample of the changed type. Every
   word on page 1 is printed with its coordinates.
2. Update the boxes so each value sits inside, with 2–5 points of padding.
3. Set `DrawRectsToPDF` to `yes` to check. An overlay PDF with the boxes drawn in red is
   written to `Certificates/<dept>/_OVERLAYS/`.
4. Run the sample tests (below) to confirm.

---

## Building

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet build              # build
dotnet test               # run the tests
dotnet run --project EQS_Tool
```

Release build for Windows:

```bash
dotnet publish EQS_Tool -c Release -r win-x64 --self-contained false -o publish
```

Deploy the whole `publish/` folder. `EQS_Tool.dll.config` in that folder is the built
`App.config`; edit it there for each machine.

### VS Code

Install the recommended **C# Dev Kit** extension when prompted, then:

- **Ctrl+Shift+B** → build
- **Terminal → Run Test Task** → tests
- **F5** → run with the debugger

Visual Studio 2022 opens `EQS_Tool.sln` as normal.

Email uses Outlook through late-bound COM, so the project builds anywhere. Sending email
only works on Windows with Outlook installed.

---

## Tests

`EQS_Tool.Tests` (xUnit) covers certificate-type detection, date parsing, EICR result
detection (with generated PDFs), UPRN/address lookups, address scoring and file naming.
All test data is fictional.

### Testing with real certificates

Real certificates contain tenant details, so **they are never committed**. Put them in
`EQS_Tool.Tests/samples/` (git ignored), or point the `EQS_SAMPLES` environment variable
at a folder, and name each one after what it should produce:

```
TYPE_RESULT_UPRN_DD-MM-YYYY.pdf

EICR_SAT_123456_13-10-2026.pdf
EIC_NA_123456_15-10-2026.pdf
MW_NA_123456_15-10-2026.pdf
```

`dotnet test` then checks every sample: type, UPRN, date, certificate number, address,
postcode, engineer and (for EICRs) the SAT/UNSAT result.

---

## Project structure

| File | Purpose |
|---|---|
| `Program.cs` | Main loop, per-file processing and routing |
| `CertificateFunctions.cs` | Type detection, field extraction, EICR result, date parsing |
| `DataTableFunction.cs` | Loading `PROPERTIES.xlsx`, UPRN and address lookups |
| `StringFunctions.cs` | Address match scoring (Levenshtein) |
| `FileNameHelpers.cs` | Output file names |
| `CertificateProcessing.cs` | Copying to the destination share |
| `Email.cs` | Outlook email |
| `ApplicationConfig.cs` | Loading and validating `App.config` |
| `DevTools.cs` | Coordinate printing and box overlays for remapping |
| `Logger.cs` | Console and file logging |
