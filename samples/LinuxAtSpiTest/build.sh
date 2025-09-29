#!/bin/bash

# Linux AT-SPI Test Application Build Script

echo "Building AT-SPI Test Application..."

# Build the application
dotnet build LinuxAtSpiTest.csproj -c Release

if [ $? -eq 0 ]; then
    echo "Build successful!"
    echo ""
    echo "To run the application:"
    echo "  dotnet run --project LinuxAtSpiTest.csproj"
    echo ""
    echo "To test AT-SPI accessibility:"
    echo "  1. Install AT-SPI tools: sudo apt install at-spi2-core at-spi2-utils"
    echo "  2. Run the application in one terminal"
    echo "  3. In another terminal, run: atspi-spy"
    echo "  4. Or run: accerciser (GUI accessibility inspector)"
    echo ""
    echo "The application should appear in the AT-SPI tree as 'AT-SPI Test Application'"
else
    echo "Build failed!"
    exit 1
fi