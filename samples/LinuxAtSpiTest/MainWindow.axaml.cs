using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace LinuxAtSpiTest;

public partial class MainWindow : Window
{
    private TextBlock StatusTextBlock => this.FindControl<TextBlock>("StatusTextBlock")!;

    public MainWindow()
    {
        InitializeComponent();
        
        // Set window properties for better AT-SPI testing
        Title = "AT-SPI Test Application";
        Width = 600;
        Height = 500;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            StatusTextBlock.Text = $"Button '{button.Content}' was clicked at {DateTime.Now:HH:mm:ss}";
        }
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            StatusTextBlock.Text = $"Text changed to: {textBox.Text}";
        }
    }

    private void OnSliderValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (sender is Slider slider)
        {
            StatusTextBlock.Text = $"Slider value: {slider.Value:F1}";
            
            // Update the volume label if it exists
            var volumeLabel = this.FindControl<TextBlock>("VolumeLabel");
            if (volumeLabel != null)
            {
                volumeLabel.Text = $"{slider.Value:F0}%";
            }
        }
    }

    private void OnCheckBoxChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
        {
            StatusTextBlock.Text = $"CheckBox '{checkBox.Content}' is now: {checkBox.IsChecked}";
        }
    }

    private void OnComboBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item)
        {
            StatusTextBlock.Text = $"Selected: {item.Content}";
        }
    }
}