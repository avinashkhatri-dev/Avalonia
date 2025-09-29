# Linux AT-SPI Test Application

This sample application demonstrates AT-SPI (Assistive Technology Service Provider Interface) support in Avalonia on Linux systems.

## Features

The application includes various UI controls to test AT-SPI functionality:

- **Buttons**: Standard buttons with click events
- **Text Input**: TextBox controls with different configurations
- **Selection Controls**: CheckBoxes, RadioButtons, and ComboBox
- **Numeric Controls**: Slider and NumericUpDown
- **Progress Indicators**: ProgressBar (determinate and indeterminate)
- **Status Display**: Dynamic text updates based on user interactions

## Building and Running

### Prerequisites

- .NET 8.0 SDK
- Linux system with AT-SPI support
- Optional: AT-SPI inspection tools

### Build

```bash
# Make build script executable
chmod +x build.sh

# Build the application
./build.sh
```

Or manually:
```bash
dotnet build LinuxAtSpiTest.csproj -c Release
```

### Run

```bash
dotnet run --project LinuxAtSpiTest.csproj
```

## Testing AT-SPI Functionality

### Install AT-SPI Tools

On Ubuntu/Debian:
```bash
sudo apt install at-spi2-core at-spi2-utils
```

Optional GUI inspector:
```bash
sudo apt install accerciser
```

### Testing Steps

1. **Run the application**:
   ```bash
   dotnet run --project LinuxAtSpiTest.csproj
   ```

2. **Inspect with command-line tool**:
   ```bash
   # In another terminal
   atspi-spy
   ```

3. **Inspect with GUI tool** (if installed):
   ```bash
   accerciser
   ```

4. **Look for the application** in the AT-SPI tree:
   - Application name: "AT-SPI Test Application"
   - Should show all UI controls with proper roles
   - Try interacting with controls through AT-SPI

### Expected AT-SPI Roles

The controls should appear with these AT-SPI roles:

- **Buttons** → `ATSPI_ROLE_PUSH_BUTTON`
- **TextBox** → `ATSPI_ROLE_TEXT` or `ATSPI_ROLE_ENTRY`
- **CheckBox** → `ATSPI_ROLE_CHECK_BOX`
- **RadioButton** → `ATSPI_ROLE_RADIO_BUTTON`
- **ComboBox** → `ATSPI_ROLE_COMBO_BOX`
- **Slider** → `ATSPI_ROLE_SLIDER`
- **ProgressBar** → `ATSPI_ROLE_PROGRESS_BAR`
- **TextBlock** → `ATSPI_ROLE_LABEL` or `ATSPI_ROLE_TEXT`

### Interaction Testing

You can test the following through AT-SPI:

1. **Button clicks**: Should trigger the OnButtonClick event
2. **Text input**: Should update the status display
3. **Checkbox state changes**: Should report checked/unchecked states
4. **Slider value changes**: Should report current value
5. **Selection changes**: ComboBox and RadioButton selections

### Debugging

If AT-SPI is not working:

1. **Check D-Bus session**: Ensure `DBUS_SESSION_BUS_ADDRESS` is set
2. **Check AT-SPI daemon**: `ps aux | grep at-spi`
3. **Check accessibility**: Some systems may need accessibility to be enabled
4. **Check logs**: Look for AT-SPI related messages in application output

### Environment Variables

You can set these for debugging:
```bash
export ATSPI_DEBUG=1
export DBUS_VERBOSE=1
dotnet run --project LinuxAtSpiTest.csproj
```

## Implementation Details

This application uses:

- **AtspiRoot**: Application-level AT-SPI registration
- **LinuxControlAutomationPeer**: Linux-specific automation peers
- **AtspiContext**: Individual control AT-SPI implementation
- **Role mapping**: Automatic mapping from Avalonia controls to AT-SPI roles

The AT-SPI implementation automatically:
- Registers the application with the AT-SPI registry
- Creates accessibility objects for each control
- Maintains parent-child relationships
- Handles property queries and method calls
- Reports state changes and events

## Troubleshooting

### Application not visible in AT-SPI tree
- Check if AT-SPI services are running
- Verify D-Bus session bus connectivity
- Ensure accessibility services are enabled

### Controls not interactive through AT-SPI
- Check if automation peers are being created
- Verify role mappings are correct
- Look for D-Bus communication errors

### Performance issues
- AT-SPI caching may need optimization
- Large control trees might need lazy loading
- Consider reducing property queries frequency