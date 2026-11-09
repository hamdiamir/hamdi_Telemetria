README - PROGETTO FRIGOTRACK TELEMETRIA SICURA
Studente: [Il Tuo Nome]
Classe: 5IB

ISTRUZIONI DI AVVIO DEL PROGETTO

1. Prerequisiti
Per eseguire il progetto e necessario l'ambiente di sviluppo .NET (.NET 8.0 oppure .NET Framework 4.7.2).
Richiesti i seguenti pacchetti NuGet installati nel progetto:
- Newtonsoft.Json
- Newtonsoft.Json.Schema

2. Come avviare il sistema
Aprire il progetto FrigoTrack su Visual Studio ed eseguire premendo F5.
Il programma avviata automaticamente il server HTTP in background (in ascolto sulla porta 8080) e subito dopo eseguira la simulazione del client sensori.


DESCRIZIONE DEI MESSAGGI E DEGLI ENDPOINT

1. POST /api/handshake
Messaggio inviato in chiaro per concordare la chiave di sessione tramite Diffie-Hellman.
Richiesta: contiene p, g e la chiave pubblica A del client.
Risposta: contiene id_sessione e la chiave pubblica B del server.

2. POST /api/telemetry
Invia la misura del sensore racchiusa in una busta cifrata.
Busta inviata: id_sessione, ciphertext, tag HMAC.
Payload decifrato: id_sensore, timestamp, tipo_carico, temperatura_c, umidita, batteria.
Risposta cifrata: conferma di ricezione (stato ACK) ed eventuale allarme se la temperatura supera i limiti del tipo di merce.

3. POST /api/readings
Richiede lo storico delle misurazioni salvate per uno specifico sensore.
Payload decifrato: id_sensore.
Risposta cifrata: elenco contenente le letture registrate su file dal server.

4. POST /api/close
Richiede la chiusura della sessione attiva e la cancellazione della chiave dalla memoria del server.


TEST EFFETTUATI E RISULTATI

Test 1: Handshake riuscito
Descrizione: Il client invia i parametri p, g, A al server e riceve id_sessione e B. Entrambi ricavano lo stesso segreto condiviso K.
Esito: OK.

Test 2: Messaggio valido
Descrizione: Invio di una telemetria corretta valida secondo lo schema telemetria.json.
Esito: OK (Risposta 200 con stato ACK).

Test 3: Messaggio non valido (Errore Schema)
Descrizione: Invio di una misura con campo temperatura_c pari a 100 (fuori dal range ammesso tra -40 e 40).
Esito: OK (Risposta 400 con dettaglio dell'errore restituito dallo schema).

Test 4: Busta manomessa (Tag fallito)
Descrizione: Modifica di un carattere del ciphertext o del tag prima dell'invio al server.
Esito: OK (Risposta 400 "Decifratura o Tag HMAC fallito" senza elaborare i dati).

Test 5: Sessione inesistente
Descrizione: Invio di una richiesta con un id_sessione errato o non registrato.
Esito: OK (Risposta 401 Non autorizzato).