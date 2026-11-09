using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Numerics;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;

namespace FrigoTrack
{
    class ServerHttp
    {
        class DatiSessione
        {
            public string idSessione;
            public byte[] chiaveCondivisa;
        }

        static ConcurrentDictionary<string, DatiSessione> sessioniAttive = new ConcurrentDictionary<string, DatiSessione>();
        static string cartellaDati = "./data_received";

        public static void AvviaServer()
        {
            Directory.CreateDirectory(cartellaDati);

            HttpListener ascoltatoreHttp = new HttpListener();
            ascoltatoreHttp.Prefixes.Add("http://localhost:8080/");
            ascoltatoreHttp.Start();
            Console.WriteLine("Server HTTP avviato su http://localhost:8080/");

            while (true)
            {
                HttpListenerContext contesto = ascoltatoreHttp.GetContext();
                System.Threading.ThreadPool.QueueUserWorkItem((_) => GestisciRichiesta(contesto));
            }
        }

        static void GestisciRichiesta(HttpListenerContext contesto)
        {
            string percorso = contesto.Request.Url.AbsolutePath;
            string corpoRichiesta = "";
            using (var lettore = new StreamReader(contesto.Request.InputStream, contesto.Request.Encoding))
            {
                corpoRichiesta = lettore.ReadToEnd();
            }

            if (percorso == "/api/handshake" && contesto.Request.HttpMethod == "POST")
            {
                GestisciHandshake(contesto, corpoRichiesta);
            }
            else if (percorso == "/api/telemetry" && contesto.Request.HttpMethod == "POST")
            {
                GestisciTelemetria(contesto, corpoRichiesta);
            }
            else if (percorso == "/api/readings" && contesto.Request.HttpMethod == "POST")
            {
                GestisciLetture(contesto, corpoRichiesta);
            }
            else if (percorso == "/api/close" && contesto.Request.HttpMethod == "POST")
            {
                GestisciChiusura(contesto, corpoRichiesta);
            }
            else
            {
                InviaRispostaGrezza(contesto, 404, "{\"errore\":\"Non trovato\"}");
            }
        }

        static void GestisciHandshake(HttpListenerContext contesto, string corpo)
        {
            try
            {
                var richiestaJson = JObject.Parse(corpo);
                BigInteger numeroPrimoP = BigInteger.Parse((string)richiestaJson["p"]);
                BigInteger generatoreG = BigInteger.Parse((string)richiestaJson["g"]);
                BigInteger chiaveClientA = BigInteger.Parse((string)richiestaJson["A"]);

                BigInteger segretoB = GestoreCrittografia.GeneraSegreto(numeroPrimoP);
                BigInteger chiaveServerB = GestoreCrittografia.CalcolaPotenzaModulare(generatoreG, segretoB, numeroPrimoP);

                BigInteger segretoCondiviso = GestoreCrittografia.CalcolaPotenzaModulare(chiaveClientA, segretoB, numeroPrimoP);
                byte[] chiaveSimmetrica = GestoreCrittografia.DerivaChiave(segretoCondiviso);

                string identificativoSessione = Guid.NewGuid().ToString("N").Substring(0, 8);
                sessioniAttive[identificativoSessione] = new DatiSessione { idSessione = identificativoSessione, chiaveCondivisa = chiaveSimmetrica };

                var rispostaJson = new JObject
                {
                    ["id_sessione"] = identificativoSessione,
                    ["B"] = chiaveServerB.ToString()
                };
                InviaRispostaGrezza(contesto, 200, rispostaJson.ToString());
            }
            catch
            {
                InviaRispostaGrezza(contesto, 400, "{\"errore\":\"Handshake errato\"}");
            }
        }

        static void GestisciTelemetria(HttpListenerContext contesto, string corpo)
        {
            string testoDecifrato = spacchettaEdecifra(contesto, corpo, out DatiSessione sessione);
            if (testoDecifrato == null) return;

            if (!ValidaJson(testoDecifrato, "schemas/telemetria.json", out string messaggioErrore))
            {
                InviaRispostaCifrata(contesto, 400, sessione.chiaveCondivisa, new JObject { ["errore"] = "Schema non valido", ["messaggio"] = messaggioErrore });
                return;
            }

            JObject dati = JObject.Parse(testoDecifrato);
            string idSensore = (string)dati["id_sensore"];
            double temperatura = (double)dati["temperatura_c"];
            string tipoCarico = (string)dati["tipo_carico"];

            string percorsoFile = Path.Combine(cartellaDati, $"{idSensore}.json");
            File.AppendAllText(percorsoFile, dati.ToString(Formatting.None) + Environment.NewLine);

            bool allarmeAttivo = false;
            string messaggioAllarme = "OK";
            if (tipoCarico == "farmaci" && (temperatura < 2.0 || temperatura > 8.0))
            {
                allarmeAttivo = true;
                messaggioAllarme = "Temperatura farmaci fuori limite (2-8°C)";
            }
            else if (tipoCarico == "surgelati" && temperatura > -18.0)
            {
                allarmeAttivo = true;
                messaggioAllarme = "Temperatura surgelati troppo alta (>-18°C)";
            }

            var rispostaOggetto = new JObject
            {
                ["stato"] = "ACK",
                ["allarme"] = allarmeAttivo,
                ["messaggio"] = messaggioAllarme
            };

            InviaRispostaCifrata(contesto, 200, sessione.chiaveCondivisa, rispostaOggetto);
        }

        static void GestisciLetture(HttpListenerContext contesto, string corpo)
        {
            string testoDecifrato = spacchettaEdecifra(contesto, corpo, out DatiSessione sessione);
            if (testoDecifrato == null) return;

            if (!ValidaJson(testoDecifrato, "schemas/richiesta_letture.json", out string messaggioErrore))
            {
                InviaRispostaCifrata(contesto, 400, sessione.chiaveCondivisa, new JObject { ["errore"] = "Schema non valido", ["messaggio"] = messaggioErrore });
                return;
            }

            JObject richiesta = JObject.Parse(testoDecifrato);
            string idSensore = (string)richiesta["id_sensore"];
            string percorsoFile = Path.Combine(cartellaDati, $"{idSensore}.json");

            JArray elencoLetture = new JArray();
            if (File.Exists(percorsoFile))
            {
                string[] righe = File.ReadAllLines(percorsoFile);
                foreach (var riga in righe)
                {
                    if (!string.IsNullOrWhiteSpace(riga))
                        elencoLetture.Add(JObject.Parse(riga));
                }
            }

            var risposta = new JObject
            {
                ["id_sensore"] = idSensore,
                ["letture"] = elencoLetture
            };

            InviaRispostaCifrata(contesto, 200, sessione.chiaveCondivisa, risposta);
        }

        static void GestisciChiusura(HttpListenerContext contesto, string corpo)
        {
            string testoDecifrato = spacchettaEdecifra(contesto, corpo, out DatiSessione sessione);
            if (testoDecifrato == null) return;

            sessioniAttive.TryRemove(sessione.idSessione, out _);
            InviaRispostaCifrata(contesto, 200, sessione.chiaveCondivisa, new JObject { ["stato"] = "Sessione chiusa" });
        }

        static string spacchettaEdecifra(HttpListenerContext contesto, string corpo, out DatiSessione sessione)
        {
            session = null;
            try
            {
                var busta = JObject.Parse(corpo);
                string idSessioneBusta = (string)busta["id_sessione"];
                string testoCifrato = (string)busta["ciphertext"];
                string tag = (string)busta["tag"];

                if (!sessioniAttive.TryGetValue(idSessioneBusta, out session))
                {
                    InviaRispostaGrezza(contesto, 401, "{\"errore\":\"Non autorizzato / Sessione mancante\"}");
                    return null;
                }

                string decifrato = GestoreCrittografia.Decifra(testoCifrato, tag, session.chiaveCondivisa);
                if (decifrato == null)
                {
                    InviaRispostaGrezza(contesto, 400, "{\"errore\":\"Decifratura o Tag HMAC fallito\"}");
                    return null;
                }
                return decifrato;
            }
            catch
            {
                InviaRispostaGrezza(contesto, 400, "{\"errore\":\"Richiesta non valida\"}");
                return null;
            }
        }

        static bool ValidaJson(string testoJson, string percorsoSchema, out string errore)
        {
            errore = null;
            if (!File.Exists(percorsoSchema)) return true;

            string schemaJson = File.ReadAllText(percorsoSchema);
            JSchema schema = JSchema.Parse(schemaJson);
            JObject oggettoJson = JObject.Parse(testoJson);

            System.Collections.Generic.IList<string> messaggi;
            bool valido = oggettoJson.IsValid(schema, out messaggi);
            if (!valido)
            {
                errore = string.Join("; ", messaggi);
            }
            return valido;
        }

        static void InviaRispostaCifrata(HttpListenerContext contesto, int codiceStato, byte[] chiaveSimmetrica, JObject caricoJson)
        {
            GestoreCrittografia.Cifra(caricoJson.ToString(Formatting.None), chiaveSimmetrica, out string cifrato, out string tag);
            var busta = new JObject
            {
                ["ciphertext"] = cifrato,
                ["tag"] = tag
            };
            InviaRispostaGrezza(contesto, codiceStato, busta.ToString(Formatting.None));
        }

        static void InviaRispostaGrezza(HttpListenerContext contesto, int codiceStato, string testo)
        {
            contesto.Response.StatusCode = codiceStato;
            contesto.Response.ContentType = "application/json";
            byte[] buffer = Encoding.UTF8.GetBytes(testo);
            contesto.Response.ContentLength64 = buffer.Length;
            contesto.Response.OutputStream.Write(buffer, 0, buffer.Length);
            contesto.Response.OutputStream.Close();
        }
    }
}