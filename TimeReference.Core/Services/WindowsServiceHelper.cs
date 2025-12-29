// Création du fichier : d:\Francis\Documents\code\Time reference NMEA\CSharp_Version\TimeReference.Core\Services\WindowsServiceHelper.cs

using System;
using System.ServiceProcess;
using System.Runtime.Versioning;

namespace TimeReference.Core.Services
{
    [SupportedOSPlatform("windows")]
    public static class WindowsServiceHelper
    {
        /// <summary>
        /// Redémarre un service Windows donné par son nom (ex: "NTP").
        /// Nécessite des droits d'administrateur.
        /// </summary>
        public static void RestartService(string serviceName)
        {
            using (ServiceController service = new ServiceController(serviceName))
            {
                // Note : Si le service n'est pas installé, une exception sera levée ici.
                
                if (service.Status == ServiceControllerStatus.Running)
                {
                    service.Stop();
                    service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
                }

                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
            }
        }

        public static void StartService(string serviceName)
        {
            using (ServiceController service = new ServiceController(serviceName))
            {
                if (service.Status != ServiceControllerStatus.Running)
                {
                    service.Start();
                    service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
                }
            }
        }

        public static void StopService(string serviceName)
        {
            using (ServiceController service = new ServiceController(serviceName))
            {
                if (service.Status != ServiceControllerStatus.Stopped)
                {
                    service.Stop();
                    service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
                }
            }
        }

        public static ServiceControllerStatus? GetStatus(string serviceName)
        {
            try
            {
                using (ServiceController service = new ServiceController(serviceName))
                {
                    return service.Status;
                }
            }
            catch
            {
                // Le service n'existe probablement pas
                return null;
            }
        }
    }
}
