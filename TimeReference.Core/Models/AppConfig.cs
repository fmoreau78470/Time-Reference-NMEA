using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TimeReference.Core.Models
{
    public class AppConfig
    {
        [JsonPropertyName("serial_port")]
        public string SerialPort { get; set; } = "COM1";

        [JsonPropertyName("baud_rate")]
        public int BaudRate { get; set; } = 9600;

        [JsonPropertyName("timeout")]
        public double Timeout { get; set; } = 1.0;

        [JsonPropertyName("config_ntp")]
        public string NtpConfPath { get; set; } = @"C:\Program Files (x86)\NTP\etc\ntp.conf";

        [JsonPropertyName("servers")]
        public List<string> Servers { get; set; } = new List<string>();

        [JsonPropertyName("server_options")]
        public string ServerOptions { get; set; } = "iburst";

        [JsonPropertyName("time2_value")]
        public double Time2Value { get; set; } = 0.0;

        [JsonPropertyName("utc_mode")]
        public bool UtcMode { get; set; } = false;

        [JsonPropertyName("mini_mode_always_on_top")]
        public bool MiniModeAlwaysOnTop { get; set; } = true;

        [JsonPropertyName("mini_mode_opacity")]
        public double MiniModeOpacity { get; set; } = 1.0;

        [JsonPropertyName("mini_mode_left")]
        public double MiniModeLeft { get; set; } = -1;

        [JsonPropertyName("mini_mode_top")]
        public double MiniModeTop { get; set; } = -1;

        [JsonPropertyName("peers_window_left")]
        public double PeersWindowLeft { get; set; } = -1;

        [JsonPropertyName("peers_window_top")]
        public double PeersWindowTop { get; set; } = -1;
    }
}
