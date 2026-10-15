# Changelog

## v5 (October 2026)

Rework of EQS Tool v3 after a NICEIC certificate layout update stopped EICRs from processing.

### Why

The NICEIC update moved several fields on page 1, and changed how the EICR
*Satisfactory/Unsatisfactory* strike-through line is drawn. v3 read the result by checking
the **first** line drawn on the page against fixed X positions. In the new layout about 160
other paths (table borders, footer glyphs) are drawn first, so every EICR went to `ERROR`.
The fixed positions also couldn't be matched from config, because config values were parsed
as `float` while PDF coordinates are `double`.

### EICR result
- Result is now read from the page geometry: find the `Satisfactory/Unsatisfactory` word and
  the `/` inside it, then look for a stroked horizontal line through the word (right of `/`
  = SAT, left = UNSAT). Uses PdfPig's transformed paths, so it works whatever order the page
  is drawn in.
- Removed the `*_RES` boxes and the `…CertificateLineStart/End` settings.

### Layout updates
- EICR: all field boxes remapped for the new layout.
- MW: date box moved.
- EIC: checked, unchanged.
- VIS, Part P, DFHN: not yet checked against new samples.

### Correctness fixes
- **DFHN / Part P UPRN lookup** only accepts a single unambiguous address match. Previously
  `Contains` matching let "1 High Street" match "11 High Street", and an empty address
  matched the first row of the property list. Both could file a certificate under the
  wrong property with no address check to catch it.
- UPRN lookup no longer breaks on a quote character.
- House-number check uses both certificate address lines.
- Certificate type checks the most specific title first, so a Minor Works certificate
  mentioning an "Electrical Installation Certificate" is no longer read as an EIC.
- Dates handle ranges (end date used), brackets, dashes and 2-digit years.
- The EICR result is now carried through to file naming (`_UNSAT` suffix).

### Robustness
- Missing fields no longer crash the run. Every field defaults to an empty string.
- Each file is processed in its own error handler, so one bad PDF can't stop a run.
- The PDF is closed before it is moved or deleted.
- Files that previously stayed in the inbox and failed on every run are now moved, with
  the reason logged, to new folders: `DUPLICATE`, `SKIPPED` (drafts), `WRONG_DEPARTMENT`
  (Empty Homes certificates outside `EH`) and `EMAIL_FAILED`.
- A failed email no longer deletes the file silently.
- `PROPERTIES.xlsx` columns are checked once at startup.
- Box overlays from `DrawRectsToPDF` are written to `_OVERLAYS/` (previously into the inbox,
  where the next run tried to process them), and not attempted for unrecognised documents.
- `isProduction` (needed a rebuild) replaced by an optional `BasePath` setting.
- No "press any key" wait when run unattended.
- Errors and warnings logged at the correct level.

### Build and tooling
- Outlook email is late-bound instead of a COM reference, so the project builds with the
  `dotnet` CLI and VS Code as well as Visual Studio.
- New `EQS_Tool.Tests` xUnit project: generated EICR PDFs for the result logic, fictional
  property data for lookups, and optional checks against real certificates kept in a
  git-ignored `samples/` folder.
- VS Code build / test / debug / publish tasks.
- Build output (`bin/`, `obj/`) no longer committed.

### Removed
- Audit mode (`GenerateAuditData`, `CertificateMode`, team list), which was no longer used.
- Unused code: observations parsing, `CertificatePage`, `CertificateDataCollection`,
  `PrintWordList`, `FileFunctions`, `TextWithCoordinates`, unused `Sylvan.Data` package.

### Kept as-is
- 2 second delay between files, so copies to CIFS shares complete.

## v3 and earlier

The original tool: certificate type detection, field extraction from configurable page
boxes, UPRN/address validation against the property list, renaming and filing per
department, optional Outlook email, and an audit-report mode (since removed).
