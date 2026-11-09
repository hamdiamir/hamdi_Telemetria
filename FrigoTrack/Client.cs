using System;
using System.Net.Http;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FrigoTrack
{
    class ClientSensori
    {
        static readonly HttpClient clientHttp = new HttpClient();
        static string urlBase = "http://localhost:8080/api";

        static BigInteger numeroPrimoP = BigInteger.Parse("997");
        static BigInteger generatoreG = 2;

        public static async Task EseguiSimulazione()
        {
            Console.WriteLine("--- SIMULATORE SENSORI FRIGOTRACK ---");

            await AvviaSimulazioneSensore("CELLA-BG-001", "farmaci", 5.2, 45, 95);
            await AvviaSimulazioneSensore("CELLA-BG-002", "farmaci", 10.5, 50, 80);
            await AvviaSimulazioneSensore("CAMION-MI-102", "surgelati", -20.0, 60, 88);
        }

        static async Task AvviaSimulazioneSensore(string idSensore, string tipoCarico, double temperatura, double umidita, int batteria)
        {
            Console.WriteLine($"\n[+] Avvio handshake per sensore: {idSensore}");

            BigInteger segretoA = GestoreCrittografia.GeneraSegreto(numeroPrimoP);
            BigInteger chiaveClientA = GestoreCrittografia.CalcolaPotenzaModulare(generatoreG, segretoA, numeroPrimoP);

            var richiestaHandshake = new JObject
            {
                ["p"] = numeroPrimoP.ToString(),
                ["g"] = generatoreG.ToString(),
                ["A"] = chiaveClientA.ToString()
            };

            var rispostaHandshake = await InviaPostAsync($"{urlBase}/handshake", richiestaHandshake.ToString(Formatting.None));
            if (rispostaHandshake.StatusCode != System.Net.HttpStatusCode.OK)
            {
                Console.WriteLine("[-] Handshake fallito");
                return;
            }

            var corpoHandshake = JObject.Parse(await rispostaHandshake.Content.ReadAsStringAsync());
            string idSessione = (string)corpoHandshake["id_sessione"];
            BigInteger chiaveServerB = BigInteger.Parse((string)corpoHandshake["B"]);

            BigInteger segretoCondiviso = GestoreCrittografia.CalcolaPotenzaModulare(chiaveServerB, segretoA, numeroPrimoP);
            byte[] chiaveSimmetrica = GestoreCrittografia.DerivaChiave(segretoCondiviso);
            Console.WriteLine($"[+] Sessione ottenuta: {idSessione}");

            var datiTelemetria = new JObject
            {
                ["id_sensore"] = idSensore,
                ["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ["tipo_carico"] = tipoCarico,
                ["temperatura_c"] = temperatura,
                ["umidita"] = umidita,
                ["batteria"] = batteria
            };

            GestoreCrittografia.Cifra(datiTelemetria.ToString(Formatting.None), chiaveSimmetrica, out string cifrato, out string tag);

            var busta = new JObject
            {
                ["id_sessione"] = idSessione,
                ["ciphertext"] = cifrato,
                ["tag"] = tag
            };

            Console.WriteLine($"[+] Invio dati cifrati per {idSensore}...");
            var rispostaTelemetria = await InviaPostAsync($"{urlBase}/telemetry", busta.ToString(Formatting.None));
            string contenutoRisposta = await rispostaTelemetria.Content.ReadAsStringAsync();

            if (rispostaTelemetria.IsSuccessStatusCode)
            {
                var bustaRisposta = JObject.Parse(contenutoRisposta);
                string chiaroRisposta = GestoreCrittografia.Decifra((string)bustaRisposta["ciphertext"], (string)bustaRisposta["tag"], chiaveSimmetrica);
                Console.WriteLine($"[Server Response]: {chiaroRisposta}");
            }
            else
            {
                Console.WriteLine($"[-] Errore dal server ({rispostaTelemetria.StatusCode}): {contenutoRisposta}");
            }

            GestoreCrittografia.Cifra(new JObject { ["azione"] = "chiudi" }.ToString(), chiaveSimmetrica, out string cifratoChiusura, out string tagChiusura);
            var bustaChiusura = new JObject { ["id_sessione"] = idSessione, ["ciphertext"] = cifratoChiusura, ["tag"] = tagChiusura };
            await InviaPostAsync($"{urlBase}/close", bustaChiusura.ToString(Formatting.None));
            Console.WriteLine($"[+] Sessione {idSessione} chiusa.");
        }

        static async Task<HttpResponseMessage> InviaPostAsync(string url, string jsonString)
        {
            var contenuto = new StringContent(jsonString, Encoding.UTF8, "application/json");
            return await clientHttp.PostAsync(url, contenuto);
        }
    }
}