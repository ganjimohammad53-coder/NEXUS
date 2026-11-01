using System.Management;

namespace Nexus.App.Services;

public sealed record HardwareItem(string Label, string Value);
public sealed record HardwareSection(string Title, IReadOnlyList<HardwareItem> Items);
public sealed record PeripheralItem(string Category, string Name, string Manufacturer, string Status, string PnpId);

/// <summary>Reads inventory from Windows WMI/CIM. It deliberately does not synthesize sensor readings.</summary>
public static class HardwareInventoryService
{
    public static IReadOnlyList<HardwareSection> ReadHardware()
    {
        var sections = new List<HardwareSection>();
        sections.Add(Section("Operating system", "SELECT Caption, Version, BuildNumber, OSArchitecture, LastBootUpTime FROM Win32_OperatingSystem", new (string, string)[] {
            ("Name", "Caption"), ("Version", "Version"), ("Build", "BuildNumber"), ("Architecture", "OSArchitecture"), ("Last boot (WMI timestamp)", "LastBootUpTime") }));
        sections.Add(Section("Processor", "SELECT Name, Manufacturer, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, L2CacheSize, L3CacheSize FROM Win32_Processor", new (string, string)[] {
            ("Model", "Name"), ("Manufacturer", "Manufacturer"), ("Cores", "NumberOfCores"), ("Logical processors", "NumberOfLogicalProcessors"), ("Maximum reported clock (MHz)", "MaxClockSpeed"), ("L2 cache (KB)", "L2CacheSize"), ("L3 cache (KB)", "L3CacheSize") }));
        sections.Add(Section("Graphics adapters", "SELECT Name, AdapterRAM, DriverVersion, VideoProcessor, CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate, Status FROM Win32_VideoController", new (string, string)[] {
            ("Adapter", "Name"), ("Reported dedicated memory (bytes)", "AdapterRAM"), ("Driver", "DriverVersion"), ("Video processor", "VideoProcessor"), ("Resolution width", "CurrentHorizontalResolution"), ("Resolution height", "CurrentVerticalResolution"), ("Refresh rate (Hz)", "CurrentRefreshRate"), ("Status", "Status") }));
        sections.Add(Section("Memory modules", "SELECT Capacity, Manufacturer, PartNumber, Speed, ConfiguredClockSpeed FROM Win32_PhysicalMemory", new (string, string)[] {
            ("Capacity (bytes)", "Capacity"), ("Manufacturer", "Manufacturer"), ("Part number", "PartNumber"), ("SPD speed (MHz)", "Speed"), ("Configured speed (MHz)", "ConfiguredClockSpeed") }));
        sections.Add(Section("System and motherboard", "SELECT Manufacturer, Model, SystemType, TotalPhysicalMemory FROM Win32_ComputerSystem", new (string, string)[] {
            ("System manufacturer", "Manufacturer"), ("System model", "Model"), ("System type", "SystemType"), ("Installed physical memory (bytes)", "TotalPhysicalMemory") }));
        sections.Add(Section("Baseboard", "SELECT Manufacturer, Product, Version, SerialNumber FROM Win32_BaseBoard", new (string, string)[] {
            ("Manufacturer", "Manufacturer"), ("Product", "Product"), ("Version", "Version"), ("Serial number", "SerialNumber") }));
        sections.Add(Section("BIOS", "SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS", new (string, string)[] {
            ("Manufacturer", "Manufacturer"), ("Version", "SMBIOSBIOSVersion"), ("Release date (WMI value)", "ReleaseDate") }));
        sections.Add(Section("Storage devices", "SELECT Model, MediaType, InterfaceType, Size, Status, FirmwareRevision FROM Win32_DiskDrive", new (string, string)[] {
            ("Model", "Model"), ("Media type", "MediaType"), ("Interface", "InterfaceType"), ("Capacity (bytes)", "Size"), ("Reported status", "Status"), ("Firmware", "FirmwareRevision") }));
        sections.Add(Section("Network adapters", "SELECT Name, Manufacturer, NetConnectionID, MACAddress, Speed, NetEnabled, AdapterType FROM Win32_NetworkAdapter WHERE PhysicalAdapter = TRUE", new (string, string)[] {
            ("Adapter", "Name"), ("Manufacturer", "Manufacturer"), ("Connection", "NetConnectionID"), ("MAC address", "MACAddress"), ("Link speed (bits/s)", "Speed"), ("Enabled", "NetEnabled"), ("Type", "AdapterType") }));
        sections.Add(Section("Displays", "SELECT Name, MonitorManufacturer, MonitorType, PixelsPerXLogicalInch, PixelsPerYLogicalInch FROM Win32_DesktopMonitor", new (string, string)[] {
            ("Display", "Name"), ("Manufacturer code", "MonitorManufacturer"), ("Monitor type", "MonitorType"), ("Horizontal density", "PixelsPerXLogicalInch"), ("Vertical density", "PixelsPerYLogicalInch") }));
        return sections;
    }

    public static IReadOnlyList<PeripheralItem> ReadPeripherals()
    {
        var result = new List<PeripheralItem>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, Manufacturer, Status, PNPDeviceID, PNPClass FROM Win32_PnPEntity");
            using var collection = searcher.Get();
            foreach (ManagementObject item in collection)
            {
                using (item)
                {
                    var name = Read(item, "Name");
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    var id = Read(item, "PNPDeviceID");
                    var cls = Read(item, "PNPClass");
                    var category = Categorize(name, id, cls);
                    if (category is null) continue;
                    result.Add(new PeripheralItem(category, name, EmptyAsUnavailable(Read(item, "Manufacturer")), EmptyAsUnavailable(Read(item, "Status")), EmptyAsUnavailable(id)));
                }
            }
        }
        catch { /* The UI reports unavailable inventory without crashing the application. */ }
        return result.DistinctBy(x => x.PnpId, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static HardwareSection Section(string title, string query, (string Label, string Property)[] fields)
    {
        var items = new List<HardwareItem>();
        try
        {
            using var searcher = new ManagementObjectSearcher(query);
            using var collection = searcher.Get();
            var index = 0;
            foreach (ManagementObject row in collection)
            {
                using (row)
                {
                    if (index++ > 0) items.Add(new HardwareItem("────────", "────────"));
                    foreach (var field in fields)
                    {
                        var value = Read(row, field.Property);
                        items.Add(new HardwareItem(field.Label, string.IsNullOrWhiteSpace(value) ? "Not reported by Windows" : Format(field.Property, value)));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            items.Add(new HardwareItem("Availability", ex is UnauthorizedAccessException ? "Access denied by Windows" : "WMI data unavailable"));
        }
        return new HardwareSection(title, items);
    }

    private static string Read(ManagementBaseObject row, string property)
    {
        try { return Convert.ToString(row[property], System.Globalization.CultureInfo.InvariantCulture)?.Trim() ?? ""; }
        catch { return ""; }
    }

    private static string Format(string property, string value)
    {
        if (property is "AdapterRAM" or "Capacity" or "TotalPhysicalMemory" or "Size" && long.TryParse(value, out var bytes) && bytes >= 0)
            return $"{bytes:N0} bytes ({bytes / 1073741824d:N2} GiB)";
        return value;
    }

    private static string EmptyAsUnavailable(string value) => string.IsNullOrWhiteSpace(value) ? "Not reported" : value;

    private static string? Categorize(string name, string id, string pnpClass)
    {
        var text = $"{name} {id} {pnpClass}".ToLowerInvariant();
        if (text.Contains("bluetooth")) return "Bluetooth";
        if (text.Contains("mouse") || text.Contains("mice")) return "Mouse";
        if (text.Contains("keyboard") || text.Contains("kbd")) return "Keyboard";
        if (text.Contains("headset") || text.Contains("headphone") || text.Contains("earphone")) return "Headsets";
        if (text.Contains("microphone") || text.Contains("audio") || text.Contains("sound")) return "Audio / microphones";
        if (text.Contains("camera") || text.Contains("webcam")) return "Cameras";
        if (text.Contains("monitor") || pnpClass.Equals("Monitor", StringComparison.OrdinalIgnoreCase)) return "Monitors";
        if (text.Contains("game controller") || text.Contains("xbox") || text.Contains("playstation") || text.Contains("controller") || text.Contains("joystick")) return "Game controllers";
        if (text.Contains("usb")) return "USB devices and hubs";
        if (text.Contains("printer") || text.Contains("scanner")) return "Printers / scanners";
        if (text.Contains("hidclass") || pnpClass.Equals("HIDClass", StringComparison.OrdinalIgnoreCase)) return "HID devices";
        return null;
    }
}
