#!/bin/bash

# AT-SPI Test Runner Script for Linux

echo "=================================="
echo "Avalonia AT-SPI Test Application"
echo "=================================="
echo ""

# Check if we're on Linux
if [[ "$OSTYPE" != "linux-gnu"* ]]; then
    echo "Warning: This test is designed for Linux systems with AT-SPI support"
    echo "Current OS: $OSTYPE"
    echo ""
fi

# Check if AT-SPI tools are available
echo "Checking AT-SPI tools availability..."

if command -v atspi-spy &> /dev/null; then
    echo "✓ atspi-spy found"
    ATSPI_SPY_AVAILABLE=true
else
    echo "✗ atspi-spy not found"
    ATSPI_SPY_AVAILABLE=false
fi

if command -v accerciser &> /dev/null; then
    echo "✓ accerciser found"
    ACCERCISER_AVAILABLE=true
else
    echo "✗ accerciser not found"
    ACCERCISER_AVAILABLE=false
fi

if [ "$ATSPI_SPY_AVAILABLE" = false ] && [ "$ACCERCISER_AVAILABLE" = false ]; then
    echo ""
    echo "AT-SPI inspection tools not found. Install with:"
    echo "  Ubuntu/Debian: sudo apt install at-spi2-core at-spi2-utils"
    echo "  Optional GUI:  sudo apt install accerciser"
    echo ""
fi

# Check D-Bus session
if [ -z "$DBUS_SESSION_BUS_ADDRESS" ]; then
    echo "⚠ Warning: DBUS_SESSION_BUS_ADDRESS not set"
    echo "  AT-SPI functionality may not work properly"
else
    echo "✓ D-Bus session bus address: $DBUS_SESSION_BUS_ADDRESS"
fi

echo ""
echo "Building and running the test application..."
echo ""

# Build the application
dotnet build LinuxAtSpiTest.csproj -c Release --verbosity quiet

if [ $? -eq 0 ]; then
    echo "✓ Build successful"
    echo ""
    echo "Starting AT-SPI Test Application..."
    echo "Application Title: 'AT-SPI Test Application'"
    echo ""
    
    if [ "$ATSPI_SPY_AVAILABLE" = true ]; then
        echo "To inspect with atspi-spy, run in another terminal:"
        echo "  atspi-spy"
        echo ""
    fi
    
    if [ "$ACCERCISER_AVAILABLE" = true ]; then
        echo "To inspect with accerciser GUI, run in another terminal:"
        echo "  accerciser"
        echo ""
    fi
    
    echo "Expected AT-SPI behavior:"
    echo "- Application should appear in AT-SPI tree"
    echo "- Controls should have proper roles (Button, TextBox, etc.)"
    echo "- Interactions should be possible through AT-SPI"
    echo "- Status updates should reflect user actions"
    echo ""
    
    # Run the application
    dotnet run --project LinuxAtSpiTest.csproj --configuration Release
else
    echo "✗ Build failed!"
    exit 1
fi