using System;
using System.IO.Ports;
using System.Threading;
using TimeReference.Core.Models;

namespace TimeReference.Core.Services;

public class SerialGpsReader
{
    private SerialPort? _serialPort;
    private Thread? _readThread;
    private volatile bool _keepReading;
    private readonly NmeaParser _parser;

    // Événement déclenché quand une nouvelle donnée GPS est prête
    public event Action<GpsData>? GpsDataReceived;
    
    // Événement pour signaler une erreur (ex: port débranché)
    public event Action<string>? ErrorOccurred;

    public bool IsConnected => _serialPort != null && _serialPort.IsOpen;

    public SerialGpsReader()
    {
        _parser = new NmeaParser();
    }

    public void Start(string portName, int baudRate = 9600)
    {
        Stop(); // Sécurité : on ferme si déjà ouvert

        try
        {
            // Configuration du port série (8 bits de données, pas de parité, 1 bit de stop)
            _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
            _serialPort.ReadTimeout = 1000; // Timeout de 1s pour ne pas bloquer indéfiniment
            _serialPort.DtrEnable = true;   // Active le signal DTR (souvent requis pour l'alimentation/réveil)
            _serialPort.RtsEnable = true;   // Active le signal RTS
            _serialPort.Open();

            _keepReading = true;
            
            // On lance la lecture dans un thread séparé (arrière-plan)
            _readThread = new Thread(ReadLoop);
            _readThread.IsBackground = true; // Le thread s'arrête si l'app ferme
            _readThread.Start();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Impossible d'ouvrir {portName}: {ex.Message}");
        }
    }

    public void Stop()
    {
        _keepReading = false;
        
        // On attend un peu que le thread finisse proprement
        if (_readThread != null && _readThread.IsAlive)
        {
            _readThread.Join(500);
        }

        if (_serialPort != null && _serialPort.IsOpen)
        {
            try { _serialPort.Close(); } catch { }
            _serialPort.Dispose();
            _serialPort = null;
        }
    }

    // Boucle de lecture infinie (tant que connecté)
    private void ReadLoop()
    {
        while (_keepReading && _serialPort != null && _serialPort.IsOpen)
        {
            try
            {
                // ReadLine lit jusqu'à trouver un saut de ligne (\n)
                string line = _serialPort.ReadLine();
                
                if (!string.IsNullOrWhiteSpace(line))
                {
                    // On décode la ligne
                    var data = _parser.Parse(line);
                    
                    // On prévient l'application qu'on a reçu quelque chose
                    GpsDataReceived?.Invoke(data);
                }
            }
            catch (TimeoutException)
            {
                // C'est normal si le GPS n'envoie rien pendant 1s, on boucle simplement
            }
            catch (Exception ex)
            {
                if (_keepReading) // Si c'est une vraie erreur inattendue
                {
                    ErrorOccurred?.Invoke($"Erreur lecture: {ex.Message}");
                }
            }
        }
    }
}
