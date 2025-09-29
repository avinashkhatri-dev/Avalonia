# Quick Linux Testing

## Build and Run

```bash
# Make executable
chmod +x run.sh

# Build and run
./run.sh
```

Or manually:
```bash
dotnet build -c Release
dotnet run -c Release
```

## Application Info

- **Name**: "AT-SPI Test Application"
- **Window Title**: "AT-SPI Test Application" 
- **Contains**: Various controls (buttons, textboxes, etc.) for AT-SPI testing

## Test with AT-SPI tools

In another terminal:
```bash
# Command line inspector
atspi-spy

# GUI inspector (if installed)
accerciser
```

The application should appear in the AT-SPI accessibility tree for testing.