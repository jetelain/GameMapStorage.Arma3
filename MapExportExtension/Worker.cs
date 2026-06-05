using System.Globalization;

namespace MapExportExtension
{
    internal static class Worker
    {
        private static MapExportSession? _session;

        internal static void Message(string function, string[] args)
        {
            switch (function)
            {
                case "map":
                    _session?.Dispose();
                    _session = new MapExportSession(
                        ArmaSerializer.ParseString(args[0]) ?? string.Empty,
                        double.Parse(args[1], CultureInfo.InvariantCulture),
                        ArmaSerializer.ParseMixedArray(args[2]),
                        ArmaSerializer.ParseString(args[4]) ?? string.Empty,
                        ArmaSerializer.ParseDouble(args[5]),
                        ArmaSerializer.ParseDouble(args[6]));
                    return;
                case "initscreen":
                    _session?.InitScreen(
                        ArmaSerializer.ParseDoubleArray(args[0]));
                    return;
                case "calibrate":
                    _session?.Calibrate(false,
                        ArmaSerializer.ParseDoubleArray(args[0]),
                        ArmaSerializer.ParseDoubleArray(args[1]),
                        int.Parse(args[2]));
                    return;
                case "hicalibrate":
                    _session?.Calibrate(true,
                        ArmaSerializer.ParseDoubleArray(args[0]),
                        ArmaSerializer.ParseDoubleArray(args[1]),
                        int.Parse(args[2]));
                    return;
                case "screenshot":
                    _session?.ScreenShot(
                        int.Parse(args[0]),
                        int.Parse(args[1]),
                        ArmaSerializer.ParseDoubleArray(args[2]),
                        ArmaSerializer.ParseDoubleArray(args[3]));
                    return;
                case "stop":
                    _session?.Stop();
                    return;
                case "histop":
                    _session?.HiResStop();
                    return;
                case "aerialcalibrate":
                    _session?.AerialCalibrate(double.Parse(args[0], CultureInfo.InvariantCulture));
                    return;
                case "aerialscreenshot":
                    _session?.AerialScreenShot(
                        int.Parse(args[0]),
                        int.Parse(args[1]),
                        ArmaSerializer.ParseDoubleArray(args[2]),
                        ArmaSerializer.ParseDoubleArray(args[3]),
                        ArmaSerializer.ParseDoubleArray(args[4]),
                        ArmaSerializer.ParseDoubleArray(args[5]));
                    return;
                case "aerialstop":
                    _session?.AerialStop();
                    return;
                case "dispose":
                    if (_session != null)
                    {
                        _session.Pack();
                        _session.Dispose();
                        _session = null;
                    }
                    return;
                default:
                    Extension.ErrorMessage($"Unknown function: {function}");
                    return;
            }
        }
    }
}

