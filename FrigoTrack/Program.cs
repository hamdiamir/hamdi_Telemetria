using System;
using System.Threading.Tasks;

namespace FrigoTrack
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== AVVIO SISTEMA FRIGOTRACK ==-\n");

            Task attivitaServer = Task.Run(() => ServerHttp.AvviaServer());

            await Task.Delay(500);

            await ClientSensori.EseguiSimulazione();

            Console.WriteLine("\nSimulazione completata. Premi un tasto per chiudere.");
            Console.ReadKey();
        }
    }
}