#!/bin/bash

# Simple script to build and run the AT-SPI test application

echo "Building AT-SPI Test Application..."
dotnet build -c Release

if [ $? -eq 0 ]; then
    echo "Build successful. Starting application..."
    dotnet run -c Release
else
    echo "Build failed!"
    exit 1
fi