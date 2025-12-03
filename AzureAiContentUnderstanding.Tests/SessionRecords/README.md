# Test Session Recordings

This directory contains HTTP session recordings for integration tests.

## Structure

- Each test class has its own subdirectory
- Recording files are named after test methods
- Recordings are in JSON format

## Usage

### Record Mode
Set the `AZURE_TEST_MODE` environment variable to `Record`:
```powershell
$env:AZURE_TEST_MODE = "Record"
dotnet test --filter "SampleNumber=16"
```

### Playback Mode
Set the `AZURE_TEST_MODE` environment variable to `Playback`:
```powershell
$env:AZURE_TEST_MODE = "Playback"
dotnet test --filter "SampleNumber=16"
```

### Live Mode
Set the `AZURE_TEST_MODE` environment variable to `Live`:
```powershell
$env:AZURE_TEST_MODE = "Live"
dotnet test --filter "SampleNumber=16"
```

## Sanitization

Recordings are automatically sanitized to remove sensitive data:
- Authorization headers
- SAS tokens and query parameters
- Analyzer IDs
- API keys

## Note

Do not commit recordings with sensitive data. Always verify sanitization before committing.
