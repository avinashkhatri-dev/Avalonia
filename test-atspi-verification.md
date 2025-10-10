# AT-SPI Verification Commands

## The Issue
`busctl --user tree` shows the session bus, but AT-SPI objects are correctly registered on the **accessibility bus** (separate from session bus). This is the correct behavior!

## How to Verify AT-SPI is Working

### 1. Check if AT-SPI accessibility bus is running:
```bash
ls -la /run/user/$(id -u)/at-spi/
```

### 2. Check AT-SPI registry daemon:
```bash
ps aux | grep at-spi
```

### 3. Connect to the accessibility bus specifically:
```bash
# Get the accessibility bus address
export AT_SPI_BUS_ADDRESS=$(ls /run/user/$(id -u)/at-spi/bus 2>/dev/null && echo "unix:path=/run/user/$(id -u)/at-spi/bus")

# Use busctl with the accessibility bus
busctl --address="$AT_SPI_BUS_ADDRESS" tree
```

### 4. Test with actual AT-SPI tools:
```bash
# Install AT-SPI utilities (if not already installed)
sudo apt install at-spi2-utils

# List accessible applications
busctl --address="unix:path=/run/user/$(id -u)/at-spi/bus" call \
  org.a11y.atspi.Registry /org/a11y/atspi/registry \
  org.a11y.atspi.Registry GetApplications

# Or use atspi-info if available
atspi-info
```

### 5. Test with screen readers:
```bash
# If Orca is installed
orca --replace &

# The ButtonTestApp should now be discoverable by Orca
./ButtonTestApp
```

## Expected Behavior
- AT-SPI objects should NOT appear on session bus (`busctl --user tree`)
- AT-SPI objects SHOULD appear on accessibility bus
- Screen readers should be able to discover and interact with the application
- Application should register with AT-SPI registry daemon

## Debug Output
The application will show:
- "Connected to AT-SPI accessibility bus"
- "AT-SPI connection established on accessibility bus: :1.XXX"
- Individual object registrations