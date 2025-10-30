using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EVBHelper.Services;
using LibEfex;
using LibEfex.core;
using LibEfex.exception;
using LibEfex.io;
using LibUsbDotNet;
using LibUsbDotNet.Main;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace EVBHelper.ViewModels;

public partial class EfexViewModel : ViewModelBase
{
    private readonly IFileDialogService _fileDialogService;

    [ObservableProperty] private ObservableCollection<UsbRegistry> _devices = new();

    [ObservableProperty] private UsbRegistry? _selectedDevice;

    private EfexContext? _efexContext;

    [ObservableProperty] private string _address = "0x80000000";

    [ObservableProperty] private string _length = "1024";

    [ObservableProperty] private string? _filePath;

    [ObservableProperty] private string _logOutput = "";

    public EfexViewModel(IFileDialogService fileDialogService)
    {
        _fileDialogService = fileDialogService;
    }

    private void Log(string message)
    {
        LogOutput += $"{message}\n";
    }

    [RelayCommand]
    private void Scan()
    {
        Devices.Clear();
        var allDevices = UsbDevice.AllDevices;
        foreach (UsbRegistry device in allDevices)
            if (device.Vid == 0x1f3a && device.Pid == 0xefe8)
                Devices.Add(device);
        Log($"Scan complete. Found {Devices.Count} devices.");
    }

    [RelayCommand]
    private void Connect()
    {
        if (SelectedDevice == null)
        {
            Log("No device selected.");
            return;
        }

        if (SelectedDevice.Open(out var device))
        {
            _efexContext = new EfexContext(new LibUsbDevice(device));
            Log($"Connected to {SelectedDevice.Name}.");
            // You might want to verify the device mode here
        }
        else
        {
            Log($"Failed to connect to {SelectedDevice.Name}.");
        }
    }

    [RelayCommand]
    private void Disconnect()
    {
        _efexContext?.Dispose();
        _efexContext = null;
        Log("Device disconnected.");
    }

    [RelayCommand]
    private async Task BrowseFile()
    {
        var result = await _fileDialogService.OpenFileAsync(new FileDialogRequest());
        if (result != null) FilePath = result;
    }

    private bool ParseAddress(out uint addr)
    {
        try
        {
            addr = uint.Parse(Address.Replace("0x", ""), NumberStyles.HexNumber);
            return true;
        }
        catch
        {
            Log("Invalid address format.");
            addr = 0;
            return false;
        }
    }

    private bool ParseLength(out int len)
    {
        try
        {
            len = int.Parse(Length);
            return true;
        }
        catch
        {
            Log("Invalid length format.");
            len = 0;
            return false;
        }
    }

    private bool IsConnected()
    {
        if (_efexContext != null) return true;
        Log("Error: No device connected.");
        return false;
    }

    [RelayCommand]
    private void FelExecute()
    {
        if (!IsConnected() || !ParseAddress(out var addr)) return;

        try
        {
            Log($"Executing at address 0x{addr:X8}...");
            Fel.Exec(_efexContext!, addr);
            Log("Execution command sent.");
        }
        catch (EfexException ex)
        {
            Log($"Error: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task FelRead()
    {
        if (!IsConnected() || !ParseAddress(out var addr) || !ParseLength(out var len)) return;

        if (string.IsNullOrEmpty(FilePath))
        {
            Log("Please select a file to save the data.");
            return;
        }

        try
        {
            Log($"Reading {len} bytes from 0x{addr:X8}...");
            var data = Fel.Read(_efexContext!, addr, len);
            await File.WriteAllBytesAsync(FilePath, data);
            Log($"Successfully read {data.Length} bytes and saved to {FilePath}.");
        }
        catch (EfexException ex)
        {
            Log($"Error: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task FelWrite()
    {
        if (!IsConnected() || !ParseAddress(out var addr)) return;

        if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath))
        {
            Log("Please select a valid file to write.");
            return;
        }

        try
        {
            var data = await File.ReadAllBytesAsync(FilePath);
            Log($"Writing {data.Length} bytes to 0x{addr:X8} from {FilePath}...");
            Fel.Write(_efexContext!, addr, data, data.Length);
            Log("Write command sent.");
        }
        catch (EfexException ex)
        {
            Log($"Error: {ex.Message}");
        }
    }

    [RelayCommand]
    private void FesQueryStorage()
    {
        if (!IsConnected()) return;
        try
        {
            Log("Querying storage type...");
            var storageType = Fes.QueryStorage(_efexContext!);
            Log($"Storage Type: {storageType}");
        }
        catch (EfexException ex)
        {
            Log($"Error: {ex.Message}");
        }
    }

    [RelayCommand]
    private void FesQuerySecure()
    {
        if (!IsConnected()) return;
        try
        {
            Log("Querying secure mode...");
            var secureType = Fes.QuerySecure(_efexContext!);
            Log($"Secure Mode: {secureType}");
        }
        catch (EfexException ex)
        {
            Log($"Error: {ex.Message}");
        }
    }

    [RelayCommand]
    private void FesProbeFlashSize()
    {
        if (!IsConnected()) return;
        try
        {
            Log("Probing flash size...");
            var flashSize = Fes.ProbeFlashSize(_efexContext!);
            Log($"Flash Size: {flashSize} MB");
        }
        catch (EfexException ex)
        {
            Log($"Error: {ex.Message}");
        }
    }

    [RelayCommand]
    private void FesGetChipId()
    {
        if (!IsConnected()) return;
        try
        {
            Log("Getting Chip ID...");
            var chipId = Fes.GetChipId(_efexContext!);
            var sb = new StringBuilder();
            foreach (var b in chipId) sb.Append($"{b:X2}");
            Log($"Chip ID: {sb}");
        }
        catch (EfexException ex)
        {
            Log($"Error: {ex.Message}");
        }
    }
}
